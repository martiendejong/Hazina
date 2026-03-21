namespace Hazina.AI.Routing.Services;

public class ModularityAnalysisService : IModularityAnalysisService
{
    private static readonly string[] ModularIndicators = new[]
    {
        "analyze", "compare", "each", "multiple", "several", "various",
        "different", "separate", "independent", "parallel"
    };

    private static readonly string[] SequentialIndicators = new[]
    {
        "then", "after", "next", "finally", "following", "step", "sequence",
        "order", "first", "second", "depends"
    };

    public double CalculateScore(string taskDescription)
    {
        if (string.IsNullOrWhiteSpace(taskDescription))
            return 0.0;

        var lowerTask = taskDescription.ToLowerInvariant();

        // Count modular indicators
        var modularCount = ModularIndicators.Count(indicator => lowerTask.Contains(indicator));

        // Count sequential indicators (reduces modularity)
        var sequentialCount = SequentialIndicators.Count(indicator => lowerTask.Contains(indicator));

        // Count numbered lists (1., 2., etc.) which often indicate independent items
        var numberedItems = System.Text.RegularExpressions.Regex.Matches(lowerTask, @"\d+\.|^\s*-\s+", System.Text.RegularExpressions.RegexOptions.Multiline).Count;

        // Calculate score (0.0 - 1.0)
        var score = (modularCount * 0.2) + (numberedItems * 0.1) - (sequentialCount * 0.15);

        // Clamp to 0-1 range
        return Math.Max(0.0, Math.Min(1.0, score));
    }
}
