namespace Hazina.Brain.Domain;

/// <summary>
/// Represents a relationship between two concept nodes in the knowledge graph.
/// Edges capture semantic relationships like "uses", "belongs-to", "related-to".
/// Optional feature for advanced knowledge graph reasoning.
/// </summary>
public sealed class ConceptEdge
{
    /// <summary>
    /// Unique identifier for this edge
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Source concept node ID
    /// </summary>
    public Guid SourceId { get; set; }

    /// <summary>
    /// Target concept node ID
    /// </summary>
    public Guid TargetId { get; set; }

    /// <summary>
    /// Type of relationship (e.g., "uses", "prefers", "belongs-to", "related-to")
    /// </summary>
    public required string Relation { get; set; }

    /// <summary>
    /// Strength of this relationship (0.0-1.0)
    /// Can be increased by repeated observations of this relationship
    /// </summary>
    public double Weight { get; set; } = 1.0;

    /// <summary>
    /// When this relationship was first identified
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Last time this relationship was observed/accessed
    /// </summary>
    public DateTimeOffset LastAccessedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation properties for EF Core
    public ConceptNode? Source { get; set; }
    public ConceptNode? Target { get; set; }
}
