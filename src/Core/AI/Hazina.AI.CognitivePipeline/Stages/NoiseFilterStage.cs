using Hazina.AI.CognitivePipeline.Core;
using Hazina.AI.CognitivePipeline.GroundTruth;
using Microsoft.Extensions.Logging;

namespace Hazina.AI.CognitivePipeline.Stages;

/// <summary>
/// Stage 3: Noise Filter (Limbic).
/// Compares facts against GroundTruth, removes contradicted claims,
/// and filters unreliable or biased content from the pipeline.
/// </summary>
public class NoiseFilterStage : ISCPStage
{
    private readonly IGroundTruthStore _groundTruthStore;
    private readonly ILogger<NoiseFilterStage> _logger;

    public SCPStageType StageType => SCPStageType.NoiseFilter;
    public int Order => 3;
    public string Name => "NoiseFilter";

    public NoiseFilterStage(IGroundTruthStore groundTruthStore, ILogger<NoiseFilterStage> logger)
    {
        _groundTruthStore = groundTruthStore ?? throw new ArgumentNullException(nameof(groundTruthStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SCPStageResult> ExecuteAsync(SCPContext context, CancellationToken cancellationToken = default)
    {
        var result = new SCPStageResult { StageType = StageType };
        var domain = context.Domain ?? "general";

        var totalFacts = context.OrganizedData.Count;
        var filteredOut = 0;

        // Get all known ground truth facts for this domain
        var groundTruthFacts = await _groundTruthStore.GetFactsAsync(domain, cancellationToken);
        var truthLookup = groundTruthFacts.ToDictionary(f => f.Key, f => f, StringComparer.OrdinalIgnoreCase);

        foreach (var entry in context.OrganizedData)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var valueStr = entry.Value?.ToString() ?? string.Empty;

            // Check if this fact contradicts established ground truth
            if (truthLookup.TryGetValue(entry.Key, out var truthFact))
            {
                if (ContradictsTruth(valueStr, truthFact))
                {
                    filteredOut++;
                    result.Warnings.Add($"Filtered '{entry.Key}': contradicts GroundTruth (expected: '{truthFact.Value}', got: '{valueStr}')");
                    _logger.LogInformation("Noise filter: removed '{Key}' - contradicts GroundTruth", entry.Key);
                    continue;
                }
            }

            // Fact passes filter — add to FilteredData
            context.FilteredData[entry.Key] = entry.Value!;
        }

        var passedCount = context.FilteredData.Count;
        var noiseRate = totalFacts > 0 ? (double)filteredOut / totalFacts : 0;

        result.Confidence = 1.0 - noiseRate; // Higher confidence when less noise
        result.Findings.Add($"Filtered {filteredOut}/{totalFacts} facts as noise (rate: {noiseRate:P0})");
        result.Data["TotalFacts"] = totalFacts;
        result.Data["FilteredOut"] = filteredOut;
        result.Data["PassedCount"] = passedCount;
        result.Data["NoiseRate"] = noiseRate;
        result.Success = true;

        _logger.LogInformation("NoiseFilter: {Filtered}/{Total} removed, {Passed} passed", filteredOut, totalFacts, passedCount);

        return result;
    }

    private static bool ContradictsTruth(string claimedValue, GroundTruthFact truthFact)
    {
        if (string.IsNullOrWhiteSpace(claimedValue) || string.IsNullOrWhiteSpace(truthFact.Value))
            return false;

        // Only consider it a contradiction if the truth has high confidence
        // and the values clearly differ
        if (truthFact.Confidence < 0.7)
            return false;

        return !string.Equals(claimedValue.Trim(), truthFact.Value.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
