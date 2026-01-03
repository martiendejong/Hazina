using System.Text.Json;
using Microsoft.Extensions.Logging;
using Hazina.Tools.Services.Database.Abstractions;
using Hazina.Tools.Services.Database.Core;
using Hazina.Tools.Services.Chat.Tools;

namespace Hazina.Tools.Services.Database.Tools;

/// <summary>
/// Database tool with restricted access for multi-tenant scenarios.
/// Automatically applies row-level security via subquery restrictions.
/// Silent failure mode prevents information leakage.
/// </summary>
public class DatabaseToolRestrictedAccess
{
    private readonly IDatabaseService _databaseService;
    private readonly ILogger<DatabaseToolRestrictedAccess> _logger;
    private readonly Func<string, DatabaseSchema> _schemaResolver;
    private readonly Func<string, string> _databasePathResolver;
    private readonly Func<string, RestrictionPolicy> _restrictionResolver;

    public DatabaseToolRestrictedAccess(
        IDatabaseService databaseService,
        ILogger<DatabaseToolRestrictedAccess> logger,
        Func<string, DatabaseSchema> schemaResolver,
        Func<string, string> databasePathResolver,
        Func<string, RestrictionPolicy> restrictionResolver)
    {
        _databaseService = databaseService;
        _logger = logger;
        _schemaResolver = schemaResolver;
        _databasePathResolver = databasePathResolver;
        _restrictionResolver = restrictionResolver;
    }

    /// <summary>
    /// Gets the tool definition for LLM function calling.
    /// </summary>
    public static IToolDefinition GetToolDefinition()
    {
        return new ToolDefinition
        {
            Name = "database_query_restricted",
            Description = "Query the project database with automatic scope restrictions. Only returns data accessible to the current context (project, user, account). All queries are validated and use parameterized SQL.",
            Parameters = JsonSerializer.Deserialize<JsonElement>(ToolSchema)
        };
    }

    /// <summary>
    /// Executes a database query with restrictions applied.
    /// </summary>
    /// <param name="argumentsJson">JSON arguments containing the structured query.</param>
    /// <param name="projectId">Current project ID.</param>
    /// <param name="userId">Current user ID (optional).</param>
    /// <param name="accountId">Current connected account ID (optional).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result with query data or empty result if restricted.</returns>
    public async Task<IToolResult> ExecuteAsync(
        string argumentsJson,
        string projectId,
        string? userId = null,
        string? accountId = null,
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
            var restriction = _restrictionResolver(projectId);

            var context = new RestrictionContext
            {
                ProjectId = projectId,
                UserId = userId,
                AccountId = accountId
            };

            _logger.LogInformation(
                "Executing restricted query on table {Table} for project {ProjectId} with policy {Policy}",
                query.Table, projectId, restriction.Name);

            var result = await _databaseService.ExecuteRestrictedQueryAsync(
                query, dbPath, schema, restriction, context, cancellationToken);

            if (!result.IsSuccess)
            {
                // For restricted queries, we may want to return empty rather than error
                // depending on the restriction policy's silent mode
                if (restriction.SilentRestriction)
                {
                    _logger.LogDebug("Query failed silently due to restrictions");
                    return new ToolResult
                    {
                        Success = true,
                        Result = new QueryResponse
                        {
                            Columns = new List<string>(),
                            Rows = new List<Dictionary<string, object?>>(),
                            RowCount = 0,
                            Message = "No accessible data found"
                        }
                    };
                }

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
            _logger.LogError(ex, "Error executing restricted database query");
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
        var estimate = result.Columns.Sum(c => c.Length);
        foreach (var row in result.Rows)
        {
            estimate += row.Values.Sum(v => v?.ToString()?.Length ?? 0);
        }
        return estimate / 4;
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
                ""description"": ""Filter conditions (in addition to automatic restrictions)""
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
                ""maximum"": 1000,
                ""description"": ""Maximum rows to return (lower limit for restricted access)""
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
        public string? Message { get; set; }
    }
}
