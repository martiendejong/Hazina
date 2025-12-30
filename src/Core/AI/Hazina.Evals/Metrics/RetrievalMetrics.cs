using Hazina.AI.RAG.Interfaces;
using Hazina.Evals.Models;

namespace Hazina.Evals.Metrics;

/// <summary>
/// Calculates standard retrieval evaluation metrics.
/// </summary>
public static class RetrievalMetrics
{
    /// <summary>
    /// Calculate Hit@K: Did we retrieve at least one relevant document in top K?
    /// </summary>
    public static double CalculateHitAtK(
        List<IRetrievalCandidate> retrieved,
        HashSet<string> relevantChunkIds,
        int k)
    {
        var topK = retrieved.Take(k);
        var hasHit = topK.Any(c => relevantChunkIds.Contains(c.ChunkId));
        return hasHit ? 1.0 : 0.0;
    }

    /// <summary>
    /// Calculate Mean Reciprocal Rank: 1 / (rank of first relevant document)
    /// </summary>
    public static double CalculateMRR(
        List<IRetrievalCandidate> retrieved,
        HashSet<string> relevantChunkIds)
    {
        for (int i = 0; i < retrieved.Count; i++)
        {
            if (relevantChunkIds.Contains(retrieved[i].ChunkId))
            {
                return 1.0 / (i + 1);
            }
        }
        return 0.0;
    }

    /// <summary>
    /// Calculate Normalized Discounted Cumulative Gain
    /// </summary>
    public static double CalculateNDCG(
        List<IRetrievalCandidate> retrieved,
        Dictionary<string, int> relevanceJudgments,
        int k)
    {
        var topK = retrieved.Take(k).ToList();

        var dcg = CalculateDCG(topK, relevanceJudgments);

        var ideal = relevanceJudgments
            .OrderByDescending(kv => kv.Value)
            .Take(k)
            .ToList();

        var idealDCG = 0.0;
        for (int i = 0; i < ideal.Count; i++)
        {
            var gain = ideal[i].Value;
            var discount = Math.Log2(i + 2);
            idealDCG += gain / discount;
        }

        return idealDCG > 0 ? dcg / idealDCG : 0.0;
    }

    private static double CalculateDCG(
        List<IRetrievalCandidate> retrieved,
        Dictionary<string, int> relevanceJudgments)
    {
        var dcg = 0.0;
        for (int i = 0; i < retrieved.Count; i++)
        {
            var chunkId = retrieved[i].ChunkId;
            var gain = relevanceJudgments.TryGetValue(chunkId, out var rel) ? rel : 0;
            var discount = Math.Log2(i + 2);
            dcg += gain / discount;
        }
        return dcg;
    }

    /// <summary>
    /// Calculate Precision@K: Fraction of top K that are relevant
    /// </summary>
    public static double CalculatePrecisionAtK(
        List<IRetrievalCandidate> retrieved,
        HashSet<string> relevantChunkIds,
        int k)
    {
        var topK = retrieved.Take(k).ToList();
        var relevantCount = topK.Count(c => relevantChunkIds.Contains(c.ChunkId));
        return topK.Count > 0 ? (double)relevantCount / topK.Count : 0.0;
    }

    /// <summary>
    /// Calculate Recall@K: Fraction of relevant documents retrieved in top K
    /// </summary>
    public static double CalculateRecallAtK(
        List<IRetrievalCandidate> retrieved,
        HashSet<string> relevantChunkIds,
        int k)
    {
        var topK = retrieved.Take(k).ToList();
        var retrievedRelevantCount = topK.Count(c => relevantChunkIds.Contains(c.ChunkId));
        return relevantChunkIds.Count > 0
            ? (double)retrievedRelevantCount / relevantChunkIds.Count
            : 0.0;
    }

    /// <summary>
    /// Calculate all standard metrics for a retrieval result
    /// </summary>
    public static EvalMetrics CalculateAllMetrics(
        List<IRetrievalCandidate> retrieved,
        EvalCase evalCase,
        int k = 5)
    {
        var metrics = new EvalMetrics();

        if (evalCase.ExpectedChunkIds != null && evalCase.ExpectedChunkIds.Count > 0)
        {
            var relevantSet = new HashSet<string>(evalCase.ExpectedChunkIds);

            metrics.HitAtK = CalculateHitAtK(retrieved, relevantSet, k);
            metrics.MRR = CalculateMRR(retrieved, relevantSet);
            metrics.PrecisionAtK = CalculatePrecisionAtK(retrieved, relevantSet, k);
            metrics.RecallAtK = CalculateRecallAtK(retrieved, relevantSet, k);
        }

        if (evalCase.RelevanceJudgments != null && evalCase.RelevanceJudgments.Count > 0)
        {
            metrics.NDCG = CalculateNDCG(retrieved, evalCase.RelevanceJudgments, k);
        }

        return metrics;
    }
}
