using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Hazina.AI.PromptManagement.Dashboard;

/// <summary>
/// Analyzes patterns in evaluation results
/// </summary>
public interface IPatternAnalyzer
{
    /// <summary>
    /// Detect failure patterns in evaluation results
    /// </summary>
    /// <param name="promptId">Prompt ID</param>
    /// <param name="startDate">Start date</param>
    /// <param name="endDate">End date</param>
    /// <param name="minFrequency">Minimum frequency threshold (0.0 to 1.0)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Detected failure patterns</returns>
    Task<List<FailurePattern>> DetectFailurePatternsAsync(
        string promptId,
        DateTime startDate,
        DateTime endDate,
        double minFrequency = 0.1,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Detect success patterns in evaluation results
    /// </summary>
    /// <param name="promptId">Prompt ID</param>
    /// <param name="startDate">Start date</param>
    /// <param name="endDate">End date</param>
    /// <param name="minFrequency">Minimum frequency threshold (0.0 to 1.0)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Detected success patterns</returns>
    Task<List<SuccessPattern>> DetectSuccessPatternsAsync(
        string promptId,
        DateTime startDate,
        DateTime endDate,
        double minFrequency = 0.1,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Detect drift in prompt performance over time
    /// </summary>
    /// <param name="promptId">Prompt ID</param>
    /// <param name="metricName">Metric to analyze</param>
    /// <param name="baselineStartDate">Baseline period start</param>
    /// <param name="baselineEndDate">Baseline period end</param>
    /// <param name="currentStartDate">Current period start</param>
    /// <param name="currentEndDate">Current period end</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Drift analysis</returns>
    Task<DriftAnalysis> DetectDriftAsync(
        string promptId,
        string metricName,
        DateTime baselineStartDate,
        DateTime baselineEndDate,
        DateTime currentStartDate,
        DateTime currentEndDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get pattern evolution over time
    /// </summary>
    /// <param name="promptId">Prompt ID</param>
    /// <param name="patternType">"failure" or "success"</param>
    /// <param name="startDate">Start date</param>
    /// <param name="endDate">End date</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Pattern evolution timeline</returns>
    Task<PatternEvolution> GetPatternEvolutionAsync(
        string promptId,
        string patternType,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Detected failure pattern
/// </summary>
public class FailurePattern
{
    public string PatternId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double Frequency { get; set; }  // 0.0 to 1.0
    public int Occurrences { get; set; }
    public List<string> Examples { get; set; } = new();  // Example queries/responses
    public string? RootCause { get; set; }
    public List<string> AffectedMetrics { get; set; } = new();
    public double Severity { get; set; }  // 0.0 to 1.0
    public DateTime FirstSeen { get; set; }
    public DateTime LastSeen { get; set; }
    public bool IsRecurring { get; set; }
}

/// <summary>
/// Detected success pattern
/// </summary>
public class SuccessPattern
{
    public string PatternId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double Frequency { get; set; }  // 0.0 to 1.0
    public int Occurrences { get; set; }
    public List<string> Examples { get; set; } = new();
    public List<string> KeyFactors { get; set; } = new();  // What makes these succeed?
    public Dictionary<string, double> MetricImpact { get; set; } = new();  // metric → avg score
    public DateTime FirstSeen { get; set; }
    public DateTime LastSeen { get; set; }
}

/// <summary>
/// Drift analysis result
/// </summary>
public class DriftAnalysis
{
    public string PromptId { get; set; } = string.Empty;
    public string MetricName { get; set; } = string.Empty;

    // Baseline statistics
    public double BaselineMean { get; set; }
    public double BaselineStdDev { get; set; }
    public int BaselineSampleSize { get; set; }

    // Current statistics
    public double CurrentMean { get; set; }
    public double CurrentStdDev { get; set; }
    public int CurrentSampleSize { get; set; }

    // Drift metrics
    public double AbsoluteChange { get; set; }
    public double PercentChange { get; set; }
    public bool HasDrift { get; set; }
    public string DriftDirection { get; set; } = "none";  // "improving", "degrading", "stable"
    public double DriftMagnitude { get; set; }  // Number of standard deviations

    // Statistical significance
    public double PValue { get; set; }
    public bool IsSignificant { get; set; }  // p < 0.05
    public double EffectSize { get; set; }  // Cohen's d

    // Recommendations
    public List<string> Recommendations { get; set; } = new();
}

/// <summary>
/// Pattern evolution over time
/// </summary>
public class PatternEvolution
{
    public string PromptId { get; set; } = string.Empty;
    public string PatternType { get; set; } = string.Empty;  // "failure" | "success"
    public List<PatternSnapshot> Timeline { get; set; } = new();

    /// <summary>
    /// Overall trends
    /// </summary>
    public PatternTrends Trends { get; set; } = new();
}

/// <summary>
/// Pattern snapshot at a point in time
/// </summary>
public class PatternSnapshot
{
    public DateTime Timestamp { get; set; }
    public List<string> ActivePatterns { get; set; } = new();  // Pattern descriptions
    public Dictionary<string, double> PatternFrequencies { get; set; } = new();
    public int TotalCases { get; set; }
}

/// <summary>
/// Overall pattern trends
/// </summary>
public class PatternTrends
{
    public List<string> EmergingPatterns { get; set; } = new();  // New patterns appearing
    public List<string> DecliningPatterns { get; set; } = new();  // Patterns disappearing
    public List<string> StablePatterns { get; set; } = new();  // Consistent patterns
    public bool IsImproving { get; set; }  // Overall trend direction
    public double ImprovementRate { get; set; }  // % improvement per time period
}
