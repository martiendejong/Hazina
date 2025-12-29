using Hazina.AI.FaultDetection.Core;
using System.Text.RegularExpressions;

namespace Hazina.AI.FaultDetection.Analyzers;

/// <summary>
/// Basic confidence scorer using heuristics
/// </summary>
public class BasicConfidenceScorer : IConfidenceScorer
{
    public async Task<ConfidenceScore> ScoreAsync(
        string response,
        ValidationContext context,
        CancellationToken cancellationToken = default)
    {
        var score = new ConfidenceScore { Score = 1.0 };

        // 1. Length-based confidence
        var lengthScore = ScoreByLength(response, context);
        score.ComponentScores["length"] = lengthScore;
        score.Factors.Add(new ConfidenceFactor
        {
            Name = "Length",
            Impact = lengthScore - 0.5,
            Description = $"Response length: {response.Length} characters"
        });

        // 2. Hedging language detection
        var hedgingScore = ScoreByHedging(response);
        score.ComponentScores["hedging"] = hedgingScore;
        score.Factors.Add(new ConfidenceFactor
        {
            Name = "Hedging",
            Impact = hedgingScore - 0.5,
            Description = "Presence of uncertain language"
        });

        // 3. Specificity score
        var specificityScore = ScoreBySpecificity(response);
        score.ComponentScores["specificity"] = specificityScore;
        score.Factors.Add(new ConfidenceFactor
        {
            Name = "Specificity",
            Impact = specificityScore - 0.5,
            Description = "Level of detail and specificity"
        });

        // 4. Consistency score
        var consistencyScore = ScoreByConsistency(response, context);
        score.ComponentScores["consistency"] = consistencyScore;
        score.Factors.Add(new ConfidenceFactor
        {
            Name = "Consistency",
            Impact = consistencyScore - 0.5,
            Description = "Consistency with context"
        });

        // 5. Format compliance score
        var formatScore = ScoreByFormat(response, context);
        score.ComponentScores["format"] = formatScore;
        score.Factors.Add(new ConfidenceFactor
        {
            Name = "Format",
            Impact = formatScore - 0.5,
            Description = "Compliance with expected format"
        });

        // Calculate weighted average
        score.Score = CalculateWeightedScore(score.ComponentScores);

        score.Reasoning = GenerateReasoning(score);

        return await Task.FromResult(score);
    }

    private double ScoreByLength(string response, ValidationContext context)
    {
        if (string.IsNullOrWhiteSpace(response))
            return 0.0;

        // Too short responses are suspicious
        if (response.Length < 10)
            return 0.3;

        // Very long responses might be rambling
        if (response.Length > 5000)
            return 0.7;

        // Optimal length range
        return response.Length switch
        {
            < 50 => 0.6,
            < 200 => 0.9,
            < 1000 => 1.0,
            < 3000 => 0.9,
            _ => 0.7
        };
    }

    private double ScoreByHedging(string response)
    {
        // Detect hedging/uncertain language
        var hedgingPhrases = new[]
        {
            "maybe", "perhaps", "possibly", "might", "could be",
            "I think", "I believe", "seems like", "appears to be",
            "probably", "likely", "not sure", "unclear"
        };

        var hedgeCount = hedgingPhrases.Count(phrase =>
            response.Contains(phrase, StringComparison.OrdinalIgnoreCase));

        // Some hedging is natural, too much indicates low confidence
        return hedgeCount switch
        {
            0 => 1.0,
            1 => 0.9,
            2 => 0.7,
            3 => 0.5,
            _ => 0.3
        };
    }

    private double ScoreBySpecificity(string response)
    {
        // Higher specificity = higher confidence
        var specificityIndicators = new[]
        {
            @"\b\d+\b",                    // Numbers
            @"[A-Z][a-z]+\s[A-Z][a-z]+",  // Proper nouns
            @"\b(20\d{2}|19\d{2})\b",     // Years
            @"\b\d+%\b",                   // Percentages
            @"\$\d+",                      // Money
            @"version \d",                  // Versions
            @"step \d",                     // Steps
        };

        var matches = specificityIndicators.Sum(pattern =>
            Regex.Matches(response, pattern).Count);

        // Normalize by response length
        var density = matches / Math.Max(response.Length / 100.0, 1);

        return density switch
        {
            > 5 => 0.9,
            > 3 => 0.8,
            > 1 => 0.7,
            > 0.5 => 0.6,
            _ => 0.5
        };
    }

    private double ScoreByConsistency(string response, ValidationContext context)
    {
        if (string.IsNullOrWhiteSpace(context.Prompt))
            return 0.8; // Neutral if no context

        // Check keyword overlap with prompt
        var promptKeywords = GetKeywords(context.Prompt);
        var responseKeywords = GetKeywords(response);

        if (!promptKeywords.Any())
            return 0.8;

        var overlap = promptKeywords.Intersect(responseKeywords).Count();
        var overlapRatio = (double)overlap / promptKeywords.Count;

        return overlapRatio switch
        {
            > 0.7 => 1.0,
            > 0.5 => 0.9,
            > 0.3 => 0.7,
            > 0.1 => 0.5,
            _ => 0.3
        };
    }

    private double ScoreByFormat(string response, ValidationContext context)
    {
        return context.ResponseType switch
        {
            ResponseType.Json => IsValidJson(response) ? 1.0 : 0.3,
            ResponseType.Xml => IsValidXml(response) ? 1.0 : 0.3,
            ResponseType.Code => ContainsCodeIndicators(response) ? 0.9 : 0.5,
            ResponseType.Markdown => ContainsMarkdownIndicators(response) ? 0.9 : 0.7,
            _ => 0.8 // Neutral for text
        };
    }

    private double CalculateWeightedScore(Dictionary<string, double> componentScores)
    {
        var weights = new Dictionary<string, double>
        {
            { "length", 0.1 },
            { "hedging", 0.2 },
            { "specificity", 0.2 },
            { "consistency", 0.3 },
            { "format", 0.2 }
        };

        var weightedSum = componentScores.Sum(kvp =>
            weights.GetValueOrDefault(kvp.Key, 0.2) * kvp.Value);

        return Math.Max(0, Math.Min(1, weightedSum));
    }

    private string GenerateReasoning(ConfidenceScore score)
    {
        var level = score.IsHighConfidence ? "high" :
                    score.IsMediumConfidence ? "medium" : "low";

        var mainFactors = score.Factors
            .OrderByDescending(f => Math.Abs(f.Impact))
            .Take(2)
            .Select(f => f.Name)
            .ToList();

        return $"Confidence is {level} ({score.Score:F2}). " +
               $"Main factors: {string.Join(", ", mainFactors)}";
    }

    private HashSet<string> GetKeywords(string text)
    {
        var stopWords = new HashSet<string>
        {
            "the", "a", "an", "and", "or", "but", "in", "on", "at", "to", "for",
            "of", "with", "by", "from", "is", "are", "was", "were"
        };

        return text
            .Split(new[] { ' ', ',', '.', '!', '?', ';', ':', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.ToLowerInvariant().Trim())
            .Where(w => w.Length > 2 && !stopWords.Contains(w))
            .ToHashSet();
    }

    private bool IsValidJson(string text)
    {
        try
        {
            System.Text.Json.JsonDocument.Parse(text);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool IsValidXml(string text)
    {
        try
        {
            System.Xml.Linq.XDocument.Parse(text);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool ContainsCodeIndicators(string text)
    {
        var indicators = new[] { "{", "}", "(", ")", "function", "class", "var", "const" };
        return indicators.Count(i => text.Contains(i)) >= 3;
    }

    private bool ContainsMarkdownIndicators(string text)
    {
        var indicators = new[] { "#", "**", "*", "```", "-", ">" };
        return indicators.Count(i => text.Contains(i)) >= 2;
    }
}
