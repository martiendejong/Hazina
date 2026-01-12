namespace Hazina.LongContext.Models;

/// <summary>
/// Unique identifier for a context shard.
/// Shards are independent chunks of retrieved context that can be processed separately.
/// </summary>
public record ContextShardId(string Value)
{
    public static ContextShardId New() => new(Guid.NewGuid().ToString());
    public static ContextShardId From(string id) => new(id);
    public override string ToString() => Value;
}
