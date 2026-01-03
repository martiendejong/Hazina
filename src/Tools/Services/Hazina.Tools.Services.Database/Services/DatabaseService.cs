using Microsoft.Extensions.Logging;
using Hazina.Tools.Services.Database.Abstractions;
using Hazina.Tools.Services.Database.Core;
using Hazina.Tools.Services.Database.Executors;

namespace Hazina.Tools.Services.Database.Services;

/// <summary>
/// Default implementation of IDatabaseService.
/// Provides query compilation and execution with schema validation.
/// </summary>
public class DatabaseService : IDatabaseService
{
    private readonly ILogger<DatabaseService> _logger;

    public DatabaseService(ILogger<DatabaseService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<QueryResult> ExecuteQueryAsync(
        StructuredQuery query,
        string databasePath,
        DatabaseSchema schema,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Executing query on table {Table} with full access", query.Table);

        var compiler = new SqlCompiler(schema, SqlDialect.SQLite);
        var compiled = compiler.Compile(query);

        if (!compiled.IsValid)
        {
            _logger.LogWarning("Query validation failed: {Errors}", string.Join(", ", compiled.Errors));
            return new QueryResult
            {
                Error = $"Query validation failed: {string.Join(", ", compiled.Errors)}"
            };
        }

        _logger.LogDebug("Compiled SQL: {Sql}", compiled.Sql);

        await using var executor = new SqliteExecutor(databasePath);
        return await executor.ExecuteAsync(compiled, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<QueryResult> ExecuteRestrictedQueryAsync(
        StructuredQuery query,
        string databasePath,
        DatabaseSchema schema,
        RestrictionPolicy restriction,
        RestrictionContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Executing restricted query on table {Table} with policy {Policy}",
            query.Table, restriction.Name);

        var compiler = new SqlCompiler(schema, SqlDialect.SQLite);
        var compiled = compiler.Compile(query, restriction, context);

        if (!compiled.IsValid)
        {
            if (restriction.SilentRestriction)
            {
                _logger.LogDebug("Restriction failed silently, returning empty result");
                return new QueryResult();
            }

            _logger.LogWarning("Restricted query validation failed: {Errors}", string.Join(", ", compiled.Errors));
            return new QueryResult
            {
                Error = $"Query validation failed: {string.Join(", ", compiled.Errors)}"
            };
        }

        _logger.LogDebug("Compiled restricted SQL: {Sql}", compiled.Sql);

        await using var executor = new SqliteExecutor(databasePath);
        return await executor.ExecuteAsync(compiled, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<DatabaseSchema> GetSchemaAsync(
        string databasePath,
        CancellationToken cancellationToken = default)
    {
        await using var executor = new SqliteExecutor(databasePath);
        return await executor.GetSchemaAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task CreateTablesAsync(
        string databasePath,
        DatabaseSchema schema,
        CancellationToken cancellationToken = default)
    {
        await using var executor = new SqliteExecutor(databasePath);

        foreach (var table in schema.Tables.Values)
        {
            var columns = new List<string>();
            foreach (var col in table.Columns)
            {
                var sqlType = col.DataType.ToUpperInvariant() switch
                {
                    "INTEGER" => "INTEGER",
                    "REAL" => "REAL",
                    "BLOB" => "BLOB",
                    "DATETIME" => "TEXT", // SQLite stores datetime as TEXT
                    _ => "TEXT"
                };
                columns.Add($"\"{col.Name}\" {sqlType}");
            }

            var sql = $"CREATE TABLE IF NOT EXISTS \"{table.Name}\" ({string.Join(", ", columns)})";
            _logger.LogDebug("Creating table: {Sql}", sql);
            await executor.ExecuteNonQueryAsync(sql, cancellationToken: cancellationToken);
        }
    }
}
