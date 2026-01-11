namespace Hazina.LongContext.Models;

/// <summary>
/// Represents a node in the recursive query tree.
/// Each node represents either a question to answer, content to retrieve, or results to aggregate.
/// </summary>
public class QueryNode
{
    /// <summary>
    /// Unique identifier for this node
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Type of this query node
    /// </summary>
    public QueryNodeType Type { get; init; }

    /// <summary>
    /// The prompt/question for this node
    /// </summary>
    public string Prompt { get; init; } = string.Empty;

    /// <summary>
    /// Target shards for retrieval nodes (optional)
    /// </summary>
    public IReadOnlyList<ContextShardId> TargetShards { get; init; } = Array.Empty<ContextShardId>();

    /// <summary>
    /// Child nodes in the query tree
    /// </summary>
    public IReadOnlyList<QueryNode> Children { get; init; } = Array.Empty<QueryNode>();

    /// <summary>
    /// Current execution status
    /// </summary>
    public QueryNodeStatus Status { get; set; } = QueryNodeStatus.Pending;

    /// <summary>
    /// Depth of this node in the tree (root = 0)
    /// </summary>
    public int Depth { get; init; }

    /// <summary>
    /// Optional parent node ID for navigation
    /// </summary>
    public Guid? ParentId { get; init; }

    /// <summary>
    /// Metadata for this node (can store planning decisions, etc.)
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new();
}
