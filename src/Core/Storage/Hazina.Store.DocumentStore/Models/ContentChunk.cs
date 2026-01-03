using System;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Represents a chunk of document content with hash for change detection.
/// Enables incremental embedding - only re-embed changed chunks.
/// </summary>
public class ContentChunk
{
    /// <summary>
    /// Unique identifier for this chunk.
    /// </summary>
    public string ChunkId { get; set; } = "";

    /// <summary>
    /// Parent document ID.
    /// </summary>
    public string DocumentId { get; set; } = "";

    /// <summary>
    /// Zero-based index of this chunk within the document.
    /// </summary>
    public int ChunkIndex { get; set; }

    /// <summary>
    /// The text content of this chunk.
    /// </summary>
    public string Content { get; set; } = "";

    /// <summary>
    /// Content hash for change detection (SHA256).
    /// </summary>
    public string ContentHash { get; set; } = "";

    /// <summary>
    /// When this chunk was created.
    /// </summary>
    public DateTime Created { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this chunk was last modified.
    /// </summary>
    public DateTime LastModified { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Whether this chunk has an embedding computed.
    /// </summary>
    public bool HasEmbedding { get; set; }

    /// <summary>
    /// The embedding model used (e.g., "openai", "local").
    /// </summary>
    public string? EmbeddingModel { get; set; }

    /// <summary>
    /// When the embedding was last computed.
    /// </summary>
    public DateTime? EmbeddingComputedAt { get; set; }

    /// <summary>
    /// Start character offset in original document.
    /// </summary>
    public int StartOffset { get; set; }

    /// <summary>
    /// End character offset in original document.
    /// </summary>
    public int EndOffset { get; set; }

    /// <summary>
    /// Compute content hash from the current content.
    /// </summary>
    public void ComputeHash()
    {
        ContentHash = ComputeContentHash(Content);
    }

    /// <summary>
    /// Check if content has changed by comparing hashes.
    /// </summary>
    public bool HasContentChanged(string newContent)
    {
        return ContentHash != ComputeContentHash(newContent);
    }

    /// <summary>
    /// Compute SHA256 hash of content.
    /// </summary>
    public static string ComputeContentHash(string content)
    {
        if (string.IsNullOrEmpty(content))
            return "";

        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Create a chunk from content.
    /// </summary>
    public static ContentChunk Create(string documentId, int index, string content, int startOffset, int endOffset)
    {
        var chunk = new ContentChunk
        {
            ChunkId = $"{documentId}:chunk:{index}",
            DocumentId = documentId,
            ChunkIndex = index,
            Content = content,
            StartOffset = startOffset,
            EndOffset = endOffset
        };
        chunk.ComputeHash();
        return chunk;
    }
}

/// <summary>
/// Status of a chunk embedding operation.
/// </summary>
public enum ChunkEmbeddingStatus
{
    /// <summary>
    /// Chunk is new and needs embedding.
    /// </summary>
    New,

    /// <summary>
    /// Chunk content has changed and needs re-embedding.
    /// </summary>
    Modified,

    /// <summary>
    /// Chunk is unchanged, cached embedding is valid.
    /// </summary>
    Cached,

    /// <summary>
    /// Chunk was removed from document.
    /// </summary>
    Deleted,

    /// <summary>
    /// Embedding is in progress.
    /// </summary>
    Processing,

    /// <summary>
    /// Embedding failed.
    /// </summary>
    Failed
}

/// <summary>
/// Result of comparing document chunks for incremental updates.
/// </summary>
public class ChunkDiff
{
    /// <summary>
    /// Chunks that are new and need embedding.
    /// </summary>
    public System.Collections.Generic.List<ContentChunk> NewChunks { get; set; } = new();

    /// <summary>
    /// Chunks that have changed and need re-embedding.
    /// </summary>
    public System.Collections.Generic.List<ContentChunk> ModifiedChunks { get; set; } = new();

    /// <summary>
    /// Chunk IDs that are unchanged (use cached embeddings).
    /// </summary>
    public System.Collections.Generic.List<string> UnchangedChunkIds { get; set; } = new();

    /// <summary>
    /// Chunk IDs that no longer exist (delete embeddings).
    /// </summary>
    public System.Collections.Generic.List<string> DeletedChunkIds { get; set; } = new();

    /// <summary>
    /// Total chunks that need embedding (new + modified).
    /// </summary>
    public int ChunksToEmbed => NewChunks.Count + ModifiedChunks.Count;

    /// <summary>
    /// Total chunks that can use cache.
    /// </summary>
    public int CachedChunks => UnchangedChunkIds.Count;

    /// <summary>
    /// Percentage of chunks that can use cache (0-100).
    /// </summary>
    public double CacheHitRate
    {
        get
        {
            var total = ChunksToEmbed + CachedChunks;
            return total > 0 ? (CachedChunks * 100.0) / total : 0;
        }
    }
}
