using System.Diagnostics;
using System.Collections.Concurrent;

namespace Hazina.Production.Monitoring.Performance;

/// <summary>
/// Profiles performance of operations
/// </summary>
public class PerformanceProfiler
{
    private readonly ConcurrentDictionary<string, OperationProfile> _profiles = new();
    private readonly ConcurrentDictionary<string, Stopwatch> _activeOperations = new();

    /// <summary>
    /// Start profiling an operation
    /// </summary>
    public void StartOperation(string operationName, string? category = null)
    {
        var key = $"{category ?? "default"}:{operationName}";
        var stopwatch = Stopwatch.StartNew();
        _activeOperations[key] = stopwatch;
    }

    /// <summary>
    /// End profiling an operation
    /// </summary>
    public OperationMetrics EndOperation(
        string operationName,
        string? category = null,
        bool success = true,
        long? memoryUsed = null)
    {
        var key = $"{category ?? "default"}:{operationName}";

        if (!_activeOperations.TryRemove(key, out var stopwatch))
        {
            return new OperationMetrics
            {
                OperationName = operationName,
                Category = category
            };
        }

        stopwatch.Stop();

        var metrics = new OperationMetrics
        {
            OperationName = operationName,
            Category = category,
            Duration = stopwatch.Elapsed,
            Success = success,
            MemoryUsedBytes = memoryUsed ?? GC.GetTotalMemory(false),
            Timestamp = DateTime.UtcNow
        };

        // Update profile
        _profiles.AddOrUpdate(key,
            new OperationProfile
            {
                OperationName = operationName,
                Category = category,
                TotalExecutions = 1,
                SuccessfulExecutions = success ? 1 : 0,
                FailedExecutions = success ? 0 : 1,
                TotalDuration = stopwatch.Elapsed,
                MinDuration = stopwatch.Elapsed,
                MaxDuration = stopwatch.Elapsed,
                Durations = new List<TimeSpan> { stopwatch.Elapsed }
            },
            (k, existing) =>
            {
                existing.TotalExecutions++;
                if (success)
                    existing.SuccessfulExecutions++;
                else
                    existing.FailedExecutions++;

                existing.TotalDuration += stopwatch.Elapsed;
                existing.MinDuration = existing.MinDuration < stopwatch.Elapsed ? existing.MinDuration : stopwatch.Elapsed;
                existing.MaxDuration = existing.MaxDuration > stopwatch.Elapsed ? existing.MaxDuration : stopwatch.Elapsed;
                existing.Durations.Add(stopwatch.Elapsed);

                // Keep only last 1000 samples
                if (existing.Durations.Count > 1000)
                {
                    existing.Durations.RemoveAt(0);
                }

                return existing;
            });

        return metrics;
    }

    /// <summary>
    /// Profile an operation automatically
    /// </summary>
    public async Task<T> ProfileAsync<T>(
        string operationName,
        Func<Task<T>> operation,
        string? category = null)
    {
        var key = $"{category ?? "default"}:{operationName}";
        var stopwatch = Stopwatch.StartNew();
        var memoryBefore = GC.GetTotalMemory(false);
        var success = false;

        try
        {
            var result = await operation();
            success = true;
            return result;
        }
        finally
        {
            stopwatch.Stop();
            var memoryAfter = GC.GetTotalMemory(false);
            var memoryUsed = memoryAfter - memoryBefore;

            EndOperation(operationName, category, success, memoryUsed);
        }
    }

    /// <summary>
    /// Get profile for an operation
    /// </summary>
    public OperationProfile? GetProfile(string operationName, string? category = null)
    {
        var key = $"{category ?? "default"}:{operationName}";
        _profiles.TryGetValue(key, out var profile);
        return profile;
    }

    /// <summary>
    /// Get all profiles
    /// </summary>
    public List<OperationProfile> GetAllProfiles()
    {
        return _profiles.Values.ToList();
    }

    /// <summary>
    /// Get performance report
    /// </summary>
    public PerformanceReport GetReport()
    {
        var report = new PerformanceReport
        {
            Timestamp = DateTime.UtcNow,
            Profiles = _profiles.Values.ToList()
        };

        // Calculate overall statistics
        report.TotalOperations = report.Profiles.Sum(p => p.TotalExecutions);
        report.TotalSuccessful = report.Profiles.Sum(p => p.SuccessfulExecutions);
        report.TotalFailed = report.Profiles.Sum(p => p.FailedExecutions);
        report.SuccessRate = report.TotalOperations > 0
            ? (double)report.TotalSuccessful / report.TotalOperations
            : 0;

        // Find slowest operations
        report.SlowestOperations = report.Profiles
            .OrderByDescending(p => p.AverageDuration)
            .Take(10)
            .ToList();

        return report;
    }

    /// <summary>
    /// Reset all profiles
    /// </summary>
    public void Reset()
    {
        _profiles.Clear();
        _activeOperations.Clear();
    }
}

/// <summary>
/// Operation metrics
/// </summary>
public class OperationMetrics
{
    public string OperationName { get; set; } = string.Empty;
    public string? Category { get; set; }
    public TimeSpan Duration { get; set; }
    public bool Success { get; set; }
    public long MemoryUsedBytes { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Operation profile with aggregated statistics
/// </summary>
public class OperationProfile
{
    public string OperationName { get; set; } = string.Empty;
    public string? Category { get; set; }
    public int TotalExecutions { get; set; }
    public int SuccessfulExecutions { get; set; }
    public int FailedExecutions { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public TimeSpan MinDuration { get; set; }
    public TimeSpan MaxDuration { get; set; }
    public List<TimeSpan> Durations { get; set; } = new();

    public double SuccessRate => TotalExecutions > 0
        ? (double)SuccessfulExecutions / TotalExecutions
        : 0;

    public TimeSpan AverageDuration => TotalExecutions > 0
        ? TimeSpan.FromTicks(TotalDuration.Ticks / TotalExecutions)
        : TimeSpan.Zero;

    public TimeSpan MedianDuration
    {
        get
        {
            if (Durations.Count == 0)
                return TimeSpan.Zero;

            var sorted = Durations.OrderBy(d => d).ToList();
            var mid = sorted.Count / 2;
            return sorted[mid];
        }
    }

    public TimeSpan P95Duration
    {
        get
        {
            if (Durations.Count == 0)
                return TimeSpan.Zero;

            var sorted = Durations.OrderBy(d => d).ToList();
            var index = (int)(sorted.Count * 0.95);
            return sorted[Math.Min(index, sorted.Count - 1)];
        }
    }
}

/// <summary>
/// Performance report
/// </summary>
public class PerformanceReport
{
    public DateTime Timestamp { get; set; }
    public List<OperationProfile> Profiles { get; set; } = new();
    public int TotalOperations { get; set; }
    public int TotalSuccessful { get; set; }
    public int TotalFailed { get; set; }
    public double SuccessRate { get; set; }
    public List<OperationProfile> SlowestOperations { get; set; } = new();

    /// <summary>
    /// Export to markdown
    /// </summary>
    public string ToMarkdown()
    {
        var md = new System.Text.StringBuilder();
        md.AppendLine("# Performance Report");
        md.AppendLine();
        md.AppendLine($"Generated: {Timestamp:yyyy-MM-dd HH:mm:ss UTC}");
        md.AppendLine();

        md.AppendLine("## Summary");
        md.AppendLine();
        md.AppendLine($"- Total Operations: {TotalOperations}");
        md.AppendLine($"- Successful: {TotalSuccessful}");
        md.AppendLine($"- Failed: {TotalFailed}");
        md.AppendLine($"- Success Rate: {SuccessRate:P2}");
        md.AppendLine();

        md.AppendLine("## Slowest Operations");
        md.AppendLine();
        md.AppendLine("| Operation | Avg Duration | P95 | Executions | Success Rate |");
        md.AppendLine("|-----------|--------------|-----|------------|--------------|");

        foreach (var op in SlowestOperations)
        {
            md.AppendLine($"| {op.OperationName} | {op.AverageDuration.TotalMilliseconds:F2}ms | {op.P95Duration.TotalMilliseconds:F2}ms | {op.TotalExecutions} | {op.SuccessRate:P2} |");
        }

        md.AppendLine();
        md.AppendLine("## All Operations");
        md.AppendLine();
        md.AppendLine("| Operation | Min | Avg | Max | P95 | Executions |");
        md.AppendLine("|-----------|-----|-----|-----|-----|------------|");

        foreach (var op in Profiles.OrderBy(p => p.OperationName))
        {
            md.AppendLine($"| {op.OperationName} | {op.MinDuration.TotalMilliseconds:F2}ms | {op.AverageDuration.TotalMilliseconds:F2}ms | {op.MaxDuration.TotalMilliseconds:F2}ms | {op.P95Duration.TotalMilliseconds:F2}ms | {op.TotalExecutions} |");
        }

        return md.ToString();
    }
}
