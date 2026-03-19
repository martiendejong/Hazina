namespace Hazina.Store.EmbeddingStore;

/// <summary>
/// Versioned wrapper for embedding file format with schema evolution support.
/// </summary>
public class EmbeddingFileFormat
{
    /// <summary>
    /// Schema version. Current version is 1.
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// List of embeddings in this file.
    /// </summary>
    public List<EmbeddingInfo> Embeddings { get; set; } = new();

    /// <summary>
    /// Optional metadata for diagnostics.
    /// </summary>
    public EmbeddingFileMetadata? Metadata { get; set; }
}

/// <summary>
/// Metadata for embedding files to aid in diagnostics and integrity verification.
/// </summary>
public class EmbeddingFileMetadata
{
    /// <summary>
    /// When this file was last saved.
    /// </summary>
    public DateTime? LastModified { get; set; }

    /// <summary>
    /// Total number of embeddings.
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Optional checksum for integrity verification.
    /// </summary>
    public string? Checksum { get; set; }
}
