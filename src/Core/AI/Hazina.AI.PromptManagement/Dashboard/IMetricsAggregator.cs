using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Hazina.AI.PromptManagement.Dashboard;

/// <summary>
/// Aggregates metrics for dashboard visualization
/// </summary>
public interface IMetricsAggregator
{
    /// <summary>
    /// Get time series metrics for a prompt
    /// </summary>
    /// <param name="promptId">Prompt ID</param>
    /// <param name="metricName">Metric name (e.g., "avg_Accuracy", "success_rate")</param>
    /// <param name="startDate">Start date</param>
    /// <param name="endDate">End date</param>
    /// <param name="granularity">Time granularity: "hour", "day", "week"</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Time series data points</returns>
    Task<TimeSeriesData> GetTimeSeriesAsync(
        string promptId,
        string metricName,
        DateTime startDate,
        DateTime endDate,
        string granularity = "day",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get metrics comparison between versions
    /// </summary>
    /// <param name="promptId">Prompt ID</param>
    /// <param name="versionIds">Version IDs to compare</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Comparison data</returns>
    Task<VersionComparisonData> CompareVersionsAsync(
        string promptId,
        List<string> versionIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get aggregate statistics for a prompt
    /// </summary>
    /// <param name="promptId">Prompt ID</param>
    /// <param name="versionId">Optional version ID (defaults to current)</param>
    /// <param name="startDate">Start date</param>
    /// <param name="endDate">End date</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Aggregate statistics</returns>
    Task<AggregateStats> GetAggregateStatsAsync(
        string promptId,
        string? versionId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all prompts ranked by performance
    /// </summary>
    /// <param name="metricName">Metric to rank by (e.g., "avg_Accuracy")</param>
    /// <param name="limit">Number of results</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Ranked prompts</returns>
    Task<List<PromptRanking>> GetPromptRankingsAsync(
        string metricName,
        int limit = 20,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Time series data for visualization
/// </summary>
public class TimeSeriesData
{
    public string PromptId { get; set; } = string.Empty;
    public string MetricName { get; set; } = string.Empty;
    public string Granularity { get; set; } = string.Empty;
    public List<DataPoint> Points { get; set; } = new();

    /// <summary>
    /// Statistical summary
    /// </summary>
    public TimeSeriesStats Stats { get; set; } = new();
}

/// <summary>
/// Single data point in time series
/// </summary>
public class DataPoint
{
    public DateTime Timestamp { get; set; }
    public double Value { get; set; }
    public int? SampleSize { get; set; }  // Number of data points in this bucket
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Statistical summary of time series
/// </summary>
public class TimeSeriesStats
{
    public double Mean { get; set; }
    public double Median { get; set; }
    public double StdDev { get; set; }
    public double Min { get; set; }
    public double Max { get; set; }
    public double Trend { get; set; }  // Linear regression slope
    public bool IsDrifting { get; set; }  // Is there significant drift?
    public double? DriftRate { get; set; }  // % change per day
}

/// <summary>
/// Comparison data between versions
/// </summary>
public class VersionComparisonData
{
    public string PromptId { get; set; } = string.Empty;
    public List<VersionMetrics> Versions { get; set; } = new();
    public Dictionary<string, VersionDelta> Deltas { get; set; } = new();  // metric → delta
}

/// <summary>
/// Metrics for a specific version
/// </summary>
public class VersionMetrics
{
    public string VersionId { get; set; } = string.Empty;
    public int VersionNumber { get; set; }
    public DateTime CreatedAt { get; set; }
    public Dictionary<string, double> Metrics { get; set; } = new();
    public int TotalUses { get; set; }
}

/// <summary>
/// Delta between versions for a metric
/// </summary>
public class VersionDelta
{
    public string MetricName { get; set; } = string.Empty;
    public double BaselineValue { get; set; }
    public double NewValue { get; set; }
    public double AbsoluteChange { get; set; }
    public double PercentChange { get; set; }
    public bool IsImprovement { get; set; }
    public bool IsRegression { get; set; }
    public string Severity { get; set; } = "none";  // "none", "low", "medium", "high", "critical"
}

/// <summary>
/// Aggregate statistics
/// </summary>
public class AggregateStats
{
    public string PromptId { get; set; } = string.Empty;
    public string? VersionId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    // Usage stats
    public int TotalUses { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public double SuccessRate { get; set; }

    // Quality metrics
    public Dictionary<string, double> AverageMetrics { get; set; } = new();
    public Dictionary<string, double> MinMetrics { get; set; } = new();
    public Dictionary<string, double> MaxMetrics { get; set; } = new();

    // Performance
    public double AvgLatencyMs { get; set; }
    public double P95LatencyMs { get; set; }
    public double P99LatencyMs { get; set; }

    // Cost
    public long TotalTokens { get; set; }
    public decimal TotalCostUsd { get; set; }
    public decimal AvgCostPerUse { get; set; }

    // Trends
    public Dictionary<string, double> Trends { get; set; } = new();  // metric → trend
}

/// <summary>
/// Prompt ranking entry
/// </summary>
public class PromptRanking
{
    public string PromptId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
    public double MetricValue { get; set; }
    public int Rank { get; set; }
    public int TotalUses { get; set; }
    public double Confidence { get; set; }  // Statistical confidence in ranking
}
