using Hazina.AI.CognitivePipeline.Core;
using Microsoft.Extensions.Logging;

namespace Hazina.AI.CognitivePipeline.Stages;

/// <summary>
/// Stage 4: Reflective (Frontal).
/// Evaluates logical consistency of filtered facts, cross-validates claims,
/// and assigns confidence scores to each fact.
/// </summary>
public class ReflectiveStage : ISCPStage
{
    private readonly ILogger<ReflectiveStage> _logger;

    public SCPStageType StageType => SCPStageType.Reflective;
    public int Order => 4;
    public string Name => "Reflective";

    public ReflectiveStage(ILogger<ReflectiveStage> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<SCPStageResult> ExecuteAsync(SCPContext context, CancellationToken cancellationToken = default)
    {
        var result = new SCPStageResult { StageType = StageType };

        var confidenceScores = new Dictionary<string, double>();
        var inconsistencies = new List<string>();

        foreach (var entry in context.FilteredData)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var confidence = CalculateFactConfidence(entry.Key, entry.Value, context);

            confidenceScores[entry.Key] = confidence;

            if (confidence < context.Config.MinConfidence)
            {
                inconsistencies.Add($"Low confidence for '{entry.Key}': {confidence:F2} (threshold: {context.Config.MinConfidence:F2})");
            }
        }

        // Store confidence scores in metadata for Decision stage
        context.Metadata["FactConfidenceScores"] = confidenceScores;

        var avgConfidence = confidenceScores.Count > 0
            ? confidenceScores.Values.Average()
            : 0;

        var lowConfidenceCount = confidenceScores.Count(kv => kv.Value < context.Config.MinConfidence);

        result.Confidence = avgConfidence;
        result.Findings.Add($"Evaluated {confidenceScores.Count} facts: avg confidence {avgConfidence:F2}, {lowConfidenceCount} below threshold");
        result.Warnings.AddRange(inconsistencies);
        result.Data["AverageConfidence"] = avgConfidence;
        result.Data["LowConfidenceCount"] = lowConfidenceCount;
        result.Data["FactCount"] = confidenceScores.Count;
        result.Success = true;

        _logger.LogDebug("Reflective: avg confidence {Confidence:F2}, {LowCount} below threshold", avgConfidence, lowConfidenceCount);

        return Task.FromResult(result);
    }

    private static double CalculateFactConfidence(string key, object value, SCPContext context)
    {
        var confidence = 0.5; // Base confidence

        // Boost if confirmed by GroundTruth (partial match)
        if (context.GroundTruthHits.TryGetValue(key, out var hit) && hit.Found)
        {
            confidence += 0.3 * hit.MatchQuality;
        }

        // Boost if fact has related facts in the same cluster (corroboration)
        if (context.Metadata.TryGetValue("OrganizedClusters", out var clustersObj)
            && clustersObj is Dictionary<string, List<KeyValuePair<string, object>>> clusters)
        {
            var cluster = key.Contains('-') ? key[..key.IndexOf('-')] : key;
            if (clusters.TryGetValue(cluster, out var clusterFacts) && clusterFacts.Count > 1)
            {
                // More corroborating facts in the cluster → higher confidence
                confidence += Math.Min(0.2, 0.05 * (clusterFacts.Count - 1));
            }
        }

        // Penalize if value is empty or very short (likely incomplete)
        var valueStr = value?.ToString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(valueStr))
        {
            confidence -= 0.3;
        }
        else if (valueStr.Length < 3)
        {
            confidence -= 0.1;
        }

        return Math.Clamp(confidence, 0.0, 1.0);
    }
}
