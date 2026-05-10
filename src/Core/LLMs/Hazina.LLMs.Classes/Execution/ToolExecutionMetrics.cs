using System.Collections.Concurrent;

namespace Hazina.LLMs.Execution;

/// <summary>
/// Collects metrics about tool execution
/// </summary>
public class ToolExecutionMetrics
{
    private readonly ConcurrentDictionary<string, ToolMetrics> _toolMetrics = new();

    /// <summary>
    /// Record a successful execution
    /// </summary>
    public void RecordSuccess(string toolName, TimeSpan duration)
    {
        var metrics = _toolMetrics.GetOrAdd(toolName, _ => new ToolMetrics(toolName));
        metrics.RecordSuccess(duration);
    }

    /// <summary>
    /// Record a failed execution
    /// </summary>
    public void RecordFailure(string toolName, TimeSpan duration, string errorType)
    {
        var metrics = _toolMetrics.GetOrAdd(toolName, _ => new ToolMetrics(toolName));
        metrics.RecordFailure(duration, errorType);
    }

    /// <summary>
    /// Record a validation failure
    /// </summary>
    public void RecordValidationFailure(string toolName)
    {
        var metrics = _toolMetrics.GetOrAdd(toolName, _ => new ToolMetrics(toolName));
        metrics.RecordValidationFailure();
    }

    /// <summary>
    /// Get metrics for a specific tool
    /// </summary>
    public ToolMetrics? GetMetrics(string toolName)
    {
        _toolMetrics.TryGetValue(toolName, out var metrics);
        return metrics;
    }

    /// <summary>
    /// Get all tool metrics
    /// </summary>
    public IEnumerable<ToolMetrics> GetAllMetrics()
    {
        return _toolMetrics.Values.ToList();
    }

    /// <summary>
    /// Clear all metrics
    /// </summary>
    public void Clear()
    {
        _toolMetrics.Clear();
    }

    /// <summary>
    /// Get summary statistics
    /// </summary>
    public MetricsSummary GetSummary()
    {
        var allMetrics = _toolMetrics.Values.ToList();

        return new MetricsSummary
        {
            TotalTools = allMetrics.Count,
            TotalExecutions = allMetrics.Sum(m => m.TotalExecutions),
            TotalSuccesses = allMetrics.Sum(m => m.SuccessCount),
            TotalFailures = allMetrics.Sum(m => m.FailureCount),
            TotalValidationFailures = allMetrics.Sum(m => m.ValidationFailureCount),
            AverageDuration = allMetrics.Any()
                ? TimeSpan.FromMilliseconds(allMetrics.Average(m => m.AverageDuration.TotalMilliseconds))
                : TimeSpan.Zero,
            ToolMetrics = allMetrics
        };
    }
}

/// <summary>
/// Metrics for a single tool
/// </summary>
public class ToolMetrics
{
    private readonly object _lock = new();
    private readonly List<TimeSpan> _durations = new();
    private readonly ConcurrentDictionary<string, int> _errorCounts = new();

    public string ToolName { get; }
    public int SuccessCount { get; private set; }
    public int FailureCount { get; private set; }
    public int ValidationFailureCount { get; private set; }
    public int TotalExecutions => SuccessCount + FailureCount;
    public double SuccessRate => TotalExecutions > 0 ? (double)SuccessCount / TotalExecutions : 0;

    public TimeSpan MinDuration { get; private set; } = TimeSpan.MaxValue;
    public TimeSpan MaxDuration { get; private set; } = TimeSpan.Zero;
    public TimeSpan AverageDuration { get; private set; } = TimeSpan.Zero;

    public DateTime FirstExecutedAt { get; private set; } = DateTime.MinValue;
    public DateTime LastExecutedAt { get; private set; } = DateTime.MinValue;

    public ToolMetrics(string toolName)
    {
        ToolName = toolName;
    }

    public void RecordSuccess(TimeSpan duration)
    {
        lock (_lock)
        {
            SuccessCount++;
            RecordDuration(duration);
        }
    }

    public void RecordFailure(TimeSpan duration, string errorType)
    {
        lock (_lock)
        {
            FailureCount++;
            _errorCounts.AddOrUpdate(errorType, 1, (_, count) => count + 1);
            RecordDuration(duration);
        }
    }

    public void RecordValidationFailure()
    {
        lock (_lock)
        {
            ValidationFailureCount++;
        }
    }

    private void RecordDuration(TimeSpan duration)
    {
        _durations.Add(duration);

        if (duration < MinDuration) MinDuration = duration;
        if (duration > MaxDuration) MaxDuration = duration;

        AverageDuration = TimeSpan.FromMilliseconds(
            _durations.Average(d => d.TotalMilliseconds));

        var now = DateTime.UtcNow;
        if (FirstExecutedAt == DateTime.MinValue) FirstExecutedAt = now;
        LastExecutedAt = now;
    }

    public Dictionary<string, int> GetErrorCounts() => _errorCounts.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

    public PercentileStats GetPercentileStats()
    {
        lock (_lock)
        {
            if (!_durations.Any())
            {
                return new PercentileStats();
            }

            var sorted = _durations.OrderBy(d => d.TotalMilliseconds).ToList();
            var count = sorted.Count;

            return new PercentileStats
            {
                P50 = sorted[count / 2],
                P90 = sorted[(int)(count * 0.9)],
                P95 = sorted[(int)(count * 0.95)],
                P99 = sorted[(int)(count * 0.99)]
            };
        }
    }
}

/// <summary>
/// Percentile statistics for duration
/// </summary>
public class PercentileStats
{
    public TimeSpan P50 { get; init; }
    public TimeSpan P90 { get; init; }
    public TimeSpan P95 { get; init; }
    public TimeSpan P99 { get; init; }
}

/// <summary>
/// Summary of all metrics
/// </summary>
public class MetricsSummary
{
    public int TotalTools { get; init; }
    public int TotalExecutions { get; init; }
    public int TotalSuccesses { get; init; }
    public int TotalFailures { get; init; }
    public int TotalValidationFailures { get; init; }
    public TimeSpan AverageDuration { get; init; }
    public List<ToolMetrics> ToolMetrics { get; init; } = new();
}
