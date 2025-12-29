# Production Monitoring Guide

## Overview

Hazina's production monitoring provides comprehensive metrics, performance profiling, and diagnostics for production deployments.

## Features

- Real-time metrics collection
- Performance profiling
- System diagnostics and health checks
- Prometheus-compatible exports
- Memory and CPU monitoring
- Garbage collection tracking

## Quick Start

```csharp
using Hazina.Production.Monitoring.Metrics;
using Hazina.Production.Monitoring.Performance;
using Hazina.Production.Monitoring.Diagnostics;

// Setup collectors
var metrics = new MetricsCollector();
var profiler = new PerformanceProfiler();
var diagnostics = new DiagnosticsCollector();

// Collect metrics
metrics.IncrementCounter("requests_total");
metrics.SetGauge("active_connections", 42);

// Profile operations
await profiler.ProfileAsync("database_query", async () =>
{
    return await ExecuteDatabaseQueryAsync();
});

// Get health check
var health = diagnostics.GetHealthCheck();
Console.WriteLine($"Health: {health.Status}");
```

## Metrics Collection

### Counter Metrics

Track cumulative values:

```csharp
var metrics = new MetricsCollector();

// Simple counter
metrics.IncrementCounter("requests_total");

// Counter with value
metrics.IncrementCounter("bytes_sent", 1024);

// Counter with tags
metrics.IncrementCounter("http_requests", 1, new Dictionary<string, string>
{
    ["method"] = "GET",
    ["path"] = "/api/users",
    ["status"] = "200"
});
```

### Gauge Metrics

Track current values:

```csharp
// Set gauge value
metrics.SetGauge("memory_usage_mb", 512.5);

// With tags
metrics.SetGauge("queue_size", 150, new Dictionary<string, string>
{
    ["queue_name"] = "processing",
    ["priority"] = "high"
});
```

### Histogram Metrics

Track distributions:

```csharp
// Record values
metrics.RecordHistogram("response_time_ms", 45.3);
metrics.RecordHistogram("response_time_ms", 123.7);
metrics.RecordHistogram("response_time_ms", 89.2);

// With tags
metrics.RecordHistogram("request_size_bytes", 2048, new Dictionary<string, string>
{
    ["endpoint"] = "/api/upload"
});

// Get statistics
var snapshot = metrics.GetSnapshot();
foreach (var histogram in snapshot.Histograms)
{
    Console.WriteLine($"{histogram.Name}:");
    Console.WriteLine($"  Count: {histogram.Count}");
    Console.WriteLine($"  Mean: {histogram.Mean:F2}");
    Console.WriteLine($"  P95: {histogram.P95:F2}");
    Console.WriteLine($"  P99: {histogram.P99:F2}");
}
```

### Timer Metrics

Time operations:

```csharp
// Manual timing
metrics.StartTimer("operation_name");
// ... do work ...
var duration = metrics.StopTimer("operation_name");
Console.WriteLine($"Took: {duration.TotalMilliseconds}ms");

// Automatic timing
var result = await metrics.TimeOperationAsync(
    "api_call",
    async () => await MakeApiCallAsync(),
    tags: new Dictionary<string, string> { ["endpoint"] = "/api/data" }
);

// Records:
// - api_call.duration_ms (histogram)
// - api_call.total (counter)
// - api_call.success (counter)
// - api_call.failure (counter)
```

## Performance Profiling

### Basic Profiling

```csharp
var profiler = new PerformanceProfiler();

// Start/stop profiling
profiler.StartOperation("data_processing");
// ... do work ...
var metrics = profiler.EndOperation("data_processing", success: true);

Console.WriteLine($"Duration: {metrics.Duration.TotalMilliseconds}ms");
Console.WriteLine($"Memory: {metrics.MemoryUsedBytes / 1024}KB");
```

### Automatic Profiling

```csharp
// Profile with automatic tracking
var result = await profiler.ProfileAsync(
    "complex_operation",
    async () =>
    {
        return await PerformComplexOperationAsync();
    },
    category: "business_logic"
);
```

### Get Profiling Report

```csharp
var report = profiler.GetReport();

Console.WriteLine($"Total Operations: {report.TotalOperations}");
Console.WriteLine($"Success Rate: {report.SuccessRate:P2}");
Console.WriteLine();
Console.WriteLine("Slowest Operations:");

foreach (var op in report.SlowestOperations)
{
    Console.WriteLine($"  {op.OperationName}:");
    Console.WriteLine($"    Avg: {op.AverageDuration.TotalMilliseconds:F2}ms");
    Console.WriteLine($"    P95: {op.P95Duration.TotalMilliseconds:F2}ms");
    Console.WriteLine($"    Executions: {op.TotalExecutions}");
    Console.WriteLine($"    Success Rate: {op.SuccessRate:P2}");
}

// Export to markdown
var markdown = report.ToMarkdown();
File.WriteAllText("performance_report.md", markdown);
```

### Profile Statistics

```csharp
var profile = profiler.GetProfile("operation_name", "category");

if (profile != null)
{
    Console.WriteLine($"Min: {profile.MinDuration.TotalMilliseconds:F2}ms");
    Console.WriteLine($"Avg: {profile.AverageDuration.TotalMilliseconds:F2}ms");
    Console.WriteLine($"Max: {profile.MaxDuration.TotalMilliseconds:F2}ms");
    Console.WriteLine($"Median: {profile.MedianDuration.TotalMilliseconds:F2}ms");
    Console.WriteLine($"P95: {profile.P95Duration.TotalMilliseconds:F2}ms");
    Console.WriteLine($"Success Rate: {profile.SuccessRate:P2}");
}
```

## System Diagnostics

### Health Checks

```csharp
var diagnostics = new DiagnosticsCollector();

// Get health check
var health = diagnostics.GetHealthCheck(new HealthCheckConfig
{
    MaxMemoryMB = 1024,
    MaxThreads = 100,
    MaxGen2CollectionsPerMinute = 10
});

Console.WriteLine($"Status: {health.Status}");
Console.WriteLine($"Healthy: {health.IsHealthy}");

foreach (var check in health.Checks)
{
    Console.WriteLine($"  {check.Name}: {check.Status}");
    Console.WriteLine($"    {check.Message}");
}
```

### System Snapshot

```csharp
var snapshot = diagnostics.GetSnapshot();

Console.WriteLine($"Process: {snapshot.ProcessName} (PID: {snapshot.ProcessId})");
Console.WriteLine($"Uptime: {snapshot.Uptime}");
Console.WriteLine();
Console.WriteLine("Memory:");
Console.WriteLine($"  Working Set: {snapshot.WorkingSetMB:F2} MB");
Console.WriteLine($"  Private: {snapshot.PrivateMemoryMB:F2} MB");
Console.WriteLine($"  GC Total: {snapshot.GCTotalMemoryMB:F2} MB");
Console.WriteLine();
Console.WriteLine("CPU:");
Console.WriteLine($"  Total Time: {snapshot.TotalProcessorTime}");
Console.WriteLine($"  User Time: {snapshot.UserProcessorTime}");
Console.WriteLine();
Console.WriteLine("Threads:");
Console.WriteLine($"  Count: {snapshot.ThreadCount}");
Console.WriteLine();
Console.WriteLine("GC:");
Console.WriteLine($"  Gen 0: {snapshot.Gen0Collections}");
Console.WriteLine($"  Gen 1: {snapshot.Gen1Collections}");
Console.WriteLine($"  Gen 2: {snapshot.Gen2Collections}");
```

### Monitoring Over Time

```csharp
// Monitor for 5 minutes, snapshot every 30 seconds
var snapshots = await diagnostics.MonitorAsync(
    duration: TimeSpan.FromMinutes(5),
    interval: TimeSpan.FromSeconds(30)
);

// Analyze trends
var memoryTrend = snapshots.Select(s => s.WorkingSetMB).ToList();
var avgMemory = memoryTrend.Average();
var maxMemory = memoryTrend.Max();

Console.WriteLine($"Average Memory: {avgMemory:F2} MB");
Console.WriteLine($"Peak Memory: {maxMemory:F2} MB");
```

### Force Garbage Collection

```csharp
var gcResult = diagnostics.ForceGarbageCollection();

Console.WriteLine($"Memory Before: {gcResult.MemoryBefore / (1024 * 1024):F2} MB");
Console.WriteLine($"Memory After: {gcResult.MemoryAfter / (1024 * 1024):F2} MB");
Console.WriteLine($"Memory Freed: {gcResult.MemoryFreedMB:F2} MB ({gcResult.MemoryFreedPercent:F1}%)");
Console.WriteLine($"Collections: Gen0={gcResult.Gen0Collections}, Gen1={gcResult.Gen1Collections}, Gen2={gcResult.Gen2Collections}");
```

## Exporting Metrics

### Prometheus Format

```csharp
var snapshot = metrics.GetSnapshot();
var prometheusFormat = snapshot.ToPrometheusFormat();

// Expose via HTTP endpoint
app.MapGet("/metrics", () => prometheusFormat);
```

Example output:
```
# TYPE requests_total counter
requests_total{method="GET",path="/api/users"} 1523

# TYPE memory_usage_mb gauge
memory_usage_mb 512.5

# TYPE response_time_ms histogram
response_time_ms_count 1000
response_time_ms_min 12.3
response_time_ms_max 456.7
response_time_ms_mean 89.4
response_time_ms_median 78.2
response_time_ms_p95 234.5
response_time_ms_p99 345.6
```

## Integration Examples

### ASP.NET Core Middleware

```csharp
public class MonitoringMiddleware
{
    private readonly RequestDelegate _next;
    private readonly MetricsCollector _metrics;
    private readonly PerformanceProfiler _profiler;

    public MonitoringMiddleware(RequestDelegate next, MetricsCollector metrics, PerformanceProfiler profiler)
    {
        _next = next;
        _metrics = metrics;
        _profiler = profiler;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "/";
        var method = context.Request.Method;

        var result = await _profiler.ProfileAsync(
            "http_request",
            async () =>
            {
                await _next(context);
                return context.Response.StatusCode;
            },
            category: "web"
        );

        var tags = new Dictionary<string, string>
        {
            ["method"] = method,
            ["path"] = path,
            ["status"] = context.Response.StatusCode.ToString()
        };

        _metrics.IncrementCounter("http_requests_total", 1, tags);
        _metrics.RecordHistogram("http_request_duration_ms", result.Duration.TotalMilliseconds, tags);
    }
}
```

### Background Health Monitoring

```csharp
public class HealthMonitorService : BackgroundService
{
    private readonly DiagnosticsCollector _diagnostics;
    private readonly ILogger _logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var health = _diagnostics.GetHealthCheck();

            if (!health.IsHealthy)
            {
                _logger.LogWarning("System unhealthy:");
                foreach (var check in health.Checks.Where(c => c.Status != HealthStatus.Healthy))
                {
                    _logger.LogWarning($"  {check.Name}: {check.Status} - {check.Message}");
                }
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
```

### Automated Performance Reports

```csharp
public class PerformanceReportService
{
    private readonly PerformanceProfiler _profiler;

    public async Task GenerateDailyReportAsync()
    {
        var report = _profiler.GetReport();
        var markdown = report.ToMarkdown();

        // Save to file
        var filename = $"performance_report_{DateTime.UtcNow:yyyy-MM-dd}.md";
        await File.WriteAllTextAsync(filename, markdown);

        // Send notification if performance degraded
        if (report.SuccessRate < 0.95)
        {
            await SendAlertAsync($"Success rate below threshold: {report.SuccessRate:P2}");
        }

        // Reset for next period
        _profiler.Reset();
    }
}
```

## Best Practices

1. **Metric Naming**: Use consistent naming conventions (e.g., `operation_noun_unit`)
2. **Tags**: Use tags for dimensions, but don't create too many unique combinations
3. **Sampling**: For high-frequency operations, consider sampling
4. **Aggregation**: Aggregate metrics before exporting to reduce overhead
5. **Health Checks**: Run health checks periodically, not on every request
6. **Profiling**: Profile in production, but be mindful of overhead
7. **Alerts**: Set up alerts for critical metrics (memory, error rates)

## Performance Considerations

```csharp
// Good: Measure at appropriate granularity
await profiler.ProfileAsync("api_endpoint", async () => await ExecuteApi());

// Avoid: Too fine-grained profiling
// profiler.ProfileAsync("loop_iteration", ...) // Don't do this in tight loops

// Good: Use tags for categorization
metrics.IncrementCounter("requests", 1, tags: new Dictionary<string, string>
{
    ["endpoint"] = "/api/users"
});

// Avoid: Creating unique metric names for each value
// metrics.IncrementCounter($"requests_api_users_{userId}"); // Don't do this

// Good: Periodic snapshots
var snapshot = metrics.GetSnapshot();  // Call every minute

// Avoid: Snapshot on every request
// app.Use(async (context, next) => {
//     var snapshot = metrics.GetSnapshot();  // Don't do this
// });
```

## Complete Example

```csharp
// Setup
var metrics = new MetricsCollector();
var profiler = new PerformanceProfiler();
var diagnostics = new DiagnosticsCollector();

// Application code
public async Task<Result> ProcessRequestAsync(Request request)
{
    // Profile the operation
    return await profiler.ProfileAsync(
        "process_request",
        async () =>
        {
            // Track metrics
            metrics.IncrementCounter("requests_received");

            try
            {
                // Process
                var result = await ActualProcessingAsync(request);

                // Success metrics
                metrics.IncrementCounter("requests_successful");
                metrics.RecordHistogram("request_size_bytes", request.Size);

                return result;
            }
            catch (Exception ex)
            {
                // Error metrics
                metrics.IncrementCounter("requests_failed", tags: new Dictionary<string, string>
                {
                    ["error_type"] = ex.GetType().Name
                });
                throw;
            }
        },
        category: "business_logic"
    );
}

// Monitoring endpoint
app.MapGet("/health", () =>
{
    var health = diagnostics.GetHealthCheck();
    return health.IsHealthy ? Results.Ok(health) : Results.StatusCode(503);
});

app.MapGet("/metrics", () =>
{
    var snapshot = metrics.GetSnapshot();
    return Results.Text(snapshot.ToPrometheusFormat(), "text/plain");
});

app.MapGet("/performance", () =>
{
    var report = profiler.GetReport();
    return Results.Text(report.ToMarkdown(), "text/markdown");
});

app.MapGet("/diagnostics", () =>
{
    var snapshot = diagnostics.GetSnapshot();
    return Results.Ok(snapshot);
});
```
