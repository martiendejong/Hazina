using Hazina.AI.CognitivePipeline.Core;
using Hazina.AI.CognitivePipeline.GroundTruth;
using Microsoft.Extensions.Logging;

namespace Hazina.AI.CognitivePipeline.Stages;

/// <summary>
/// Stage 6: Memory.
/// Promotes high-confidence validated facts to the GroundTruth store,
/// increments verification counts for already-known facts, and
/// records pipeline execution for future learning.
/// </summary>
public class MemoryStage : ISCPStage
{
    private readonly IGroundTruthStore _groundTruthStore;
    private readonly ILogger<MemoryStage> _logger;

    private const double PromotionConfidenceThreshold = 0.8;

    public SCPStageType StageType => SCPStageType.Memory;
    public int Order => 6;
    public string Name => "Memory";

    public MemoryStage(IGroundTruthStore groundTruthStore, ILogger<MemoryStage> logger)
    {
        _groundTruthStore = groundTruthStore ?? throw new ArgumentNullException(nameof(groundTruthStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SCPStageResult> ExecuteAsync(SCPContext context, CancellationToken cancellationToken = default)
    {
        var result = new SCPStageResult { StageType = StageType };
        var domain = context.Domain ?? "general";

        var promoted = 0;
        var verified = 0;

        // Get per-fact confidence scores from Reflective stage
        var factConfidences = context.Metadata.TryGetValue("FactConfidenceScores", out var scoresObj)
            && scoresObj is Dictionary<string, double> scores
                ? scores
                : new Dictionary<string, double>();

        foreach (var entry in context.FilteredData)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var factConfidence = factConfidences.TryGetValue(entry.Key, out var c) ? c : 0;

            // Only promote facts that meet the confidence threshold
            if (factConfidence < PromotionConfidenceThreshold)
                continue;

            var valueStr = entry.Value?.ToString() ?? string.Empty;

            // Check if this fact already exists in GroundTruth
            if (context.GroundTruthHits.TryGetValue(entry.Key, out var hit) && hit.Found)
            {
                // Increment verification count for existing fact
                await _groundTruthStore.IncrementVerificationAsync(domain, entry.Key, cancellationToken);
                verified++;
                _logger.LogDebug("Verified existing GroundTruth fact: {Key}", entry.Key);
            }
            else
            {
                // Promote new high-confidence fact to GroundTruth
                var fact = new GroundTruthFact
                {
                    Domain = domain,
                    Key = entry.Key,
                    Value = valueStr,
                    Confidence = factConfidence,
                    Source = $"Pipeline promotion (execution: {context.ExecutionId})",
                    VerificationCount = 1
                };

                await _groundTruthStore.AddFactAsync(fact, cancellationToken);
                promoted++;
                _logger.LogInformation("Promoted fact to GroundTruth: {Key} = {Value} (confidence: {Confidence:F2})", entry.Key, valueStr, factConfidence);
            }
        }

        result.Confidence = 1.0; // Memory stage always succeeds if it runs
        result.Findings.Add($"Memory: {promoted} facts promoted, {verified} existing facts re-verified");
        result.Data["PromotedCount"] = promoted;
        result.Data["VerifiedCount"] = verified;
        result.Data["PromotionThreshold"] = PromotionConfidenceThreshold;
        result.Success = true;

        _logger.LogInformation("Memory: {Promoted} promoted, {Verified} re-verified", promoted, verified);

        return result;
    }
}
