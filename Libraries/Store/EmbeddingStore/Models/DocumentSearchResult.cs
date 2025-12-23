namespace DevGPT.Store.EmbeddingStore;

/// <summary>
/// Represents a document search result with similarity score.
/// This is a cleaner alternative to RelevantEmbedding without callback dependencies.
/// </summary>
public class DocumentSearchResult
{
    /// <summary>
    /// Unique identifier for the document chunk.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// Cosine similarity score (0.0 to 1.0).
    /// </summary>
    public required double Similarity { get; init; }

    /// <summary>
    /// Name of the store this result came from (useful for multi-store searches).
    /// </summary>
    public string? StoreName { get; init; }

    /// <summary>
    /// Parent document key if this is a chunk.
    /// </summary>
    public string? ParentDocumentKey { get; init; }

    /// <summary>
    /// Checksum of the source text.
    /// </summary>
    public string? Checksum { get; init; }

    /// <summary>
    /// Optional metadata for extensibility.
    /// </summary>
    public Dictionary<string, object>? Metadata { get; init; }

    /// <summary>
    /// The actual text content (populated separately if needed).
    /// </summary>
    public string? Text { get; set; }
}
