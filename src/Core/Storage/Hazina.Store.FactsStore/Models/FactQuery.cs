namespace Hazina.Store.FactsStore.Models;

/// <summary>
/// Query parameters for retrieving facts from the store
/// </summary>
public class FactQuery
{
    /// <summary>
    /// Optional semantic query text (requires embeddings)
    /// </summary>
    public string? QueryText { get; set; }

    /// <summary>
    /// Optional query embedding (for semantic search)
    /// </summary>
    public float[]? QueryEmbedding { get; set; }

    /// <summary>
    /// Filter by fact type(s)
    /// </summary>
    public List<string>? Types { get; set; }

    /// <summary>
    /// Filter by tags (AND logic - fact must have all tags)
    /// </summary>
    public List<string>? Tags { get; set; }

    /// <summary>
    /// Filter by metadata (AND logic - fact must match all key-value pairs)
    /// </summary>
    public Dictionary<string, string>? Metadata { get; set; }

    /// <summary>
    /// Filter by fact IDs
    /// </summary>
    public List<string>? Ids { get; set; }

    /// <summary>
    /// Maximum number of results to return
    /// </summary>
    public int TopK { get; set; } = 5;

    /// <summary>
    /// Minimum similarity threshold (for semantic search)
    /// </summary>
    public double MinSimilarity { get; set; } = 0.0;

    /// <summary>
    /// Whether to include embeddings in results
    /// (set to false to reduce payload size)
    /// </summary>
    public bool IncludeEmbeddings { get; set; } = false;
}
