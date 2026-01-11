namespace Hazina.AI.ContextEngineering.Models;

/// <summary>
/// Query parameters for context retrieval
/// </summary>
public class RetrievalQuery
{
    /// <summary>
    /// Query text
    /// </summary>
    public string QueryText { get; set; } = string.Empty;

    /// <summary>
    /// Optional query embedding (for semantic search)
    /// </summary>
    public float[]? QueryEmbedding { get; set; }

    /// <summary>
    /// Maximum number of results per retriever
    /// </summary>
    public int TopK { get; set; } = 5;

    /// <summary>
    /// Minimum similarity threshold
    /// </summary>
    public double MinSimilarity { get; set; } = 0.0;

    /// <summary>
    /// Optional metadata filters
    /// </summary>
    public Dictionary<string, string>? MetadataFilters { get; set; }

    /// <summary>
    /// Optional tags to filter by
    /// </summary>
    public List<string>? Tags { get; set; }

    /// <summary>
    /// Optional types to filter by (e.g., "fact", "document", "chunk")
    /// </summary>
    public List<string>? Types { get; set; }

    /// <summary>
    /// Optional specific IDs to retrieve (for direct lookup)
    /// </summary>
    public List<string>? Ids { get; set; }
}
