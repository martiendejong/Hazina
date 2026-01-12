namespace Hazina.Brain.Domain;

/// <summary>
/// Represents a single episodic memory - a conversation turn with embeddings for retrieval.
/// Episodes capture the raw interaction between user and agent, enabling temporal reasoning and context recall.
/// </summary>
public sealed class MemoryEpisode
{
    /// <summary>
    /// Unique identifier for this episode
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Store identifier - partitions memories by store/project
    /// </summary>
    public required string StoreId { get; set; }

    /// <summary>
    /// Optional agent identifier - allows per-agent memory isolation
    /// </summary>
    public string? AgentId { get; set; }

    /// <summary>
    /// Optional user identifier - allows per-user memory isolation
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// User's input text (raw)
    /// </summary>
    public required string RawInput { get; set; }

    /// <summary>
    /// Agent's output text (raw)
    /// </summary>
    public required string RawOutput { get; set; }

    /// <summary>
    /// Optional LLM-generated summary of this episode (1-2 sentences)
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>
    /// Embedding vector for similarity search
    /// Generated from RawInput + RawOutput
    /// </summary>
    public required float[] Embedding { get; set; }

    /// <summary>
    /// Dynamic relevance score - decays over time, boosted by usage
    /// Default: 1.0 (freshly created)
    /// </summary>
    public float RelevanceScore { get; set; } = 1.0f;

    /// <summary>
    /// When this episode was created
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Last time this episode was retrieved/accessed
    /// Used for recency-based scoring and decay calculation
    /// </summary>
    public DateTimeOffset LastAccessedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Optional tags for filtering (e.g., "preference", "error", "success")
    /// </summary>
    public string[] Tags { get; set; } = Array.Empty<string>();
}
