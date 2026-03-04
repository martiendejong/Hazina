namespace Hazina.AI.OpenCode.Models;

/// <summary>
/// Aggregated usage statistics for OpenCode service
/// </summary>
public class UsageStatistics
{
    /// <summary>
    /// Total number of API calls
    /// </summary>
    public int TotalCalls { get; set; }

    /// <summary>
    /// Number of successful calls
    /// </summary>
    public int SuccessfulCalls { get; set; }

    /// <summary>
    /// Number of failed calls
    /// </summary>
    public int FailedCalls { get; set; }

    /// <summary>
    /// Success rate percentage (0-100)
    /// </summary>
    public double SuccessRate => TotalCalls > 0 ? (SuccessfulCalls * 100.0 / TotalCalls) : 0;

    /// <summary>
    /// Usage breakdown by agent
    /// </summary>
    public required Dictionary<string, int> CallsByAgent { get; set; }

    /// <summary>
    /// Usage breakdown by operation type
    /// </summary>
    public required Dictionary<string, int> CallsByOperation { get; set; }

    /// <summary>
    /// Average duration in milliseconds
    /// </summary>
    public long AverageDurationMs { get; set; }

    /// <summary>
    /// First call timestamp
    /// </summary>
    public DateTime? FirstCall { get; set; }

    /// <summary>
    /// Most recent call timestamp
    /// </summary>
    public DateTime? LastCall { get; set; }

    /// <summary>
    /// Recent errors (last 10)
    /// </summary>
    public required List<UsageRecord> RecentErrors { get; set; }
}
