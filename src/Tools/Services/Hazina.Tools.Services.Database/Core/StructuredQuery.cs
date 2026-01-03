using System.Text.Json.Serialization;

namespace Hazina.Tools.Services.Database.Core;

/// <summary>
/// Represents a structured database query that can be safely compiled to SQL.
/// Agents provide this model instead of raw SQL strings.
/// </summary>
public class StructuredQuery
{
    /// <summary>
    /// The table name to query. Must match a registered table schema.
    /// </summary>
    public string Table { get; set; } = string.Empty;

    /// <summary>
    /// Columns to select. Use "*" for all allowed columns.
    /// </summary>
    public string[] Select { get; set; } = Array.Empty<string>();

    /// <summary>
    /// WHERE conditions. All conditions are AND-ed together.
    /// </summary>
    public WhereClause[] Where { get; set; } = Array.Empty<WhereClause>();

    /// <summary>
    /// ORDER BY clauses.
    /// </summary>
    public OrderByClause[] OrderBy { get; set; } = Array.Empty<OrderByClause>();

    /// <summary>
    /// GROUP BY columns.
    /// </summary>
    public string[] GroupBy { get; set; } = Array.Empty<string>();

    /// <summary>
    /// HAVING conditions (for aggregated results).
    /// </summary>
    public WhereClause[] Having { get; set; } = Array.Empty<WhereClause>();

    /// <summary>
    /// Maximum rows to return. Default 100, max 10000.
    /// </summary>
    public int Limit { get; set; } = 100;

    /// <summary>
    /// Rows to skip (for pagination).
    /// </summary>
    public int Offset { get; set; } = 0;

    /// <summary>
    /// Aggregate functions to apply.
    /// </summary>
    public AggregateClause[] Aggregates { get; set; } = Array.Empty<AggregateClause>();
}

/// <summary>
/// Represents a WHERE condition.
/// </summary>
public class WhereClause
{
    /// <summary>
    /// Column name to filter on.
    /// </summary>
    public string Column { get; set; } = string.Empty;

    /// <summary>
    /// Comparison operator: =, !=, >, >=, <, <=, LIKE, IN, NOT IN, IS NULL, IS NOT NULL
    /// </summary>
    public string Operator { get; set; } = "=";

    /// <summary>
    /// Value to compare against. For IN/NOT IN, provide an array.
    /// For IS NULL/IS NOT NULL, this is ignored.
    /// </summary>
    public object? Value { get; set; }

    /// <summary>
    /// Logical connector to next clause: AND, OR
    /// </summary>
    public string Connector { get; set; } = "AND";
}

/// <summary>
/// Represents an ORDER BY clause.
/// </summary>
public class OrderByClause
{
    /// <summary>
    /// Column to order by.
    /// </summary>
    public string Column { get; set; } = string.Empty;

    /// <summary>
    /// True for descending order, false for ascending.
    /// </summary>
    public bool Descending { get; set; } = false;
}

/// <summary>
/// Represents an aggregate function.
/// </summary>
public class AggregateClause
{
    /// <summary>
    /// Function name: COUNT, SUM, AVG, MIN, MAX
    /// </summary>
    public string Function { get; set; } = string.Empty;

    /// <summary>
    /// Column to aggregate. Use "*" for COUNT(*).
    /// </summary>
    public string Column { get; set; } = string.Empty;

    /// <summary>
    /// Alias for the result column.
    /// </summary>
    public string Alias { get; set; } = string.Empty;
}

/// <summary>
/// Result of compiling a StructuredQuery.
/// </summary>
public class CompiledQuery
{
    /// <summary>
    /// The parameterized SQL string.
    /// </summary>
    public string Sql { get; set; } = string.Empty;

    /// <summary>
    /// Parameters for the query.
    /// </summary>
    public Dictionary<string, object?> Parameters { get; set; } = new();

    /// <summary>
    /// Validation errors, if any.
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Whether the query is valid.
    /// </summary>
    public bool IsValid => Errors.Count == 0;
}
