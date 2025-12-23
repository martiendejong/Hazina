namespace DevGPT.Store.EmbeddingStore;

/// <summary>
/// Core interface for storing and retrieving embeddings.
/// Focused solely on CRUD operations without embedding generation or search logic.
/// </summary>
public interface IEmbeddingStore
{
    /// <summary>
    /// Stores an embedding with its associated key and checksum.
    /// </summary>
    /// <param name="key">Unique identifier for the embedding</param>
    /// <param name="embedding">The embedding vector to store</param>
    /// <param name="checksum">Checksum of the source text for cache invalidation</param>
    /// <returns>True if stored successfully, false otherwise</returns>
    Task<bool> StoreAsync(string key, Embedding embedding, string checksum);

    /// <summary>
    /// Retrieves an embedding by its key.
    /// </summary>
    /// <param name="key">Unique identifier for the embedding</param>
    /// <returns>EmbeddingInfo if found, null otherwise</returns>
    Task<EmbeddingInfo?> GetAsync(string key);

    /// <summary>
    /// Removes an embedding from the store.
    /// </summary>
    /// <param name="key">Unique identifier for the embedding to remove</param>
    /// <returns>True if removed successfully, false if not found</returns>
    Task<bool> RemoveAsync(string key);

    /// <summary>
    /// Checks if an embedding exists in the store.
    /// </summary>
    /// <param name="key">Unique identifier for the embedding</param>
    /// <returns>True if exists, false otherwise</returns>
    Task<bool> ExistsAsync(string key);
}
