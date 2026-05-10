using Hazina.AI.CognitivePipeline.Core;
using Hazina.AI.CognitivePipeline.GroundTruth;
using Microsoft.Extensions.Logging;

namespace Hazina.AI.CognitivePipeline.Stages;

/// <summary>
/// Stage 1: Sensory Input.
/// Normalizes raw input, tokenizes claims into individual facts,
/// and checks the GroundTruth store for cached results (enabling short-circuit).
/// </summary>
public class SensoryStage : ISCPStage
{
    private readonly IGroundTruthStore _groundTruthStore;
    private readonly ILogger<SensoryStage> _logger;

    public SCPStageType StageType => SCPStageType.Sensory;
    public int Order => 1;
    public string Name => "Sensory";

    public SensoryStage(IGroundTruthStore groundTruthStore, ILogger<SensoryStage> logger)
    {
        _groundTruthStore = groundTruthStore ?? throw new ArgumentNullException(nameof(groundTruthStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SCPStageResult> ExecuteAsync(SCPContext context, CancellationToken cancellationToken = default)
    {
        var result = new SCPStageResult { StageType = StageType };
        var domain = context.Domain ?? "general";

        // Normalize and tokenize raw input into individual facts
        var facts = new Dictionary<string, object>();
        foreach (var entry in context.RawData)
        {
            var normalizedKey = NormalizeKey(entry.Key);
            var normalizedValue = NormalizeValue(entry.Value);
            facts[normalizedKey] = normalizedValue;
        }

        // Check each fact against GroundTruth store
        var groundTruthHitCount = 0;
        foreach (var fact in facts)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var lookup = await _groundTruthStore.LookupAsync(domain, fact.Key, cancellationToken);
            if (lookup is { Found: true })
            {
                context.GroundTruthHits[fact.Key] = lookup;
                groundTruthHitCount++;
                _logger.LogDebug("GroundTruth hit for {Key} (confidence: {Confidence:F2})", fact.Key, lookup.Fact?.Confidence ?? 0);
            }
        }

        // Populate organized data with normalized facts for downstream stages
        foreach (var fact in facts)
        {
            context.OrganizedData[fact.Key] = fact.Value;
        }

        // Determine if we can short-circuit
        if (context.Config.EnableGroundTruthShortCircuit
            && facts.Count > 0
            && groundTruthHitCount == facts.Count)
        {
            var avgConfidence = context.GroundTruthHits.Values
                .Where(h => h.Fact != null)
                .Average(h => h.Fact!.Confidence);

            if (avgConfidence >= context.Config.GroundTruthShortCircuitThreshold)
            {
                context.GroundTruthShortCircuit = true;
                result.Findings.Add($"GroundTruth short-circuit triggered: all {facts.Count} facts cached with avg confidence {avgConfidence:F2}");
                _logger.LogInformation("GroundTruth short-circuit: all {Count} facts cached (avg confidence {Confidence:F2})", facts.Count, avgConfidence);
            }
        }

        result.Confidence = facts.Count > 0 ? (double)groundTruthHitCount / facts.Count : 0;
        result.Findings.Add($"Processed {facts.Count} facts, {groundTruthHitCount} GroundTruth hits");
        result.Data["FactCount"] = facts.Count;
        result.Data["GroundTruthHits"] = groundTruthHitCount;
        result.Success = true;

        return result;
    }

    private static string NormalizeKey(string key)
    {
        return key.Trim().ToLowerInvariant().Replace(" ", "-");
    }

    private static object NormalizeValue(object value)
    {
        if (value is string s)
            return s.Trim();
        return value;
    }
}
