using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Hazina.App.HazinaCoder.Core.Tools;

/// <summary>
/// Tracks performance metrics for tool execution.
/// Monitors execution time, success rate, error rate, and usage patterns.
/// </summary>
/// <remarks>
/// Improvement #25: Tool Performance Profiling
/// </remarks>
public class ToolPerformanceProfiler
{
    private readonly Dictionary<string, ToolMetrics> _metrics = new();
    private readonly object _lock = new();

    /// <summary>
    /// Start tracking a tool execution
    /// </summary>
    public ToolExecutionContext StartExecution(string toolName)
    {
        return new ToolExecutionContext(this, toolName);
    }

    /// <summary>
    /// Record a completed execution
    /// </summary>
    internal void RecordExecution(string toolName, TimeSpan duration, bool success, string? error = null)
    {
        lock (_lock)
        {
            if (!_metrics.ContainsKey(toolName))
            {
                _metrics[toolName] = new ToolMetrics { ToolName = toolName };
            }

            var metrics = _metrics[toolName];
            metrics.TotalExecutions++;
            metrics.TotalDuration += duration;

            if (success)
            {
                metrics.SuccessCount++;
            }
            else
            {
                metrics.ErrorCount++;
                if (error != null)
                {
                    metrics.RecentErrors.Add(new ToolError
                    {
                        Error = error,
                        Timestamp = DateTime.UtcNow
                    });

                    // Keep only last 10 errors
                    if (metrics.RecentErrors.Count > 10)
                    {
                        metrics.RecentErrors.RemoveAt(0);
                    }
                }
            }

            metrics.LastExecuted = DateTime.UtcNow;

            // Update min/max/avg
            if (duration < metrics.MinDuration || metrics.MinDuration == TimeSpan.Zero)
                metrics.MinDuration = duration;

            if (duration > metrics.MaxDuration)
                metrics.MaxDuration = duration;

            metrics.AverageDuration = TimeSpan.FromMilliseconds(
                metrics.TotalDuration.TotalMilliseconds / metrics.TotalExecutions);
        }
    }

    /// <summary>
    /// Get metrics for a specific tool
    /// </summary>
    public ToolMetrics? GetMetrics(string toolName)
    {
        lock (_lock)
        {
            return _metrics.TryGetValue(toolName, out var metrics) ? metrics : null;
        }
    }

    /// <summary>
    /// Get metrics for all tools, sorted by usage
    /// </summary>
    public IReadOnlyList<ToolMetrics> GetAllMetrics()
    {
        lock (_lock)
        {
            return _metrics.Values.OrderByDescending(m => m.TotalExecutions).ToList();
        }
    }

    /// <summary>
    /// Display performance summary
    /// </summary>
    public void DisplaySummary()
    {
        lock (_lock)
        {
            Console.WriteLine("\n[Tool Performance Summary]");
            Console.WriteLine("──────────────────────────────────────────────────────────");

            foreach (var metrics in _metrics.Values.OrderByDescending(m => m.TotalExecutions))
            {
                var successRate = metrics.TotalExecutions > 0
                    ? (metrics.SuccessCount * 100.0 / metrics.TotalExecutions)
                    : 0;

                Console.WriteLine($"\n{metrics.ToolName}:");
                Console.WriteLine($"  Executions: {metrics.TotalExecutions}");
                Console.WriteLine($"  Success Rate: {successRate:F1}%");
                Console.WriteLine($"  Avg Duration: {metrics.AverageDuration.TotalMilliseconds:F0}ms");
                Console.WriteLine($"  Min/Max: {metrics.MinDuration.TotalMilliseconds:F0}ms / {metrics.MaxDuration.TotalMilliseconds:F0}ms");
                Console.WriteLine($"  Last Executed: {metrics.LastExecuted:yyyy-MM-dd HH:mm:ss}");
            }
        }
    }
}

/// <summary>
/// Metrics for a single tool
/// </summary>
public class ToolMetrics
{
    public string ToolName { get; set; } = string.Empty;
    public int TotalExecutions { get; set; }
    public int SuccessCount { get; set; }
    public int ErrorCount { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public TimeSpan AverageDuration { get; set; }
    public TimeSpan MinDuration { get; set; }
    public TimeSpan MaxDuration { get; set; }
    public DateTime LastExecuted { get; set; }
    public List<ToolError> RecentErrors { get; set; } = new();
}

/// <summary>
/// Recorded tool error
/// </summary>
public class ToolError
{
    public string Error { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Context for tracking a single tool execution
/// </summary>
public class ToolExecutionContext : IDisposable
{
    private readonly ToolPerformanceProfiler _profiler;
    private readonly string _toolName;
    private readonly Stopwatch _stopwatch;
    private bool _success;
    private string? _error;

    internal ToolExecutionContext(ToolPerformanceProfiler profiler, string toolName)
    {
        _profiler = profiler;
        _toolName = toolName;
        _stopwatch = Stopwatch.StartNew();
    }

    /// <summary>
    /// Mark execution as successful
    /// </summary>
    public void Success() => _success = true;

    /// <summary>
    /// Mark execution as failed
    /// </summary>
    public void Fail(string error)
    {
        _success = false;
        _error = error;
    }

    public void Dispose()
    {
        _stopwatch.Stop();
        _profiler.RecordExecution(_toolName, _stopwatch.Elapsed, _success, _error);
    }
}
