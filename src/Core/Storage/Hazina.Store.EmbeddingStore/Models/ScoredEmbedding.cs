namespace Hazina.Store.EmbeddingStore;

/// <summary>
/// Represents an embedding with its similarity score from a vector search operation.
/// </summary>
public class ScoredEmbedding
{
    /// <summary>
    /// The embedding information (key, checksum, vector data).
    /// </summary>
    public required EmbeddingInfo Info { get; init; }

    /// <summary>
    /// Cosine similarity score (0.0 to 1.0, where 1.0 is identical).
    /// </summary>
    public required double Similarity { get; init; }

    /// <summary>
    /// Optional parent document key for chunk-to-document mapping.
    /// </summary>
    public string? ParentDocumentKey { get; init; }

    /// <summary>
    /// Optional metadata for extensibility.
    /// </summary>
    public Dictionary<string, object>? Metadata { get; init; }
}
