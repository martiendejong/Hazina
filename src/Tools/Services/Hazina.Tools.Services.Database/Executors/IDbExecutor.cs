using Hazina.Tools.Services.Database.Core;

namespace Hazina.Tools.Services.Database.Executors;

/// <summary>
/// Interface for database query execution.
/// Implementations handle connection management and result mapping.
/// </summary>
public interface IDbExecutor : IAsyncDisposable
{
    /// <summary>
    /// Executes a compiled query and returns results.
    /// </summary>
    /// <param name="query">The compiled query with SQL and parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Query results with rows and metadata.</returns>
    Task<QueryResult> ExecuteAsync(CompiledQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a raw SQL command (for schema operations, not agent queries).
    /// </summary>
    /// <param name="sql">SQL command to execute.</param>
    /// <param name="parameters">Optional parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of rows affected.</returns>
    Task<int> ExecuteNonQueryAsync(string sql, Dictionary<string, object?>? parameters = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the database schema by introspecting the database.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Database schema.</returns>
    Task<DatabaseSchema> GetSchemaAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Tests the database connection.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if connection is valid.</returns>
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of executing a query.
/// </summary>
public class QueryResult
{
    /// <summary>
    /// Column names in result order.
    /// </summary>
    public List<string> Columns { get; set; } = new();

    /// <summary>
    /// Result rows as dictionaries.
    /// </summary>
    public List<Dictionary<string, object?>> Rows { get; set; } = new();

    /// <summary>
    /// Total rows returned.
    /// </summary>
    public int RowCount => Rows.Count;

    /// <summary>
    /// Whether query was truncated by limit.
    /// </summary>
    public bool IsTruncated { get; set; }

    /// <summary>
    /// Execution time in milliseconds.
    /// </summary>
    public long ExecutionTimeMs { get; set; }

    /// <summary>
    /// Error message if query failed.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Whether query executed successfully.
    /// </summary>
    public bool IsSuccess => string.IsNullOrEmpty(Error);
}
