using Hazina.AI.RAG.Interfaces;

namespace Hazina.AI.RAG.Retrieval;

/// <summary>
/// No-op reranker that simply returns candidates unchanged.
/// Useful for baseline comparisons and when reranking is not needed.
/// </summary>
public class NoOpReranker : IReranker
{
    public Task<List<IRetrievalCandidate>> RerankAsync(
        string query,
        List<IRetrievalCandidate> candidates,
        int topN = 5,
        CancellationToken cancellationToken = default)
    {
        var topCandidates = candidates.Take(topN).ToList();

        foreach (var candidate in topCandidates)
        {
            if (candidate is RetrievalCandidate rc)
            {
                rc.RerankScore = rc.OriginalScore;
            }
        }

        return Task.FromResult(topCandidates);
    }
}
