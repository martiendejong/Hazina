using Hazina.AI.ContextEngineering.Interfaces;
using Hazina.AI.ContextEngineering.Models;
using Microsoft.Extensions.Logging;

namespace Hazina.AI.ContextEngineering.Fusion;

/// <summary>
/// Engine that combines results from multiple retrievers using configurable fusion strategies
/// </summary>
public class FusionEngine
{
    private readonly ILogger<FusionEngine>? _logger;

    /// <summary>
    /// Creates a new fusion engine
    /// </summary>
    /// <param name="logger">Optional logger</param>
    public FusionEngine(ILogger<FusionEngine>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Fuse results from multiple retrievers
    /// </summary>
    /// <param name="retrieverResults">Results from each retriever (retriever name -> results)</param>
    /// <param name="weights">Weights for each retriever (retriever name -> weight)</param>
    /// <param name="strategy">Fusion strategy to use</param>
    /// <param name="topK">Final number of results to return</param>
    /// <returns>Fused and ranked results</returns>
    public List<RetrievalResult> Fuse(
        Dictionary<string, List<RetrievalResult>> retrieverResults,
        Dictionary<string, double> weights,
        FusionStrategy strategy = FusionStrategy.WeightedSum,
        int topK = 10)
    {
        _logger?.LogDebug(
            "Fusing results from {Count} retrievers using {Strategy}",
            retrieverResults.Count,
            strategy
        );

        // Deduplicate results first (same ID from different sources)
        var allResults = retrieverResults
            .SelectMany(kvp => kvp.Value)
            .GroupBy(r => r.Id)
            .Select(g => MergeResults(g.ToList(), weights, strategy))
            .ToList();

        // Apply fusion strategy
        var scored = strategy switch
        {
            FusionStrategy.WeightedSum => ApplyWeightedSum(allResults, weights),
            FusionStrategy.ReciprocalRankFusion => ApplyReciprocalRankFusion(retrieverResults, weights),
            FusionStrategy.MaxScore => ApplyMaxScore(allResults),
            _ => allResults
        };

        // Sort by final score and take topK
        var final = scored
            .OrderByDescending(r => r.Score)
            .Take(topK)
            .ToList();

        _logger?.LogDebug("Fusion produced {Count} final results", final.Count);

        return final;
    }

    private RetrievalResult MergeResults(
        List<RetrievalResult> duplicates,
        Dictionary<string, double> weights,
        FusionStrategy strategy)
    {
        // Take the first one as base
        var merged = duplicates.First();

        if (duplicates.Count == 1)
            return merged;

        // Combine scores based on strategy
        merged.Score = strategy switch
        {
            FusionStrategy.WeightedSum => duplicates
                .Sum(r => r.Score * weights.GetValueOrDefault(r.Source, 1.0)),
            FusionStrategy.MaxScore => duplicates.Max(r => r.Score),
            _ => duplicates.Average(r => r.Score)
        };

        // Combine sources
        merged.Source = string.Join("+", duplicates.Select(r => r.Source).Distinct());

        // Merge tags (union)
        merged.Tags = duplicates
            .SelectMany(r => r.Tags)
            .Distinct()
            .ToList();

        return merged;
    }

    private List<RetrievalResult> ApplyWeightedSum(
        List<RetrievalResult> results,
        Dictionary<string, double> weights)
    {
        foreach (var result in results)
        {
            var sources = result.Source.Split('+');
            var weight = sources.Average(s => weights.GetValueOrDefault(s, 1.0));
            result.Score *= weight;
        }

        return results;
    }

    private List<RetrievalResult> ApplyReciprocalRankFusion(
        Dictionary<string, List<RetrievalResult>> retrieverResults,
        Dictionary<string, double> weights,
        int k = 60)
    {
        var rrfScores = new Dictionary<string, double>();

        foreach (var (retrieverName, results) in retrieverResults)
        {
            var weight = weights.GetValueOrDefault(retrieverName, 1.0);

            for (int rank = 0; rank < results.Count; rank++)
            {
                var id = results[rank].Id;
                var rrfScore = weight / (k + rank + 1);

                if (rrfScores.ContainsKey(id))
                    rrfScores[id] += rrfScore;
                else
                    rrfScores[id] = rrfScore;
            }
        }

        // Update scores
        var allResults = retrieverResults
            .SelectMany(kvp => kvp.Value)
            .GroupBy(r => r.Id)
            .Select(g => g.First())
            .ToList();

        foreach (var result in allResults)
        {
            if (rrfScores.TryGetValue(result.Id, out var score))
            {
                result.Score = score;
            }
        }

        return allResults;
    }

    private List<RetrievalResult> ApplyMaxScore(List<RetrievalResult> results)
    {
        // Scores are already set to max during merge
        return results;
    }
}

/// <summary>
/// Fusion strategies for combining retriever results
/// </summary>
public enum FusionStrategy
{
    /// <summary>
    /// Weighted sum of scores from each retriever
    /// </summary>
    WeightedSum,

    /// <summary>
    /// Reciprocal Rank Fusion (RRF) - combines ranks rather than scores
    /// </summary>
    ReciprocalRankFusion,

    /// <summary>
    /// Take maximum score across all retrievers
    /// </summary>
    MaxScore
}
