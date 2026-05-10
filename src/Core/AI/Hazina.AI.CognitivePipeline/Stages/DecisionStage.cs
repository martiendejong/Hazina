using Hazina.AI.CognitivePipeline.Core;
using Microsoft.Extensions.Logging;

namespace Hazina.AI.CognitivePipeline.Stages;

/// <summary>
/// Stage 5: Decision (Binding).
/// Synthesizes weighted confidence scores, accepts/rejects/flags individual claims,
/// and produces the final Findings list for the pipeline result.
/// </summary>
public class DecisionStage : ISCPStage
{
    private readonly ILogger<DecisionStage> _logger;

    public SCPStageType StageType => SCPStageType.Decision;
    public int Order => 5;
    public string Name => "Decision";

    public DecisionStage(ILogger<DecisionStage> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<SCPStageResult> ExecuteAsync(SCPContext context, CancellationToken cancellationToken = default)
    {
        var result = new SCPStageResult { StageType = StageType };

        // Get per-fact confidence scores from Reflective stage
        var factConfidences = context.Metadata.TryGetValue("FactConfidenceScores", out var scoresObj)
            && scoresObj is Dictionary<string, double> scores
                ? scores
                : new Dictionary<string, double>();

        var accepted = new List<string>();
        var rejected = new List<string>();
        var flagged = new List<string>();

        var acceptThreshold = context.Config.MinConfidence;
        var rejectThreshold = acceptThreshold * 0.5; // Below half the min confidence → reject

        foreach (var entry in context.FilteredData)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var factConfidence = factConfidences.TryGetValue(entry.Key, out var c) ? c : 0.5;
            var valueStr = entry.Value?.ToString() ?? string.Empty;

            if (factConfidence >= acceptThreshold)
            {
                accepted.Add($"[ACCEPT] {entry.Key}: {valueStr} (confidence: {factConfidence:F2})");
                context.Findings.Add($"{entry.Key}: {valueStr}");
            }
            else if (factConfidence < rejectThreshold)
            {
                rejected.Add($"[REJECT] {entry.Key}: {valueStr} (confidence: {factConfidence:F2})");
            }
            else
            {
                flagged.Add($"[FLAG] {entry.Key}: {valueStr} (confidence: {factConfidence:F2})");
                context.Warnings.Add($"Low-confidence claim flagged: {entry.Key} ({factConfidence:F2})");
            }
        }

        // Handle GroundTruth short-circuit: accept all cached facts directly
        if (context.GroundTruthShortCircuit)
        {
            foreach (var hit in context.GroundTruthHits.Where(h => h.Value.Found && h.Value.Fact != null))
            {
                var fact = hit.Value.Fact!;
                accepted.Add($"[ACCEPT/CACHED] {fact.Key}: {fact.Value} (cached confidence: {fact.Confidence:F2})");
                // Only add to Findings if not already present
                var finding = $"{fact.Key}: {fact.Value}";
                if (!context.Findings.Contains(finding))
                    context.Findings.Add(finding);
            }
        }

        // Calculate weighted synthesis confidence from stage results
        var synthesizedConfidence = CalculateWeightedConfidence(context);

        result.Confidence = synthesizedConfidence;
        result.Findings.Add($"Decision: {accepted.Count} accepted, {rejected.Count} rejected, {flagged.Count} flagged");
        result.Data["Accepted"] = accepted;
        result.Data["Rejected"] = rejected;
        result.Data["Flagged"] = flagged;
        result.Data["AcceptedCount"] = accepted.Count;
        result.Data["RejectedCount"] = rejected.Count;
        result.Data["FlaggedCount"] = flagged.Count;
        result.Success = true;

        _logger.LogInformation(
            "Decision: {Accepted} accepted, {Rejected} rejected, {Flagged} flagged, confidence {Confidence:F2}",
            accepted.Count, rejected.Count, flagged.Count, synthesizedConfidence);

        return Task.FromResult(result);
    }

    private static double CalculateWeightedConfidence(SCPContext context)
    {
        var totalWeight = 0.0;
        var weightedSum = 0.0;

        foreach (var stageWeight in context.Config.StageWeights)
        {
            if (Enum.TryParse<SCPStageType>(stageWeight.Key, out var stageType)
                && context.StageResults.TryGetValue(stageType, out var stageResult)
                && stageResult.Success && !stageResult.Skipped)
            {
                weightedSum += stageResult.Confidence * stageWeight.Value;
                totalWeight += stageWeight.Value;
            }
        }

        return totalWeight > 0 ? weightedSum / totalWeight : 0;
    }
}
