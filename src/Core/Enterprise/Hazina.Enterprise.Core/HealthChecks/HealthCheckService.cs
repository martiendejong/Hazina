using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Hazina.Enterprise.Core.HealthChecks;

/// <summary>
/// Aggregated health report
/// </summary>
public class HealthReport
{
    /// <summary>
    /// Overall health status
    /// </summary>
    public HealthStatus Status { get; set; }

    /// <summary>
    /// Individual health check results
    /// </summary>
    public Dictionary<string, HealthCheckResult> Entries { get; set; } = new();

    /// <summary>
    /// Total duration of all health checks
    /// </summary>
    public TimeSpan TotalDuration { get; set; }

    /// <summary>
    /// Timestamp of the report
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Service for managing and executing health checks
/// </summary>
public class HealthCheckService
{
    private readonly List<IHealthCheck> _healthChecks = new();
    private readonly ILogger<HealthCheckService> _logger;

    public HealthCheckService(ILogger<HealthCheckService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Register a health check
    /// </summary>
    public void RegisterHealthCheck(IHealthCheck healthCheck)
    {
        if (healthCheck == null)
            throw new ArgumentNullException(nameof(healthCheck));

        _healthChecks.Add(healthCheck);
        _logger.LogInformation("[HEALTH CHECK SERVICE] Registered health check: {Name}", healthCheck.Name);
    }

    /// <summary>
    /// Execute all health checks and generate a report
    /// </summary>
    public async Task<HealthReport> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[HEALTH CHECK SERVICE] Starting health check for {Count} components", _healthChecks.Count);

        var sw = Stopwatch.StartNew();
        var report = new HealthReport();

        var tasks = _healthChecks.Select(async healthCheck =>
        {
            var checkSw = Stopwatch.StartNew();
            try
            {
                _logger.LogDebug("[HEALTH CHECK SERVICE] Checking: {Name}", healthCheck.Name);

                var result = await healthCheck.CheckHealthAsync(cancellationToken);
                result.Duration = checkSw.Elapsed;

                _logger.LogInformation("[HEALTH CHECK SERVICE] {Name}: {Status} ({Duration}ms)",
                    healthCheck.Name, result.Status, result.Duration.TotalMilliseconds);

                return (healthCheck.Name, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[HEALTH CHECK SERVICE] Health check failed: {Name}", healthCheck.Name);

                var result = HealthCheckResult.Unhealthy(
                    $"Health check threw an exception: {ex.Message}",
                    ex);
                result.Duration = checkSw.Elapsed;
                return (healthCheck.Name, result);
            }
        });

        var results = await Task.WhenAll(tasks);

        foreach (var (name, result) in results)
        {
            report.Entries[name] = result;
        }

        report.TotalDuration = sw.Elapsed;

        // Determine overall status
        report.Status = DetermineOverallStatus(report.Entries.Values);

        _logger.LogInformation("[HEALTH CHECK SERVICE] Health check complete. Overall Status: {Status}, Duration: {Duration}ms",
            report.Status, report.TotalDuration.TotalMilliseconds);

        return report;
    }

    private HealthStatus DetermineOverallStatus(IEnumerable<HealthCheckResult> results)
    {
        var statuses = results.Select(r => r.Status).ToList();

        if (statuses.Any(s => s == HealthStatus.Unhealthy))
            return HealthStatus.Unhealthy;

        if (statuses.Any(s => s == HealthStatus.Degraded))
            return HealthStatus.Degraded;

        return HealthStatus.Healthy;
    }
}

/// <summary>
/// Basic memory health check
/// </summary>
public class MemoryHealthCheck : IHealthCheck
{
    private readonly long _thresholdBytes;

    public string Name => "Memory";

    public MemoryHealthCheck(long thresholdBytes = 1024 * 1024 * 1024) // 1GB default
    {
        _thresholdBytes = thresholdBytes;
    }

    public Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        var process = Process.GetCurrentProcess();
        var memoryUsed = process.WorkingSet64;
        var memoryUsedMB = memoryUsed / 1024 / 1024;

        var data = new Dictionary<string, object>
        {
            { "memory_used_mb", memoryUsedMB },
            { "threshold_mb", _thresholdBytes / 1024 / 1024 }
        };

        if (memoryUsed > _thresholdBytes)
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                $"Memory usage is high: {memoryUsedMB}MB",
                data));
        }

        return Task.FromResult(HealthCheckResult.Healthy(
            $"Memory usage is normal: {memoryUsedMB}MB",
            data));
    }
}
