namespace Hazina.LongContext.Models;

/// <summary>
/// Represents a shard of context retrieved from storage.
/// Shards are independent chunks that can be processed in parallel.
/// </summary>
public class ContextShard
{
    /// <summary>
    /// Unique identifier for this shard
    /// </summary>
    public ContextShardId Id { get; init; } = ContextShardId.New();

    /// <summary>
    /// The actual content of this shard
    /// </summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// Metadata associated with this shard (source, tags, timestamps, etc.)
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new();

    /// <summary>
    /// Relevance score (0.0 to 1.0) indicating how relevant this shard is to the query
    /// </summary>
    public double Relevance { get; init; }

    /// <summary>
    /// Optional: Which query node retrieved this shard
    /// </summary>
    public Guid? RetrievedByNodeId { get; init; }
}
