namespace Hazina.AI.RAG.Interfaces;

/// <summary>
/// Abstraction for retrieving candidate chunks from a document store or vector database.
/// Decouples retrieval from reranking and generation.
/// </summary>
public interface IRetriever
{
    /// <summary>
    /// Retrieve top-K candidates for a given query
    /// </summary>
    /// <param name="query">User query text</param>
    /// <param name="topK">Number of candidates to retrieve</param>
    /// <param name="minSimilarity">Minimum similarity threshold (optional)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of retrieval candidates ordered by similarity</returns>
    Task<List<IRetrievalCandidate>> GetCandidatesAsync(
        string query,
        int topK = 20,
        double minSimilarity = 0.0,
        CancellationToken cancellationToken = default
    );
}
