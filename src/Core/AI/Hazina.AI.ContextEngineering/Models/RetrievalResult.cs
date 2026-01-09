namespace Hazina.AI.ContextEngineering.Models;

/// <summary>
/// Result from a context retriever
/// </summary>
public class RetrievalResult
{
    /// <summary>
    /// Unique identifier for the result
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Content of the retrieved item
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Relevance score (0.0-1.0)
    /// </summary>
    public double Score { get; set; }

    /// <summary>
    /// Source of the result (e.g., "semantic", "facts", "metadata", "lookup")
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// Type of content (e.g., "chunk", "fact", "document", "metadata")
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Additional metadata
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Tags associated with this result
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Timestamp of when this item was created/indexed
    /// </summary>
    public DateTime? Timestamp { get; set; }
}
