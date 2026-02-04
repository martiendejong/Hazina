using System;
using System.Collections.Generic;
using System.Linq;

namespace Hazina.App.HazinaCoder.Core.Tools;

/// <summary>
/// Tracks which tools are used, how effective they are, and learns patterns.
/// Helps identify underutilized or ineffective tools.
/// </summary>
/// <remarks>
/// Improvement #30: Tool Effectiveness Tracking
///
/// Metrics tracked:
/// - Usage frequency
/// - Success rate
/// - User satisfaction (implicit)
/// - Problem resolution rate
/// - Time to solution
/// </remarks>
public class ToolEffectivenessTracker
{
    private readonly Dictionary<string, ToolEffectiveness> _effectiveness = new();
    private readonly object _lock = new();

    /// <summary>
    /// Record tool usage
    /// </summary>
    public void RecordUsage(string toolName, bool solved, TimeSpan duration)
    {
        lock (_lock)
        {
            if (!_effectiveness.ContainsKey(toolName))
            {
                _effectiveness[toolName] = new ToolEffectiveness
                {
                    ToolName = toolName
                };
            }

            var eff = _effectiveness[toolName];
            eff.TotalUsages++;
            eff.LastUsed = DateTime.UtcNow;

            if (solved)
            {
                eff.SuccessfulUsages++;
                eff.TotalTimeToSolution += duration;
            }

            eff.EffectivenessScore = CalculateScore(eff);
        }
    }

    /// <summary>
    /// Record user feedback on tool effectiveness
    /// </summary>
    public void RecordFeedback(string toolName, bool wasHelpful, string? comment = null)
    {
        lock (_lock)
        {
            if (_effectiveness.TryGetValue(toolName, out var eff))
            {
                eff.FeedbackCount++;
                if (wasHelpful)
                    eff.PositiveFeedback++;

                if (!string.IsNullOrEmpty(comment))
                {
                    eff.Comments.Add(new ToolFeedback
                    {
                        Comment = comment,
                        Timestamp = DateTime.UtcNow,
                        Positive = wasHelpful
                    });
                }

                eff.EffectivenessScore = CalculateScore(eff);
            }
        }
    }

    /// <summary>
    /// Get effectiveness metrics for a tool
    /// </summary>
    public ToolEffectiveness? GetEffectiveness(string toolName)
    {
        lock (_lock)
        {
            return _effectiveness.TryGetValue(toolName, out var eff) ? eff : null;
        }
    }

    /// <summary>
    /// Get all tools ranked by effectiveness
    /// </summary>
    public IReadOnlyList<ToolEffectiveness> GetRankedTools()
    {
        lock (_lock)
        {
            return _effectiveness.Values
                .OrderByDescending(e => e.EffectivenessScore)
                .ToList();
        }
    }

    /// <summary>
    /// Get underutilized tools (low usage but high success rate)
    /// </summary>
    public IEnumerable<ToolEffectiveness> GetUnderutilized(int minSuccessRate = 80)
    {
        lock (_lock)
        {
            return _effectiveness.Values
                .Where(e => e.TotalUsages < 10 && e.GetSuccessRate() >= minSuccessRate)
                .OrderByDescending(e => e.GetSuccessRate());
        }
    }

    /// <summary>
    /// Get ineffective tools (high usage but low success rate)
    /// </summary>
    public IEnumerable<ToolEffectiveness> GetIneffective(int maxSuccessRate = 50)
    {
        lock (_lock)
        {
            return _effectiveness.Values
                .Where(e => e.TotalUsages >= 10 && e.GetSuccessRate() <= maxSuccessRate)
                .OrderBy(e => e.GetSuccessRate());
        }
    }

    /// <summary>
    /// Calculate effectiveness score (0-100)
    /// </summary>
    private double CalculateScore(ToolEffectiveness eff)
    {
        if (eff.TotalUsages == 0)
            return 0;

        // Weighted formula:
        // 50% success rate
        // 30% user feedback
        // 20% usage frequency (normalized)

        double successScore = eff.GetSuccessRate();

        double feedbackScore = 0;
        if (eff.FeedbackCount > 0)
        {
            feedbackScore = (eff.PositiveFeedback * 100.0) / eff.FeedbackCount;
        }

        double usageScore = Math.Min(100, eff.TotalUsages * 5); // Cap at 100

        return (successScore * 0.5) + (feedbackScore * 0.3) + (usageScore * 0.2);
    }

    /// <summary>
    /// Display effectiveness report
    /// </summary>
    public void DisplayReport()
    {
        lock (_lock)
        {
            Console.WriteLine("\n[Tool Effectiveness Report]");
            Console.WriteLine("────────────────────────────────────────────────────────");

            var ranked = GetRankedTools();
            foreach (var eff in ranked.Take(10))
            {
                Console.WriteLine($"\n{eff.ToolName}:");
                Console.WriteLine($"  Effectiveness: {eff.EffectivenessScore:F1}");
                Console.WriteLine($"  Usage: {eff.TotalUsages}");
                Console.WriteLine($"  Success Rate: {eff.GetSuccessRate():F1}%");
                Console.WriteLine($"  Avg Time to Solution: {eff.GetAverageTimeToSolution().TotalSeconds:F1}s");
                Console.WriteLine($"  User Feedback: {eff.PositiveFeedback}/{eff.FeedbackCount} positive");
            }

            Console.WriteLine("\n[Underutilized Tools]");
            foreach (var eff in GetUnderutilized().Take(5))
            {
                Console.WriteLine($"  {eff.ToolName}: {eff.GetSuccessRate():F1}% success, only {eff.TotalUsages} uses");
            }

            Console.WriteLine("\n[Ineffective Tools]");
            foreach (var eff in GetIneffective().Take(5))
            {
                Console.WriteLine($"  {eff.ToolName}: {eff.GetSuccessRate():F1}% success from {eff.TotalUsages} uses");
            }
        }
    }
}

/// <summary>
/// Effectiveness metrics for a single tool
/// </summary>
public class ToolEffectiveness
{
    public string ToolName { get; set; } = string.Empty;
    public int TotalUsages { get; set; }
    public int SuccessfulUsages { get; set; }
    public TimeSpan TotalTimeToSolution { get; set; }
    public DateTime LastUsed { get; set; }
    public double EffectivenessScore { get; set; }
    public int FeedbackCount { get; set; }
    public int PositiveFeedback { get; set; }
    public List<ToolFeedback> Comments { get; set; } = new();

    public double GetSuccessRate() =>
        TotalUsages > 0 ? (SuccessfulUsages * 100.0 / TotalUsages) : 0;

    public TimeSpan GetAverageTimeToSolution() =>
        SuccessfulUsages > 0
            ? TimeSpan.FromMilliseconds(TotalTimeToSolution.TotalMilliseconds / SuccessfulUsages)
            : TimeSpan.Zero;
}

/// <summary>
/// User feedback on tool effectiveness
/// </summary>
public class ToolFeedback
{
    public string Comment { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public bool Positive { get; set; }
}
