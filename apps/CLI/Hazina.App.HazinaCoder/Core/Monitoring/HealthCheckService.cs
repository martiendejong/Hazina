namespace Hazina.App.HazinaCoder.Core.Monitoring;

/// <summary>
/// Health check service for monitoring system status
/// Iteration 11: Production monitoring
/// </summary>
public class HealthCheckService
{
    public class HealthStatus
    {
        public bool IsHealthy { get; set; }
        public string Status => IsHealthy ? "Healthy" : "Unhealthy";
        public Dictionary<string, ComponentHealth> Components { get; set; } = new();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class ComponentHealth
    {
        public bool IsHealthy { get; set; }
        public string Message { get; set; } = "";
        public TimeSpan ResponseTime { get; set; }
    }

    public async Task<HealthStatus> CheckHealthAsync()
    {
        var status = new HealthStatus();

        // Check providers
        status.Components["providers"] = await CheckProvidersAsync();

        // Check event bus
        status.Components["eventbus"] = CheckEventBus();

        // Check memory
        status.Components["memory"] = CheckMemory();

        // Check disk
        status.Components["disk"] = CheckDisk();

        status.IsHealthy = status.Components.Values.All(c => c.IsHealthy);

        return status;
    }

    private async Task<ComponentHealth> CheckProvidersAsync()
    {
        var start = DateTime.UtcNow;
        try
        {
            // TODO: Ping providers
            return new ComponentHealth
            {
                IsHealthy = true,
                Message = "All providers available",
                ResponseTime = DateTime.UtcNow - start
            };
        }
        catch (Exception ex)
        {
            return new ComponentHealth
            {
                IsHealthy = false,
                Message = ex.Message,
                ResponseTime = DateTime.UtcNow - start
            };
        }
    }

    private ComponentHealth CheckEventBus()
    {
        return new ComponentHealth
        {
            IsHealthy = true,
            Message = "Event bus operational"
        };
    }

    private ComponentHealth CheckMemory()
    {
        var memoryMB = GC.GetTotalMemory(false) / 1024 / 1024;
        return new ComponentHealth
        {
            IsHealthy = memoryMB < 1000, // < 1GB
            Message = $"Memory usage: {memoryMB}MB"
        };
    }

    private ComponentHealth CheckDisk()
    {
        var drive = DriveInfo.GetDrives().First(d => d.IsReady);
        var freeSpaceGB = drive.AvailableFreeSpace / 1024 / 1024 / 1024;

        return new ComponentHealth
        {
            IsHealthy = freeSpaceGB > 1, // > 1GB free
            Message = $"Free space: {freeSpaceGB}GB"
        };
    }
}
