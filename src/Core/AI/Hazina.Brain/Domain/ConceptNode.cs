namespace Hazina.Brain.Domain;

/// <summary>
/// Represents a concept node in the knowledge graph.
/// Concepts can represent entities, topics, or abstract ideas mentioned in conversations.
/// Optional feature for advanced knowledge graph reasoning.
/// </summary>
public sealed class ConceptNode
{
    /// <summary>
    /// Unique identifier for this concept node
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Store identifier - partitions concepts by store/project
    /// </summary>
    public required string StoreId { get; set; }

    /// <summary>
    /// Label or name of the concept (e.g., "Stripe", "Project-123", "User Preferences")
    /// </summary>
    public required string Label { get; set; }

    /// <summary>
    /// Embedding vector for similarity search across concepts
    /// Generated from Label + optional description
    /// </summary>
    public required float[] Embedding { get; set; }

    /// <summary>
    /// Optional type/category of this concept (e.g., "Entity", "Topic", "Preference")
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Optional metadata as JSON
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>
    /// When this concept was first identified
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Last time this concept was referenced/accessed
    /// </summary>
    public DateTimeOffset LastAccessedAt { get; set; } = DateTimeOffset.UtcNow;
}
