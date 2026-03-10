using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Hazina.AI.Learning;

/// <summary>
/// Pattern extraction service using frequency analysis and correlation
/// </summary>
public class PatternExtractorService : IPatternExtractor
{
    private readonly ILogger<PatternExtractorService> _logger;
    private readonly string _patternsPath;
    private const int MIN_OCCURRENCES = 3; // Pattern must occur 3+ times
    private const double MIN_CONFIDENCE = 0.7; // Minimum confidence to promote

    public PatternExtractorService(ILogger<PatternExtractorService> logger, string? basePath = null)
    {
        _logger = logger;
        var dir = basePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Hazina",
            "Learning"
        );

        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        _patternsPath = Path.Combine(dir, "patterns.jsonl");
    }

    public async Task<List<Pattern>> Extract(List<Outcome> outcomes)
    {
        await Task.CompletedTask;

        var patterns = new List<Pattern>();

        // Group by outcome type
        var byType = outcomes.GroupBy(o => o.Type);

        foreach (var group in byType)
        {
            var successful = group.Where(o => o.Success).ToList();
            var failed = group.Where(o => !o.Success).ToList();

            // Find factors correlated with success
            var successFactors = FindCorrelatedFactors(successful, failed);

            foreach (var (factor, correlation) in successFactors)
            {
                if (correlation > 0.6) // Strong positive correlation
                {
                    var pattern = new Pattern
                    {
                        Description = $"When {factor.Key}={factor.Value}, {group.Key} tends to succeed",
                        Condition = $"{factor.Key}={factor.Value}",
                        Action = $"Prioritize tasks where {factor.Key}={factor.Value}",
                        Confidence = correlation,
                        Occurrences = successful.Count(o => MatchesFactor(o, factor)),
                        SuccessRate = successful.Count / (double)group.Count()
                    };

                    patterns.Add(pattern);
                    _logger.LogInformation("Discovered pattern: {Description} (confidence {Confidence:P0})", pattern.Description, pattern.Confidence);
                }
            }
        }

        // Save new patterns
        foreach (var pattern in patterns.Where(p => p.Confidence >= MIN_CONFIDENCE))
        {
            await SavePattern(pattern);
        }

        return patterns;
    }

    public async Task<bool> ShouldPromoteToMemory(Pattern pattern)
    {
        await Task.CompletedTask;

        return pattern.Occurrences >= MIN_OCCURRENCES
            && pattern.Confidence >= MIN_CONFIDENCE
            && pattern.SuccessRate >= 0.7;
    }

    public async Task<ConfidenceScore> ValidatePattern(Pattern pattern, List<Outcome> newOutcomes)
    {
        await Task.CompletedTask;

        var matching = newOutcomes.Where(o => OutcomeMatchesPattern(o, pattern)).ToList();
        var successCount = matching.Count(o => o.Success);

        var score = new ConfidenceScore
        {
            SampleSize = matching.Count
        };

        if (matching.Count > 0)
        {
            score.Overall = successCount / (double)matching.Count;
            // Simple statistical significance based on sample size
            score.StatisticalSignificance = Math.Min(matching.Count / 10.0, 1.0);
        }

        return score;
    }

    public async Task<List<Pattern>> GetActivePatterns()
    {
        if (!File.Exists(_patternsPath))
            return [];

        var lines = await File.ReadAllLinesAsync(_patternsPath);
        var patterns = new List<Pattern>();

        foreach (var line in lines)
        {
            try
            {
                var pattern = JsonSerializer.Deserialize<Pattern>(line);
                if (pattern != null)
                    patterns.Add(pattern);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse pattern");
            }
        }

        return patterns.Where(p => p.Confidence >= MIN_CONFIDENCE).ToList();
    }

    private List<(KeyValuePair<string, object> factor, double correlation)> FindCorrelatedFactors(
        List<Outcome> successful,
        List<Outcome> failed)
    {
        var correlations = new List<(KeyValuePair<string, object> factor, double correlation)>();

        // Collect all unique factors
        var allFactors = successful
            .Concat(failed)
            .SelectMany(o => o.Factors)
            .GroupBy(f => f.Key)
            .Select(g => g.Key)
            .ToList();

        foreach (var factorKey in allFactors)
        {
            // Get all unique values for this factor
            var values = successful
                .Concat(failed)
                .SelectMany(o => o.Factors.Where(f => f.Key == factorKey).Select(f => f.Value))
                .Distinct()
                .ToList();

            foreach (var value in values)
            {
                var factor = new KeyValuePair<string, object>(factorKey, value);
                var successWithFactor = successful.Count(o => MatchesFactor(o, factor));
                var failWithFactor = failed.Count(o => MatchesFactor(o, factor));

                if (successWithFactor + failWithFactor > 0)
                {
                    var correlation = successWithFactor / (double)(successWithFactor + failWithFactor);
                    correlations.Add((factor, correlation));
                }
            }
        }

        return correlations.OrderByDescending(c => c.correlation).ToList();
    }

    private bool MatchesFactor(Outcome outcome, KeyValuePair<string, object> factor)
    {
        return outcome.Factors.TryGetValue(factor.Key, out var value)
            && value?.ToString() == factor.Value?.ToString();
    }

    private bool OutcomeMatchesPattern(Outcome outcome, Pattern pattern)
    {
        // Simple condition matching (can be enhanced with expression parsing)
        return pattern.Condition.Split('=') is [var key, var value]
            && outcome.Factors.TryGetValue(key, out var factorValue)
            && factorValue?.ToString() == value;
    }

    private async Task SavePattern(Pattern pattern)
    {
        var json = JsonSerializer.Serialize(pattern);
        await File.AppendAllLinesAsync(_patternsPath, new[] { json });
    }
}
