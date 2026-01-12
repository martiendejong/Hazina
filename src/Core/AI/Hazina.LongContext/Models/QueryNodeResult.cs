namespace Hazina.LongContext.Models;

/// <summary>
/// Result of executing a query node
/// </summary>
public class QueryNodeResult
{
    /// <summary>
    /// ID of the node that produced this result
    /// </summary>
    public Guid NodeId { get; init; }

    /// <summary>
    /// The answer or output produced by this node
    /// </summary>
    public string Answer { get; init; } = string.Empty;

    /// <summary>
    /// Shards that were used to produce this answer
    /// </summary>
    public IReadOnlyList<ContextShard> UsedShards { get; init; } = Array.Empty<ContextShard>();

    /// <summary>
    /// Number of tokens consumed by this node's execution
    /// </summary>
    public int TokensUsed { get; init; }

    /// <summary>
    /// Time taken to execute this node
    /// </summary>
    public TimeSpan ExecutionTime { get; init; }

    /// <summary>
    /// Whether this node's execution was successful
    /// </summary>
    public bool Success { get; init; } = true;

    /// <summary>
    /// Error message if execution failed
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Results from child nodes (for aggregation nodes)
    /// </summary>
    public IReadOnlyList<QueryNodeResult> ChildResults { get; init; } = Array.Empty<QueryNodeResult>();
}
