using Hazina.AI.FaultDetection.Core;
using System.Text.RegularExpressions;

namespace Hazina.AI.FaultDetection.Detectors;

/// <summary>
/// Basic hallucination detector using heuristics
/// </summary>
public class BasicHallucinationDetector : IHallucinationDetector
{
    public async Task<HallucinationDetectionResult> DetectAsync(
        string response,
        ValidationContext context,
        CancellationToken cancellationToken = default)
    {
        var result = new HallucinationDetectionResult();

        // 1. Check for contradictions with conversation history
        DetectContradictions(response, context, result);

        // 2. Check for unsupported claims
        DetectUnsupportedClaims(response, context, result);

        // 3. Check for context mismatches
        DetectContextMismatches(response, context, result);

        // 4. Check for fabricated technical details
        DetectFabricatedDetails(response, result);

        // 5. Check for temporal inconsistencies
        DetectTemporalErrors(response, result);

        result.ContainsHallucination = result.Instances.Any();
        result.ConfidenceScore = CalculateConfidence(result);

        return await Task.FromResult(result);
    }

    private void DetectContradictions(
        string response,
        ValidationContext context,
        HallucinationDetectionResult result)
    {
        // Check if response contradicts conversation history
        foreach (var message in context.ConversationHistory)
        {
            if (message.Role == HazinaMessageRole.Assistant)
            {
                // Simple keyword-based contradiction detection
                // This is a basic implementation - could be enhanced with semantic similarity
                var responseWords = GetSignificantWords(response);
                var historyWords = GetSignificantWords(message.Text);

                var opposites = new Dictionary<string, string>
                {
                    { "yes", "no" },
                    { "true", "false" },
                    { "correct", "incorrect" },
                    { "valid", "invalid" },
                    { "possible", "impossible" }
                };

                foreach (var (word, opposite) in opposites)
                {
                    if (responseWords.Contains(opposite) && historyWords.Contains(word))
                    {
                        result.Instances.Add(new HallucinationInstance
                        {
                            Content = $"Contradiction detected: '{opposite}' vs previous '{word}'",
                            Type = HallucinationType.Contradiction,
                            Confidence = 0.7,
                            Reasoning = "Response contradicts previous statement"
                        });
                    }
                }
            }
        }
    }

    private void DetectUnsupportedClaims(
        string response,
        ValidationContext context,
        HallucinationDetectionResult result)
    {
        // Check if response makes claims not supported by context
        if (context.GroundTruth.Any())
        {
            // Look for definitive statements
            var definitivePatterns = new[]
            {
                @"(?:is|are|was|were)\s+definitely",
                @"(?:is|are|was|were)\s+exactly",
                @"(?:is|are|was|were)\s+precisely",
                @"the\s+fact\s+(?:is|that)",
                @"it\s+(?:is|was)\s+proven"
            };

            foreach (var pattern in definitivePatterns)
            {
                var matches = Regex.Matches(response, pattern, RegexOptions.IgnoreCase);
                if (matches.Count > 0)
                {
                    // Check if these claims are in ground truth
                    var hasGroundTruth = context.GroundTruth.Values
                        .Any(v => response.Contains(v, StringComparison.OrdinalIgnoreCase));

                    if (!hasGroundTruth)
                    {
                        result.Instances.Add(new HallucinationInstance
                        {
                            Content = matches[0].Value,
                            Type = HallucinationType.UnsupportedClaim,
                            Confidence = 0.6,
                            Reasoning = "Definitive claim without ground truth support"
                        });
                    }
                }
            }
        }
    }

    private void DetectContextMismatches(
        string response,
        ValidationContext context,
        HallucinationDetectionResult result)
    {
        // Check if response doesn't match the prompt context
        if (!string.IsNullOrWhiteSpace(context.Prompt))
        {
            var promptKeywords = GetSignificantWords(context.Prompt);
            var responseKeywords = GetSignificantWords(response);

            var overlap = promptKeywords.Intersect(responseKeywords).Count();
            var overlapRatio = (double)overlap / Math.Max(promptKeywords.Count, 1);

            if (overlapRatio < 0.2 && response.Length > 50)
            {
                result.Instances.Add(new HallucinationInstance
                {
                    Content = "Response context mismatch",
                    Type = HallucinationType.ContextMismatch,
                    Confidence = 0.5,
                    Reasoning = $"Low keyword overlap with prompt: {overlapRatio:P0}"
                });
            }
        }
    }

    private void DetectFabricatedDetails(string response, HallucinationDetectionResult result)
    {
        // Detect suspiciously specific details that might be fabricated
        var suspiciousPatterns = new[]
        {
            @"\b\d{3,}\.\d{2,}\b",           // Overly precise numbers (123.456)
            @"\b\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\b",  // Specific timestamps
            @"\b[A-Z0-9]{8,}-[A-Z0-9]{4,}-[A-Z0-9]{4,}\b"  // UUID-like patterns
        };

        foreach (var pattern in suspiciousPatterns)
        {
            var matches = Regex.Matches(response, pattern);
            if (matches.Count > 3) // Many specific details might be fabricated
            {
                result.Instances.Add(new HallucinationInstance
                {
                    Content = "Multiple overly specific details",
                    Type = HallucinationType.FabricatedFact,
                    Confidence = 0.4,
                    Reasoning = "Response contains many suspiciously specific details"
                });
            }
        }
    }

    private void DetectTemporalErrors(string response, HallucinationDetectionResult result)
    {
        // Detect temporal inconsistencies
        var yearPattern = @"\b(19|20)\d{2}\b";
        var years = Regex.Matches(response, yearPattern)
            .Select(m => int.Parse(m.Value))
            .ToList();

        if (years.Any())
        {
            var futureYears = years.Where(y => y > DateTime.UtcNow.Year).ToList();
            if (futureYears.Any())
            {
                result.Instances.Add(new HallucinationInstance
                {
                    Content = $"Future year referenced: {futureYears.First()}",
                    Type = HallucinationType.TemporalError,
                    Confidence = 0.8,
                    Reasoning = "Response references future years as past events"
                });
            }
        }
    }

    private HashSet<string> GetSignificantWords(string text)
    {
        var stopWords = new HashSet<string>
        {
            "the", "a", "an", "and", "or", "but", "in", "on", "at", "to", "for",
            "of", "with", "by", "from", "is", "are", "was", "were", "be", "been"
        };

        return text
            .Split(new[] { ' ', ',', '.', '!', '?', ';', ':', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.ToLowerInvariant().Trim())
            .Where(w => w.Length > 2 && !stopWords.Contains(w))
            .ToHashSet();
    }

    private double CalculateConfidence(HallucinationDetectionResult result)
    {
        if (!result.Instances.Any())
            return 1.0;

        return 1.0 - (result.Instances.Average(i => i.Confidence) * 0.8);
    }
}
