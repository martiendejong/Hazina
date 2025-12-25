namespace Hazina.Store.EmbeddingStore;

/// <summary>
/// Interface for embedding stores that support full enumeration.
/// Only implement this for small-scale stores (file-based, in-memory).
/// Large-scale stores (databases with millions of embeddings) should NOT implement this.
/// </summary>
public interface IEnumerableEmbeddingStore : IEmbeddingStore
{
    /// <summary>
    /// Gets all embeddings from the store.
    /// WARNING: Use with caution on large datasets - may cause memory issues.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Async enumerable of all embeddings</returns>
    IAsyncEnumerable<EmbeddingInfo> GetAllAsync(CancellationToken cancellationToken = default);
}
