using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Hazina.AI.DecisionTracking;

/// <summary>
/// JSONL-based decision auditor for transparency and learning
/// </summary>
public class DecisionAuditorService : IDecisionAuditor
{
    private readonly ILogger<DecisionAuditorService> _logger;
    private readonly string _decisionsPath;
    private readonly string _outcomesPath;

    public DecisionAuditorService(ILogger<DecisionAuditorService> logger, string? basePath = null)
    {
        _logger = logger;
        var dir = basePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Hazina",
            "DecisionTracking"
        );

        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        _decisionsPath = Path.Combine(dir, "decisions.jsonl");
        _outcomesPath = Path.Combine(dir, "outcomes.jsonl");
    }

    public async Task<string> LogDecision(Decision decision)
    {
        await Task.CompletedTask;

        var json = JsonSerializer.Serialize(decision);
        await File.AppendAllLinesAsync(_decisionsPath, new[] { json });

        _logger.LogInformation(
            "Logged decision {Id} with confidence {Confidence:P0}: {Decision}",
            decision.Id, decision.Confidence, decision.DecisionMade
        );

        return decision.Id;
    }

    public async Task<List<Decision>> SearchSimilar(string context, int limit = 5)
    {
        if (!File.Exists(_decisionsPath))
            return [];

        var lines = await File.ReadAllLinesAsync(_decisionsPath);
        var decisions = new List<(Decision decision, double score)>();

        foreach (var line in lines)
        {
            try
            {
                var decision = JsonSerializer.Deserialize<Decision>(line);
                if (decision != null)
                {
                    // Simple cosine similarity on context words
                    var score = CalculateSimilarity(context, decision.Context);
                    decisions.Add((decision, score));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse decision line");
            }
        }

        return decisions
            .OrderByDescending(d => d.score)
            .Take(limit)
            .Select(d => d.decision)
            .ToList();
    }

    public async Task<DecisionOutcome?> GetOutcome(string decisionId)
    {
        if (!File.Exists(_outcomesPath))
            return null;

        var lines = await File.ReadAllLinesAsync(_outcomesPath);

        foreach (var line in lines)
        {
            try
            {
                var outcome = JsonSerializer.Deserialize<DecisionOutcome>(line);
                if (outcome?.DecisionId == decisionId)
                    return outcome;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse outcome line");
            }
        }

        return null;
    }

    public async Task RecordOutcome(string decisionId, DecisionOutcome outcome)
    {
        await Task.CompletedTask;

        outcome.DecisionId = decisionId;
        var json = JsonSerializer.Serialize(outcome);
        await File.AppendAllLinesAsync(_outcomesPath, new[] { json });

        _logger.LogInformation(
            "Recorded outcome for decision {Id}: {Success}",
            decisionId, outcome.Successful ? "Success" : "Failure"
        );
    }

    public async Task<CalibrationMetrics> GetCalibration()
    {
        var decisions = await LoadAllDecisions();
        var outcomes = await LoadAllOutcomes();

        var outcomesDict = outcomes.ToDictionary(o => o.DecisionId);
        var decisionsWithOutcomes = decisions.Where(d => outcomesDict.ContainsKey(d.Id)).ToList();

        var metrics = new CalibrationMetrics
        {
            TotalDecisions = decisions.Count,
            DecisionsWithOutcomes = decisionsWithOutcomes.Count
        };

        if (decisionsWithOutcomes.Count > 0)
        {
            metrics.SuccessRate = decisionsWithOutcomes.Count(d => outcomesDict[d.Id].Successful) / (double)decisionsWithOutcomes.Count;

            // Simple confidence calibration: correlation between confidence and success
            var confidenceSuccessCorrelation = decisionsWithOutcomes
                .Select(d => new { Confidence = d.Confidence, Success = outcomesDict[d.Id].Successful ? 1.0 : 0.0 })
                .ToList();

            if (confidenceSuccessCorrelation.Count > 1)
            {
                var avgConf = confidenceSuccessCorrelation.Average(x => x.Confidence);
                var avgSucc = confidenceSuccessCorrelation.Average(x => x.Success);
                var numerator = confidenceSuccessCorrelation.Sum(x => (x.Confidence - avgConf) * (x.Success - avgSucc));
                var denomConf = Math.Sqrt(confidenceSuccessCorrelation.Sum(x => Math.Pow(x.Confidence - avgConf, 2)));
                var denomSucc = Math.Sqrt(confidenceSuccessCorrelation.Sum(x => Math.Pow(x.Success - avgSucc, 2)));

                metrics.ConfidenceCalibration = denomConf > 0 && denomSucc > 0
                    ? numerator / (denomConf * denomSucc)
                    : 0;
            }

            var times = decisionsWithOutcomes.Select(d => outcomesDict[d.Id].TimeToResolution).ToList();
            metrics.AverageTimeToResolution = times.Count > 0
                ? TimeSpan.FromTicks((long)times.Average(t => t.Ticks))
                : TimeSpan.Zero;
        }

        return metrics;
    }

    private async Task<List<Decision>> LoadAllDecisions()
    {
        if (!File.Exists(_decisionsPath))
            return [];

        var lines = await File.ReadAllLinesAsync(_decisionsPath);
        var decisions = new List<Decision>();

        foreach (var line in lines)
        {
            try
            {
                var decision = JsonSerializer.Deserialize<Decision>(line);
                if (decision != null)
                    decisions.Add(decision);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse decision");
            }
        }

        return decisions;
    }

    private async Task<List<DecisionOutcome>> LoadAllOutcomes()
    {
        if (!File.Exists(_outcomesPath))
            return [];

        var lines = await File.ReadAllLinesAsync(_outcomesPath);
        var outcomes = new List<DecisionOutcome>();

        foreach (var line in lines)
        {
            try
            {
                var outcome = JsonSerializer.Deserialize<DecisionOutcome>(line);
                if (outcome != null)
                    outcomes.Add(outcome);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse outcome");
            }
        }

        return outcomes;
    }

    private double CalculateSimilarity(string text1, string text2)
    {
        var words1 = text1.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var words2 = text2.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();

        if (words1.Count == 0 || words2.Count == 0)
            return 0;

        var intersection = words1.Intersect(words2).Count();
        var union = words1.Union(words2).Count();

        return (double)intersection / union; // Jaccard similarity
    }
}
