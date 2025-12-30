namespace Hazina.AI.RAG.Interfaces;

/// <summary>
/// Orchestrates the full retrieval pipeline: retrieve → rerank → return
/// </summary>
public interface IRetrievalPipeline
{
    /// <summary>
    /// Execute the full retrieval pipeline
    /// </summary>
    /// <param name="query">User query</param>
    /// <param name="retrievalTopK">Number of candidates to retrieve initially</param>
    /// <param name="rerankTopN">Number of top results to return after reranking</param>
    /// <param name="minSimilarity">Minimum similarity threshold for retrieval</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Final reranked candidates</returns>
    Task<List<IRetrievalCandidate>> RetrieveAsync(
        string query,
        int retrievalTopK = 20,
        int rerankTopN = 5,
        double minSimilarity = 0.0,
        CancellationToken cancellationToken = default
    );
}
