namespace Hazina.AI.RAG.Graph.Models;

/// <summary>
/// Represents a directed relationship between two entities.
/// </summary>
public class GraphRelationship
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SourceEntityId { get; set; } = string.Empty;
    public string TargetEntityId { get; set; } = string.Empty;
    public string RelationType { get; set; } = "RELATED_TO";
    public string? Description { get; set; }
    public Dictionary<string, object> Properties { get; set; } = new();
    public double Confidence { get; set; } = 1.0;
    public string? SourceDocumentId { get; set; }
    public string? SourceText { get; set; }
    public DateTime Created { get; set; } = DateTime.UtcNow;
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    public double Weight { get; set; } = 1.0;
    public bool IsBidirectional { get; set; } = false;
    public TemporalScope? Temporal { get; set; }
}

public class TemporalScope
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsActive => (StartDate == null || StartDate <= DateTime.UtcNow) &&
                            (EndDate == null || EndDate >= DateTime.UtcNow);
}
