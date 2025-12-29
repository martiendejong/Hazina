using System.Diagnostics;

namespace Hazina.Production.Monitoring.Diagnostics;

/// <summary>
/// Collects system diagnostics and health information
/// </summary>
public class DiagnosticsCollector
{
    private readonly Process _currentProcess;

    public DiagnosticsCollector()
    {
        _currentProcess = Process.GetCurrentProcess();
    }

    /// <summary>
    /// Get current diagnostics snapshot
    /// </summary>
    public DiagnosticsSnapshot GetSnapshot()
    {
        _currentProcess.Refresh();

        var snapshot = new DiagnosticsSnapshot
        {
            Timestamp = DateTime.UtcNow,

            // Memory
            WorkingSetBytes = _currentProcess.WorkingSet64,
            PrivateMemoryBytes = _currentProcess.PrivateMemorySize64,
            VirtualMemoryBytes = _currentProcess.VirtualMemorySize64,
            GCTotalMemory = GC.GetTotalMemory(false),

            // CPU
            TotalProcessorTime = _currentProcess.TotalProcessorTime,
            UserProcessorTime = _currentProcess.UserProcessorTime,

            // Threads
            ThreadCount = _currentProcess.Threads.Count,

            // GC
            Gen0Collections = GC.CollectionCount(0),
            Gen1Collections = GC.CollectionCount(1),
            Gen2Collections = GC.CollectionCount(2),

            // Process
            ProcessId = _currentProcess.Id,
            ProcessName = _currentProcess.ProcessName,
            StartTime = _currentProcess.StartTime,
            Uptime = DateTime.UtcNow - _currentProcess.StartTime
        };

        return snapshot;
    }

    /// <summary>
    /// Get health check
    /// </summary>
    public HealthCheckResult GetHealthCheck(HealthCheckConfig? config = null)
    {
        config ??= new HealthCheckConfig();
        var snapshot = GetSnapshot();
        var result = new HealthCheckResult
        {
            Timestamp = DateTime.UtcNow,
            Status = HealthStatus.Healthy,
            Checks = new List<HealthCheck>()
        };

        // Memory check
        var memoryUsageMB = snapshot.WorkingSetBytes / (1024.0 * 1024.0);
        result.Checks.Add(new HealthCheck
        {
            Name = "Memory Usage",
            Status = memoryUsageMB < config.MaxMemoryMB ? HealthStatus.Healthy : HealthStatus.Unhealthy,
            Message = $"{memoryUsageMB:F2} MB / {config.MaxMemoryMB} MB",
            Value = memoryUsageMB
        });

        // Thread count check
        result.Checks.Add(new HealthCheck
        {
            Name = "Thread Count",
            Status = snapshot.ThreadCount < config.MaxThreads ? HealthStatus.Healthy : HealthStatus.Warning,
            Message = $"{snapshot.ThreadCount} threads",
            Value = snapshot.ThreadCount
        });

        // GC check
        var gen2Rate = snapshot.Gen2Collections / Math.Max(1, snapshot.Uptime.TotalMinutes);
        result.Checks.Add(new HealthCheck
        {
            Name = "GC Gen2 Rate",
            Status = gen2Rate < config.MaxGen2CollectionsPerMinute ? HealthStatus.Healthy : HealthStatus.Warning,
            Message = $"{gen2Rate:F2} collections/min",
            Value = gen2Rate
        });

        // Overall status is worst of individual checks
        result.Status = result.Checks.Max(c => c.Status);

        return result;
    }

    /// <summary>
    /// Monitor diagnostics over time
    /// </summary>
    public async Task<List<DiagnosticsSnapshot>> MonitorAsync(
        TimeSpan duration,
        TimeSpan interval,
        CancellationToken cancellationToken = default)
    {
        var snapshots = new List<DiagnosticsSnapshot>();
        var endTime = DateTime.UtcNow + duration;

        while (DateTime.UtcNow < endTime && !cancellationToken.IsCancellationRequested)
        {
            snapshots.Add(GetSnapshot());
            await Task.Delay(interval, cancellationToken);
        }

        return snapshots;
    }

    /// <summary>
    /// Force garbage collection
    /// </summary>
    public GarbageCollectionResult ForceGarbageCollection()
    {
        var memoryBefore = GC.GetTotalMemory(false);
        var gen0Before = GC.CollectionCount(0);
        var gen1Before = GC.CollectionCount(1);
        var gen2Before = GC.CollectionCount(2);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var memoryAfter = GC.GetTotalMemory(false);

        return new GarbageCollectionResult
        {
            MemoryBefore = memoryBefore,
            MemoryAfter = memoryAfter,
            MemoryFreed = memoryBefore - memoryAfter,
            Gen0Collections = GC.CollectionCount(0) - gen0Before,
            Gen1Collections = GC.CollectionCount(1) - gen1Before,
            Gen2Collections = GC.CollectionCount(2) - gen2Before
        };
    }
}

/// <summary>
/// Diagnostics snapshot
/// </summary>
public class DiagnosticsSnapshot
{
    public DateTime Timestamp { get; set; }

    // Memory metrics
    public long WorkingSetBytes { get; set; }
    public long PrivateMemoryBytes { get; set; }
    public long VirtualMemoryBytes { get; set; }
    public long GCTotalMemory { get; set; }

    // CPU metrics
    public TimeSpan TotalProcessorTime { get; set; }
    public TimeSpan UserProcessorTime { get; set; }

    // Thread metrics
    public int ThreadCount { get; set; }

    // GC metrics
    public int Gen0Collections { get; set; }
    public int Gen1Collections { get; set; }
    public int Gen2Collections { get; set; }

    // Process info
    public int ProcessId { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public TimeSpan Uptime { get; set; }

    // Convenience properties
    public double WorkingSetMB => WorkingSetBytes / (1024.0 * 1024.0);
    public double PrivateMemoryMB => PrivateMemoryBytes / (1024.0 * 1024.0);
    public double GCTotalMemoryMB => GCTotalMemory / (1024.0 * 1024.0);
}

/// <summary>
/// Health check configuration
/// </summary>
public class HealthCheckConfig
{
    public double MaxMemoryMB { get; set; } = 1024; // 1 GB
    public int MaxThreads { get; set; } = 100;
    public double MaxGen2CollectionsPerMinute { get; set; } = 10;
}

/// <summary>
/// Health check result
/// </summary>
public class HealthCheckResult
{
    public DateTime Timestamp { get; set; }
    public HealthStatus Status { get; set; }
    public List<HealthCheck> Checks { get; set; } = new();

    public bool IsHealthy => Status == HealthStatus.Healthy;
}

/// <summary>
/// Individual health check
/// </summary>
public class HealthCheck
{
    public string Name { get; set; } = string.Empty;
    public HealthStatus Status { get; set; }
    public string Message { get; set; } = string.Empty;
    public double Value { get; set; }
}

/// <summary>
/// Health status levels
/// </summary>
public enum HealthStatus
{
    Healthy = 0,
    Warning = 1,
    Unhealthy = 2
}

/// <summary>
/// Garbage collection result
/// </summary>
public class GarbageCollectionResult
{
    public long MemoryBefore { get; set; }
    public long MemoryAfter { get; set; }
    public long MemoryFreed { get; set; }
    public int Gen0Collections { get; set; }
    public int Gen1Collections { get; set; }
    public int Gen2Collections { get; set; }

    public double MemoryFreedMB => MemoryFreed / (1024.0 * 1024.0);
    public double MemoryFreedPercent => MemoryBefore > 0
        ? (double)MemoryFreed / MemoryBefore * 100
        : 0;
}
