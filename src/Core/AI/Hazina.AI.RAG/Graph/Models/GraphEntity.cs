namespace Hazina.AI.RAG.Graph.Models;

/// <summary>
/// Represents an entity node in the knowledge graph.
/// </summary>
public class GraphEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "Unknown";
    public string? Description { get; set; }
    public Dictionary<string, object> Properties { get; set; } = new();
    public DateTime Created { get; set; } = DateTime.UtcNow;
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    public float[]? Embedding { get; set; }
    public List<string> SourceDocuments { get; set; } = new();
    public double Confidence { get; set; } = 1.0;
    public int MentionCount { get; set; } = 1;
    public List<string> Aliases { get; set; } = new();
    public Dictionary<string, string>? ExternalIds { get; set; }
}
