using Hazina.AI.RAG.Interfaces;

namespace Hazina.AI.RAG.Retrieval;

/// <summary>
/// Default implementation of IRetrievalPipeline.
/// Orchestrates the retrieval → rerank → return flow with configurable components.
/// </summary>
public class RetrievalPipeline : IRetrievalPipeline
{
    private readonly IRetriever _retriever;
    private readonly IReranker _reranker;

    public RetrievalPipeline(IRetriever retriever, IReranker reranker)
    {
        _retriever = retriever ?? throw new ArgumentNullException(nameof(retriever));
        _reranker = reranker ?? throw new ArgumentNullException(nameof(reranker));
    }

    public async Task<List<IRetrievalCandidate>> RetrieveAsync(
        string query,
        int retrievalTopK = 20,
        int rerankTopN = 5,
        double minSimilarity = 0.0,
        CancellationToken cancellationToken = default)
    {
        var candidates = await _retriever.GetCandidatesAsync(
            query,
            retrievalTopK,
            minSimilarity,
            cancellationToken
        );

        if (candidates.Count == 0)
            return candidates;

        var reranked = await _reranker.RerankAsync(
            query,
            candidates,
            rerankTopN,
            cancellationToken
        );

        return reranked;
    }
}
