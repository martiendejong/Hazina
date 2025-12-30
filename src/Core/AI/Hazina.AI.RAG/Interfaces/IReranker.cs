namespace Hazina.AI.RAG.Interfaces;

/// <summary>
/// Abstraction for reranking retrieval candidates.
/// Rerankers can use different strategies (LLM-based, cross-encoder, hybrid, or no-op).
/// </summary>
public interface IReranker
{
    /// <summary>
    /// Rerank candidates and return top-N results
    /// </summary>
    /// <param name="query">User query text</param>
    /// <param name="candidates">Candidates to rerank</param>
    /// <param name="topN">Number of top results to return after reranking</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Reranked candidates with RerankScore populated</returns>
    Task<List<IRetrievalCandidate>> RerankAsync(
        string query,
        List<IRetrievalCandidate> candidates,
        int topN = 5,
        CancellationToken cancellationToken = default
    );
}
