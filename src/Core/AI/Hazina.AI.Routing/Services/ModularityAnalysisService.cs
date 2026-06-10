using System.Text.RegularExpressions;
using Hazina.AI.Routing.Interfaces;

namespace Hazina.AI.Routing.Services;

/// <summary>
/// Heuristic-based modularity analyzer that scores how parallelizable a task is.
/// Score > 0.7 = good candidate for polymath delegation (multiple agents).
/// Score < 0.3 = sequential task, single agent preferred.
/// </summary>
public partial class ModularityAnalysisService : IModularityAnalysisService
{
    // Patterns indicating independent sub-tasks (increase modularity)
    private static readonly string[] ParallelIndicators =
    [
        "analyze", "compare", "evaluate", "review", "assess",
        "each", "every", "all", "multiple", "various",
        "pros and cons", "advantages and disadvantages",
        "different perspectives", "from multiple angles",
        "markets", "categories", "segments", "regions",
        "independently", "separately", "in parallel"
    ];

    // Patterns indicating sequential dependency (decrease modularity)
    private static readonly string[] SequentialIndicators =
    [
        "step by step", "first then", "after that", "before",
        "depends on", "based on the result", "following",
        "sequential", "in order", "one by one",
        "build upon", "chain", "pipeline", "workflow",
        "write", "create", "generate", "compose", "draft"
    ];

    /// <inheritdoc />
    public double CalculateScore(string taskDescription)
    {
        if (string.IsNullOrWhiteSpace(taskDescription))
            return 0.0;

        var lower = taskDescription.ToLowerInvariant();
        var score = 0.5; // Neutral baseline

        // Count parallel indicators
        var parallelHits = ParallelIndicators.Count(indicator => lower.Contains(indicator));
        score += parallelHits * 0.08;

        // Count sequential indicators
        var sequentialHits = SequentialIndicators.Count(indicator => lower.Contains(indicator));
        score -= sequentialHits * 0.1;

        // Count enumerated items (numbered lists, bullet points, comma-separated items)
        var enumerationCount = CountEnumerations(taskDescription);
        if (enumerationCount >= 3)
            score += 0.15;
        else if (enumerationCount >= 2)
            score += 0.08;

        // Question marks suggest multiple independent sub-questions
        var questionCount = taskDescription.Count(c => c == '?');
        if (questionCount >= 3)
            score += 0.15;
        else if (questionCount >= 2)
            score += 0.08;

        return Math.Clamp(score, 0.0, 1.0);
    }

    private static int CountEnumerations(string text)
    {
        // Count numbered items (1. 2. 3. etc.)
        var numberedItems = NumberedListRegex().Matches(text).Count;

        // Count bullet points (- or *)
        var bulletItems = BulletListRegex().Matches(text).Count;

        // Count comma-separated items in a single sentence
        var commaGroups = 0;
        var sentences = text.Split('.', '!', '?');
        foreach (var sentence in sentences)
        {
            var commas = sentence.Count(c => c == ',');
            if (commas >= 2)
                commaGroups += commas + 1;
        }

        return Math.Max(numberedItems, Math.Max(bulletItems, commaGroups));
    }

    [GeneratedRegex(@"^\s*\d+[\.\)]\s", RegexOptions.Multiline)]
    private static partial Regex NumberedListRegex();

    [GeneratedRegex(@"^\s*[-*]\s", RegexOptions.Multiline)]
    private static partial Regex BulletListRegex();
}
