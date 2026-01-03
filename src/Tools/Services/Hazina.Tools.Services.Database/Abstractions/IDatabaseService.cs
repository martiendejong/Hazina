using Hazina.Tools.Services.Database.Core;
using Hazina.Tools.Services.Database.Executors;

namespace Hazina.Tools.Services.Database.Abstractions;

/// <summary>
/// Service interface for database operations.
/// Provides query compilation and execution with schema validation.
/// </summary>
public interface IDatabaseService
{
    /// <summary>
    /// Executes a structured query with full access (no restrictions).
    /// </summary>
    /// <param name="query">The structured query to execute.</param>
    /// <param name="databasePath">Path to the database file.</param>
    /// <param name="schema">Database schema for validation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Query results.</returns>
    Task<QueryResult> ExecuteQueryAsync(
        StructuredQuery query,
        string databasePath,
        DatabaseSchema schema,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a structured query with restrictions applied.
    /// </summary>
    /// <param name="query">The structured query to execute.</param>
    /// <param name="databasePath">Path to the database file.</param>
    /// <param name="schema">Database schema for validation.</param>
    /// <param name="restriction">Restriction policy to apply.</param>
    /// <param name="context">Context for resolving restriction values.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Query results (empty if restricted data not accessible).</returns>
    Task<QueryResult> ExecuteRestrictedQueryAsync(
        StructuredQuery query,
        string databasePath,
        DatabaseSchema schema,
        RestrictionPolicy restriction,
        RestrictionContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the schema for a database.
    /// </summary>
    /// <param name="databasePath">Path to the database file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Database schema.</returns>
    Task<DatabaseSchema> GetSchemaAsync(
        string databasePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates tables in the database based on schema definition.
    /// </summary>
    /// <param name="databasePath">Path to the database file.</param>
    /// <param name="schema">Schema defining tables to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task CreateTablesAsync(
        string databasePath,
        DatabaseSchema schema,
        CancellationToken cancellationToken = default);
}
