namespace Hazina.Store.EmbeddingStore;

/// <summary>
/// Interface for vector similarity search operations.
/// Implementations can use native vector search (pgvector, FAISS) or fallback to in-memory search.
/// </summary>
public interface IVectorSearchStore
{
    /// <summary>
    /// Searches for embeddings similar to the query embedding.
    /// </summary>
    /// <param name="queryEmbedding">The embedding vector to search for</param>
    /// <param name="topK">Maximum number of results to return</param>
    /// <param name="minSimilarity">Minimum cosine similarity threshold (0.0 to 1.0)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of scored embeddings ordered by similarity (highest first)</returns>
    Task<List<ScoredEmbedding>> SearchSimilarAsync(
        Embedding queryEmbedding,
        int topK = 10,
        double minSimilarity = 0.0,
        CancellationToken cancellationToken = default
    );
}
