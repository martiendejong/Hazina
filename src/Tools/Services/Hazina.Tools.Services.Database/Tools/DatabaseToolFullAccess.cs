using System.Text.Json;
using Microsoft.Extensions.Logging;
using Hazina.Tools.Services.Database.Abstractions;
using Hazina.Tools.Services.Database.Core;
using Hazina.Tools.Services.Chat.Tools;

namespace Hazina.Tools.Services.Database.Tools;

/// <summary>
/// Database tool with full access for owner-level operations.
/// No row-level restrictions applied - suitable for project owners and internal tools.
/// </summary>
public class DatabaseToolFullAccess
{
    private readonly IDatabaseService _databaseService;
    private readonly ILogger<DatabaseToolFullAccess> _logger;
    private readonly Func<string, DatabaseSchema> _schemaResolver;
    private readonly Func<string, string> _databasePathResolver;

    public DatabaseToolFullAccess(
        IDatabaseService databaseService,
        ILogger<DatabaseToolFullAccess> logger,
        Func<string, DatabaseSchema> schemaResolver,
        Func<string, string> databasePathResolver)
    {
        _databaseService = databaseService;
        _logger = logger;
        _schemaResolver = schemaResolver;
        _databasePathResolver = databasePathResolver;
    }

    /// <summary>
    /// Gets the tool definition for LLM function calling.
    /// </summary>
    public static IToolDefinition GetToolDefinition()
    {
        return new ToolDefinition
        {
            Name = "database_query",
            Description = "Query the project database to retrieve stored data. Use structured queries with table name, columns, filters, and ordering. All queries are validated against the schema and use parameterized SQL for security.",
            Parameters = JsonSerializer.Deserialize<JsonElement>(ToolSchema)
        };
    }

    /// <summary>
    /// Executes a database query with full access.
    /// </summary>
    /// <param name="argumentsJson">JSON arguments containing the structured query.</param>
    /// <param name="projectId">Current project ID for database resolution.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result with query data or error.</returns>
    public async Task<IToolResult> ExecuteAsync(
        string argumentsJson,
        string projectId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var args = JsonSerializer.Deserialize<QueryArgs>(argumentsJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (args == null)
            {
                return new ToolResult { Success = false, Error = "Invalid query arguments" };
            }

            var query = BuildQuery(args);
            var schema = _schemaResolver(projectId);
            var dbPath = _databasePathResolver(projectId);

            _logger.LogInformation("Executing full access query on table {Table} for project {ProjectId}",
                query.Table, projectId);

            var result = await _databaseService.ExecuteQueryAsync(query, dbPath, schema, cancellationToken);

            if (!result.IsSuccess)
            {
                return new ToolResult { Success = false, Error = result.Error ?? "Query execution failed" };
            }

            return new ToolResult
            {
                Success = true,
                Result = new QueryResponse
                {
                    Columns = result.Columns,
                    Rows = result.Rows,
                    RowCount = result.RowCount,
                    IsTruncated = result.IsTruncated,
                    ExecutionTimeMs = result.ExecutionTimeMs
                },
                TokensUsed = EstimateTokens(result)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing database query");
            return new ToolResult { Success = false, Error = $"Query failed: {ex.Message}" };
        }
    }

    private static StructuredQuery BuildQuery(QueryArgs args)
    {
        var query = new StructuredQuery
        {
            Table = args.Table,
            Select = args.Select ?? Array.Empty<string>(),
            Limit = args.Limit > 0 ? args.Limit : 100,
            Offset = args.Offset > 0 ? args.Offset : 0
        };

        if (args.Where != null)
        {
            query.Where = args.Where.Select(w => new WhereClause
            {
                Column = w.Column,
                Operator = w.Operator ?? "=",
                Value = w.Value,
                Connector = w.Connector ?? "AND"
            }).ToArray();
        }

        if (args.OrderBy != null)
        {
            query.OrderBy = args.OrderBy.Select(o => new OrderByClause
            {
                Column = o.Column,
                Descending = o.Descending
            }).ToArray();
        }

        if (args.GroupBy != null)
        {
            query.GroupBy = args.GroupBy;
        }

        if (args.Aggregates != null)
        {
            query.Aggregates = args.Aggregates.Select(a => new AggregateClause
            {
                Function = a.Function,
                Column = a.Column,
                Alias = a.Alias ?? ""
            }).ToArray();
        }

        return query;
    }

    private static int EstimateTokens(Executors.QueryResult result)
    {
        // Rough estimate: column names + row data
        var estimate = result.Columns.Sum(c => c.Length);
        foreach (var row in result.Rows)
        {
            estimate += row.Values.Sum(v => v?.ToString()?.Length ?? 0);
        }
        return estimate / 4; // ~4 chars per token
    }

    private const string ToolSchema = @"{
        ""type"": ""object"",
        ""properties"": {
            ""table"": {
                ""type"": ""string"",
                ""description"": ""The table name to query""
            },
            ""select"": {
                ""type"": ""array"",
                ""items"": { ""type"": ""string"" },
                ""description"": ""Columns to select. Use ['*'] or omit for all columns.""
            },
            ""where"": {
                ""type"": ""array"",
                ""items"": {
                    ""type"": ""object"",
                    ""properties"": {
                        ""column"": { ""type"": ""string"" },
                        ""operator"": {
                            ""type"": ""string"",
                            ""enum"": [""="", ""!="", "">"", "">="", ""<"", ""<="", ""LIKE"", ""NOT LIKE"", ""IN"", ""NOT IN"", ""IS NULL"", ""IS NOT NULL"", ""BETWEEN""]
                        },
                        ""value"": {},
                        ""connector"": {
                            ""type"": ""string"",
                            ""enum"": [""AND"", ""OR""],
                            ""default"": ""AND""
                        }
                    },
                    ""required"": [""column""]
                },
                ""description"": ""Filter conditions""
            },
            ""orderBy"": {
                ""type"": ""array"",
                ""items"": {
                    ""type"": ""object"",
                    ""properties"": {
                        ""column"": { ""type"": ""string"" },
                        ""descending"": { ""type"": ""boolean"", ""default"": false }
                    },
                    ""required"": [""column""]
                },
                ""description"": ""Sort order""
            },
            ""groupBy"": {
                ""type"": ""array"",
                ""items"": { ""type"": ""string"" },
                ""description"": ""Group by columns""
            },
            ""aggregates"": {
                ""type"": ""array"",
                ""items"": {
                    ""type"": ""object"",
                    ""properties"": {
                        ""function"": {
                            ""type"": ""string"",
                            ""enum"": [""COUNT"", ""SUM"", ""AVG"", ""MIN"", ""MAX""]
                        },
                        ""column"": { ""type"": ""string"" },
                        ""alias"": { ""type"": ""string"" }
                    },
                    ""required"": [""function"", ""column""]
                },
                ""description"": ""Aggregate functions""
            },
            ""limit"": {
                ""type"": ""integer"",
                ""default"": 100,
                ""maximum"": 10000,
                ""description"": ""Maximum rows to return""
            },
            ""offset"": {
                ""type"": ""integer"",
                ""default"": 0,
                ""description"": ""Rows to skip for pagination""
            }
        },
        ""required"": [""table""]
    }";

    // Argument classes for deserialization
    private class QueryArgs
    {
        public string Table { get; set; } = "";
        public string[]? Select { get; set; }
        public WhereArg[]? Where { get; set; }
        public OrderByArg[]? OrderBy { get; set; }
        public string[]? GroupBy { get; set; }
        public AggregateArg[]? Aggregates { get; set; }
        public int Limit { get; set; } = 100;
        public int Offset { get; set; }
    }

    private class WhereArg
    {
        public string Column { get; set; } = "";
        public string? Operator { get; set; }
        public object? Value { get; set; }
        public string? Connector { get; set; }
    }

    private class OrderByArg
    {
        public string Column { get; set; } = "";
        public bool Descending { get; set; }
    }

    private class AggregateArg
    {
        public string Function { get; set; } = "";
        public string Column { get; set; } = "";
        public string? Alias { get; set; }
    }

    private class QueryResponse
    {
        public List<string> Columns { get; set; } = new();
        public List<Dictionary<string, object?>> Rows { get; set; } = new();
        public int RowCount { get; set; }
        public bool IsTruncated { get; set; }
        public long ExecutionTimeMs { get; set; }
    }
}
