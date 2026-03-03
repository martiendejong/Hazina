using System;
using System.Collections.Generic;
using System.Linq;

namespace Hazina.App.HazinaCoder.Core.Learning;

/// <summary>
/// Automatic 5-whys analysis for every failure.
/// Systematically investigates root causes to prevent recurrence.
/// </summary>
/// <remarks>
/// Improvement #33: Failure Analysis Automation
///
/// For every failure:
/// 1. What went wrong? (symptom)
/// 2. Why did it happen? (immediate cause)
/// 3. Why did that happen? (level 2)
/// 4. Why did that happen? (level 3)
/// 5. Why did that happen? (root cause)
/// </remarks>
public class FailureAnalyzer
{
    private readonly List<FailureAnalysis> _analyses = new();

    /// <summary>
    /// Analyze a failure using 5-whys technique
    /// </summary>
    public FailureAnalysis AnalyzeFailure(string symptom, string context)
    {
        var analysis = new FailureAnalysis
        {
            Id = Guid.NewGuid(),
            Symptom = symptom,
            Context = context,
            Timestamp = DateTime.UtcNow
        };

        Console.WriteLine("\n[Failure Analysis] Starting 5-whys investigation");
        Console.WriteLine($"Symptom: {symptom}");
        Console.WriteLine($"Context: {context}\n");

        // TODO: Use LLM to generate progressive "why" questions
        // For now, record the structure
        analysis.Whys.Add(new WhyLevel
        {
            Level = 1,
            Question = "Why did this failure occur?",
            Answer = "TODO: LLM analysis"
        });

        _analyses.Add(analysis);

        Console.WriteLine("[Failure Analysis] Analysis recorded for future learning");
        return analysis;
    }

    /// <summary>
    /// Record a corrective action for an analysis
    /// </summary>
    public void RecordCorrectiveAction(Guid analysisId, string action, string expectedOutcome)
    {
        var analysis = _analyses.FirstOrDefault(a => a.Id == analysisId);
        if (analysis != null)
        {
            analysis.CorrectiveActions.Add(new CorrectiveAction
            {
                Action = action,
                ExpectedOutcome = expectedOutcome,
                Implemented = DateTime.UtcNow
            });

            Console.WriteLine($"[Failure Analysis] Recorded corrective action: {action}");
        }
    }

    /// <summary>
    /// Get all failure analyses
    /// </summary>
    public IReadOnlyList<FailureAnalysis> GetAnalyses() => _analyses;

    /// <summary>
    /// Find similar past failures
    /// </summary>
    public IEnumerable<FailureAnalysis> FindSimilar(string symptom)
    {
        // TODO: Use embeddings for semantic similarity
        var keywords = symptom.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return _analyses
            .Where(a => keywords.Any(k => a.Symptom.ToLower().Contains(k)))
            .OrderByDescending(a => a.Timestamp);
    }

    /// <summary>
    /// Display failure patterns summary
    /// </summary>
    public void DisplayPatterns()
    {
        Console.WriteLine("\n[Failure Patterns]");
        Console.WriteLine("──────────────────────────────────────────────────");

        // Group by root cause (simplified)
        var grouped = _analyses
            .GroupBy(a => a.Whys.LastOrDefault()?.Answer ?? "Unknown")
            .OrderByDescending(g => g.Count());

        foreach (var group in grouped.Take(5))
        {
            Console.WriteLine($"\nRoot Cause: {group.Key}");
            Console.WriteLine($"  Occurrences: {group.Count()}");
            Console.WriteLine($"  Latest: {group.First().Timestamp:yyyy-MM-dd}");
        }
    }
}

/// <summary>
/// Complete failure analysis
/// </summary>
public class FailureAnalysis
{
    public Guid Id { get; set; }
    public string Symptom { get; set; } = string.Empty;
    public string Context { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public List<WhyLevel> Whys { get; set; } = new();
    public List<CorrectiveAction> CorrectiveActions { get; set; } = new();
}

/// <summary>
/// Single "why" level in 5-whys analysis
/// </summary>
public class WhyLevel
{
    public int Level { get; set; }
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
}

/// <summary>
/// Corrective action to prevent recurrence
/// </summary>
public class CorrectiveAction
{
    public string Action { get; set; } = string.Empty;
    public string ExpectedOutcome { get; set; } = string.Empty;
    public DateTime Implemented { get; set; }
    public bool Verified { get; set; }
}
