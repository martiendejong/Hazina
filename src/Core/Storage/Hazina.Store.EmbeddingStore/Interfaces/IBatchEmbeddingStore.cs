namespace Hazina.Store.EmbeddingStore;

/// <summary>
/// Interface for embedding stores that support efficient batch operations.
/// Useful for bulk ingestion and retrieval scenarios.
/// </summary>
public interface IBatchEmbeddingStore : IEmbeddingStore
{
    /// <summary>
    /// Stores multiple embeddings in a single batch operation.
    /// </summary>
    /// <param name="batch">Collection of key, embedding, and checksum tuples</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of embeddings successfully stored</returns>
    Task<int> StoreBatchAsync(
        IEnumerable<(string key, Embedding embedding, string checksum)> batch,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Retrieves multiple embeddings in a single batch operation.
    /// </summary>
    /// <param name="keys">Collection of keys to retrieve</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of found embeddings (may be fewer than requested keys)</returns>
    Task<List<EmbeddingInfo>> GetBatchAsync(
        IEnumerable<string> keys,
        CancellationToken cancellationToken = default
    );
}
