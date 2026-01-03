using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Service for incremental document embedding.
/// Only re-embeds chunks that have changed, reducing API costs.
/// </summary>
public interface IIncrementalEmbeddingService
{
    /// <summary>
    /// Compute which chunks need embedding vs can use cache.
    /// </summary>
    /// <param name="documentId">Document identifier</param>
    /// <param name="newChunks">New content chunks</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Diff showing what needs embedding</returns>
    Task<ChunkDiff> ComputeDiffAsync(
        string documentId,
        List<ContentChunk> newChunks,
        CancellationToken ct = default);

    /// <summary>
    /// Embed only the chunks that need it (new + modified).
    /// </summary>
    /// <param name="documentId">Document identifier</param>
    /// <param name="chunks">Chunks to potentially embed</param>
    /// <param name="forceReembed">If true, ignore cache and re-embed all</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Embedding result with statistics</returns>
    Task<IncrementalEmbeddingResult> EmbedIncrementallyAsync(
        string documentId,
        List<ContentChunk> chunks,
        bool forceReembed = false,
        CancellationToken ct = default);

    /// <summary>
    /// Get stored chunk hashes for a document.
    /// </summary>
    Task<Dictionary<string, string>> GetChunkHashesAsync(
        string documentId,
        CancellationToken ct = default);

    /// <summary>
    /// Store chunk embeddings and hashes.
    /// </summary>
    Task StoreChunkEmbeddingsAsync(
        string documentId,
        List<ChunkEmbeddingRecord> embeddings,
        CancellationToken ct = default);

    /// <summary>
    /// Delete embeddings for removed chunks.
    /// </summary>
    Task DeleteChunkEmbeddingsAsync(
        string documentId,
        List<string> chunkIds,
        CancellationToken ct = default);

    /// <summary>
    /// Get embedding statistics for a document.
    /// </summary>
    Task<EmbeddingStatistics> GetStatisticsAsync(
        string documentId,
        CancellationToken ct = default);
}

/// <summary>
/// Result of incremental embedding operation.
/// </summary>
public class IncrementalEmbeddingResult
{
    /// <summary>
    /// Document that was processed.
    /// </summary>
    public string DocumentId { get; set; } = "";

    /// <summary>
    /// Total chunks in document.
    /// </summary>
    public int TotalChunks { get; set; }

    /// <summary>
    /// Chunks that were newly embedded.
    /// </summary>
    public int NewlyEmbedded { get; set; }

    /// <summary>
    /// Chunks that were re-embedded due to changes.
    /// </summary>
    public int ReEmbedded { get; set; }

    /// <summary>
    /// Chunks that used cached embeddings.
    /// </summary>
    public int CachedUsed { get; set; }

    /// <summary>
    /// Chunks that were deleted.
    /// </summary>
    public int Deleted { get; set; }

    /// <summary>
    /// Chunks that failed to embed.
    /// </summary>
    public int Failed { get; set; }

    /// <summary>
    /// Total API cost saved by using cache.
    /// </summary>
    public double EstimatedCostSaved { get; set; }

    /// <summary>
    /// Time taken for embedding operation.
    /// </summary>
    public System.TimeSpan Duration { get; set; }

    /// <summary>
    /// Whether operation was successful.
    /// </summary>
    public bool Success => Failed == 0;

    /// <summary>
    /// Cache hit rate as percentage.
    /// </summary>
    public double CacheHitRate => TotalChunks > 0
        ? (CachedUsed * 100.0) / TotalChunks
        : 0;

    /// <summary>
    /// Error messages if any failures occurred.
    /// </summary>
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Record of a chunk's embedding.
/// </summary>
public class ChunkEmbeddingRecord
{
    /// <summary>
    /// Chunk identifier.
    /// </summary>
    public string ChunkId { get; set; } = "";

    /// <summary>
    /// Content hash at time of embedding.
    /// </summary>
    public string ContentHash { get; set; } = "";

    /// <summary>
    /// The embedding vector.
    /// </summary>
    public float[] Embedding { get; set; } = System.Array.Empty<float>();

    /// <summary>
    /// Model used for embedding.
    /// </summary>
    public string Model { get; set; } = "";

    /// <summary>
    /// When embedding was computed.
    /// </summary>
    public System.DateTime ComputedAt { get; set; } = System.DateTime.UtcNow;

    /// <summary>
    /// Token count of the content.
    /// </summary>
    public int TokenCount { get; set; }
}

/// <summary>
/// Embedding statistics for a document.
/// </summary>
public class EmbeddingStatistics
{
    /// <summary>
    /// Total chunks in document.
    /// </summary>
    public int TotalChunks { get; set; }

    /// <summary>
    /// Chunks with embeddings.
    /// </summary>
    public int EmbeddedChunks { get; set; }

    /// <summary>
    /// Chunks pending embedding.
    /// </summary>
    public int PendingChunks { get; set; }

    /// <summary>
    /// Last embedding update time.
    /// </summary>
    public System.DateTime? LastUpdated { get; set; }

    /// <summary>
    /// Embedding model used.
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// Total tokens embedded.
    /// </summary>
    public int TotalTokens { get; set; }

    /// <summary>
    /// Estimated embedding cost.
    /// </summary>
    public double EstimatedCost { get; set; }
}
