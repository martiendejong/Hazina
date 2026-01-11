namespace Hazina.Brain.Domain;

/// <summary>
/// Represents a distilled long-term memory fact.
/// Facts are extracted from episodes via LLM and represent stable knowledge that should persist.
/// Examples: user preferences, domain knowledge, entity information
/// </summary>
public sealed class MemoryFact
{
    /// <summary>
    /// Unique identifier for this fact
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Store identifier - partitions facts by store/project
    /// </summary>
    public required string StoreId { get; set; }

    /// <summary>
    /// Optional agent identifier - allows per-agent fact isolation
    /// </summary>
    public string? AgentId { get; set; }

    /// <summary>
    /// Optional user identifier - allows per-user fact isolation
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// The factual statement (e.g., "user prefers Dutch responses")
    /// </summary>
    public required string Text { get; set; }

    /// <summary>
    /// Embedding vector for similarity search
    /// Generated from Text
    /// </summary>
    public required float[] Embedding { get; set; }

    /// <summary>
    /// LLM's confidence in this fact (0.0-1.0)
    /// Set during distillation based on LLM's assessment
    /// </summary>
    public double Confidence { get; set; } = 0.8;

    /// <summary>
    /// Usage-based weight - increases when fact is retrieved, decays over time
    /// Higher weight = more important/relevant
    /// Default: 1.0 (freshly created)
    /// </summary>
    public double Weight { get; set; } = 1.0;

    /// <summary>
    /// When this fact was created/distilled
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Last time this fact was retrieved/accessed
    /// Used for weighting and decay calculation
    /// </summary>
    public DateTimeOffset LastAccessedAt { get; set; } = DateTimeOffset.UtcNow;
}
