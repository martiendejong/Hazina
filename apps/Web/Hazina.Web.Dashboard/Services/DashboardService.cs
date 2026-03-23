using Hazina.AgenticOrchestration.Integration.ServiceHub;
using Hazina.AgenticOrchestration.Integration.StateSync;
using Hazina.Web.Dashboard.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Hazina.Web.Dashboard.Services;

/// <summary>
/// Dashboard service - aggregates status and broadcasts to clients
/// Central hub for all dashboard data
/// </summary>
public class DashboardService
{
    private readonly IHubContext<DashboardHub> _hubContext;
    private readonly ILogger<DashboardService> _logger;

    // References to Phase 3 components (injected when available)
    private ServiceHubCoordinator? _serviceHub;
    private AgentStateSynchronizer? _stateSynchronizer;

    public DashboardService(
        IHubContext<DashboardHub> hubContext,
        ILogger<DashboardService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>
    /// Register Phase 3 components (called after initialization)
    /// </summary>
    public void RegisterComponents(
        ServiceHubCoordinator? serviceHub = null,
        AgentStateSynchronizer? stateSynchronizer = null)
    {
        _serviceHub = serviceHub;
        _stateSynchronizer = stateSynchronizer;
        _logger.LogInformation("Dashboard components registered");
    }

    /// <summary>
    /// Get current system status snapshot
    /// </summary>
    public async Task<DashboardStatus> GetStatusAsync()
    {
        var status = new DashboardStatus
        {
            Timestamp = DateTime.UtcNow,
            SystemState = SystemState.Running
        };

        // Aggregate from ServiceHub
        if (_serviceHub != null)
        {
            var agents = _serviceHub.GetAllAgents();
            var capacity = _serviceHub.GetAvailableCapacity();

            status.Agents = agents.Select(a => new AgentStatusDto
            {
                AgentId = a.AgentId,
                Type = a.Type.ToString(),
                State = a.State.ToString(),
                SpawnedAt = a.SpawnedAt,
                CurrentWork = a.CurrentWork?.WorkItemId
            }).ToList();

            status.ActiveAgents = agents.Count;
            status.AvailableCapacity = capacity;
            status.MaxConcurrency = 3; // From ServiceHub config
        }

        // Aggregate from StateSynchronizer
        if (_stateSynchronizer != null)
        {
            var syncStats = _stateSynchronizer.GetStatistics();
            status.TotalAgents = syncStats.TotalAgents;
            status.StaleAgents = syncStats.StaleAgents;
            status.ActiveLocks = syncStats.ActiveLocks;
            status.ExpiredLocks = syncStats.ExpiredLocks;
        }

        await Task.CompletedTask;
        return status;
    }

    /// <summary>
    /// Broadcast status update to all connected clients
    /// </summary>
    public async Task BroadcastStatusAsync()
    {
        try
        {
            var status = await GetStatusAsync();
            await _hubContext.Clients.All.SendAsync("StatusUpdate", status);
            _logger.LogDebug("Status broadcast to all clients");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast status update");
        }
    }

    /// <summary>
    /// Broadcast agent event to all clients
    /// </summary>
    public async Task BroadcastAgentEventAsync(string agentId, string eventType, object? data = null)
    {
        try
        {
            var agentEvent = new AgentEventDto
            {
                AgentId = agentId,
                EventType = eventType,
                Timestamp = DateTime.UtcNow,
                Data = data
            };

            await _hubContext.Clients.All.SendAsync("AgentEvent", agentEvent);
            _logger.LogDebug("Agent event broadcast: {AgentId} - {EventType}", agentId, eventType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast agent event");
        }
    }

    /// <summary>
    /// Broadcast metrics update to all clients
    /// </summary>
    public async Task BroadcastMetricsAsync(DashboardMetrics metrics)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("MetricsUpdate", metrics);
            _logger.LogDebug("Metrics broadcast to all clients");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast metrics");
        }
    }
}

/// <summary>
/// Dashboard status snapshot
/// </summary>
public class DashboardStatus
{
    public DateTime Timestamp { get; set; }
    public SystemState SystemState { get; set; }
    public int ActiveAgents { get; set; }
    public int TotalAgents { get; set; }
    public int StaleAgents { get; set; }
    public int AvailableCapacity { get; set; }
    public int MaxConcurrency { get; set; }
    public int ActiveLocks { get; set; }
    public int ExpiredLocks { get; set; }
    public List<AgentStatusDto> Agents { get; set; } = new();
}

/// <summary>
/// Agent status DTO
/// </summary>
public class AgentStatusDto
{
    public string AgentId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public DateTime SpawnedAt { get; set; }
    public string? CurrentWork { get; set; }
}

/// <summary>
/// Agent event DTO
/// </summary>
public class AgentEventDto
{
    public string AgentId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public object? Data { get; set; }
}

/// <summary>
/// Dashboard metrics
/// </summary>
public class DashboardMetrics
{
    public DateTime Timestamp { get; set; }
    public long TotalEventsProcessed { get; set; }
    public long EventsPerMinute { get; set; }
    public long TotalAgentsSpawned { get; set; }
    public long SuccessfulCompletions { get; set; }
    public long FailedCompletions { get; set; }
    public double AverageExecutionTime { get; set; }
    public double SystemUptime { get; set; }
}

/// <summary>
/// System state enum
/// </summary>
public enum SystemState
{
    Starting,
    Running,
    Paused,
    Stopping,
    Stopped,
    Error
}
