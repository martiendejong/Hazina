using System.Collections.Concurrent;
using Hazina.LLMs.GoogleADK.Core;
using Hazina.LLMs.GoogleADK.Events;
using Microsoft.Extensions.Logging;

namespace Hazina.LLMs.GoogleADK.DeveloperUI;

/// <summary>
/// Monitors agent activity for debugging and visualization
/// </summary>
public class AgentMonitor : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, AgentInfo> _agents = new();
    private readonly ConcurrentQueue<AgentEvent> _eventHistory = new();
    private readonly int _maxHistorySize;
    private readonly ILogger? _logger;

    public AgentMonitor(int maxHistorySize = 1000, ILogger? logger = null)
    {
        _maxHistorySize = maxHistorySize;
        _logger = logger;
    }

    /// <summary>
    /// Register an agent for monitoring
    /// </summary>
    public void RegisterAgent(BaseAgent agent)
    {
        var info = new AgentInfo
        {
            AgentId = agent.AgentId,
            Name = agent.Name,
            Status = agent.Status,
            RegisteredAt = DateTime.UtcNow
        };

        _agents[agent.AgentId] = info;

        // Subscribe to agent's event bus
        var context = agent.GetContext();
        if (context.EventBus != null)
        {
            context.EventBus.Subscribe<AgentEvent>(OnAgentEvent);
        }

        _logger?.LogInformation("Registered agent for monitoring: {AgentId}", agent.AgentId);
    }

    /// <summary>
    /// Unregister an agent
    /// </summary>
    public void UnregisterAgent(string agentId)
    {
        _agents.TryRemove(agentId, out _);
        _logger?.LogInformation("Unregistered agent: {AgentId}", agentId);
    }

    /// <summary>
    /// Get all monitored agents
    /// </summary>
    public List<AgentInfo> GetAgents()
    {
        return _agents.Values.OrderBy(a => a.Name).ToList();
    }

    /// <summary>
    /// Get agent by ID
    /// </summary>
    public AgentInfo? GetAgent(string agentId)
    {
        _agents.TryGetValue(agentId, out var agent);
        return agent;
    }

    /// <summary>
    /// Get event history
    /// </summary>
    public List<AgentEvent> GetEventHistory(int limit = 100)
    {
        return _eventHistory.TakeLast(limit).ToList();
    }

    /// <summary>
    /// Get events for specific agent
    /// </summary>
    public List<AgentEvent> GetAgentEvents(string agentId, int limit = 100)
    {
        return _eventHistory
            .Where(e => e.AgentId == agentId)
            .TakeLast(limit)
            .ToList();
    }

    /// <summary>
    /// Get statistics
    /// </summary>
    public MonitorStatistics GetStatistics()
    {
        return new MonitorStatistics
        {
            TotalAgents = _agents.Count,
            ActiveAgents = _agents.Values.Count(a => a.Status == AgentStatus.Running),
            TotalEvents = _eventHistory.Count,
            EventsByType = _eventHistory
                .GroupBy(e => e.EventType)
                .ToDictionary(g => g.Key, g => g.Count())
        };
    }

    private void OnAgentEvent(AgentEvent evt)
    {
        // Update agent info
        if (_agents.TryGetValue(evt.AgentId, out var agentInfo))
        {
            agentInfo.LastEventAt = evt.Timestamp;
            agentInfo.EventCount++;

            if (evt is AgentStartedEvent)
            {
                agentInfo.Status = AgentStatus.Running;
            }
            else if (evt is AgentCompletedEvent)
            {
                agentInfo.Status = AgentStatus.Completed;
            }
            else if (evt is AgentErrorEvent)
            {
                agentInfo.Status = AgentStatus.Error;
            }
        }

        // Add to history
        _eventHistory.Enqueue(evt);

        // Trim history if needed
        while (_eventHistory.Count > _maxHistorySize)
        {
            _eventHistory.TryDequeue(out _);
        }
    }

    public ValueTask DisposeAsync()
    {
        _agents.Clear();
        _eventHistory.Clear();
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Information about a monitored agent
/// </summary>
public class AgentInfo
{
    public string AgentId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public AgentStatus Status { get; set; }
    public DateTime RegisteredAt { get; set; }
    public DateTime? LastEventAt { get; set; }
    public int EventCount { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Monitor statistics
/// </summary>
public class MonitorStatistics
{
    public int TotalAgents { get; set; }
    public int ActiveAgents { get; set; }
    public int TotalEvents { get; set; }
    public Dictionary<string, int> EventsByType { get; set; } = new();
}
