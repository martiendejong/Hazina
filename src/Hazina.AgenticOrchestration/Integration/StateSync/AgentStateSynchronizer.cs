using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Hazina.AgenticOrchestration.Integration.StateSync;

/// <summary>
/// Agent state synchronization
/// Enables multiple agents to share state and coordinate work
/// Prevents duplicate work and enables collaboration
/// </summary>
public class AgentStateSynchronizer
{
    private readonly ConcurrentDictionary<string, AgentState> _agentStates = new();
    private readonly ConcurrentDictionary<string, WorkLock> _workLocks = new();
    private readonly TimeSpan _lockTimeout = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Register agent state
    /// Called when agent starts
    /// </summary>
    public async Task RegisterAgentAsync(string agentId, AgentMetadata metadata)
    {
        var state = new AgentState
        {
            AgentId = agentId,
            Metadata = metadata,
            Status = AgentStatus.Active,
            RegisteredAt = DateTime.UtcNow,
            LastHeartbeat = DateTime.UtcNow
        };

        _agentStates[agentId] = state;

        Console.WriteLine($"[StateSync] Registered agent {agentId} ({metadata.Type})");

        await Task.CompletedTask;
    }

    /// <summary>
    /// Update agent heartbeat
    /// Agents should call this periodically to indicate they're still alive
    /// </summary>
    public async Task HeartbeatAsync(string agentId)
    {
        if (_agentStates.TryGetValue(agentId, out var state))
        {
            state.LastHeartbeat = DateTime.UtcNow;
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Acquire lock on work item
    /// Prevents multiple agents from working on the same item
    /// Returns true if lock acquired, false if already locked by another agent
    /// </summary>
    public async Task<bool> AcquireWorkLockAsync(string agentId, string workItemId)
    {
        // Check for existing lock
        if (_workLocks.TryGetValue(workItemId, out var existingLock))
        {
            // Check if lock expired
            if (DateTime.UtcNow - existingLock.AcquiredAt > _lockTimeout)
            {
                Console.WriteLine($"[StateSync] Lock on {workItemId} expired, releasing");
                _workLocks.TryRemove(workItemId, out _);
            }
            else if (existingLock.AgentId != agentId)
            {
                Console.WriteLine($"[StateSync] Work item {workItemId} locked by {existingLock.AgentId}");
                return false;
            }
        }

        // Acquire lock
        var lockObj = new WorkLock
        {
            WorkItemId = workItemId,
            AgentId = agentId,
            AcquiredAt = DateTime.UtcNow
        };

        _workLocks[workItemId] = lockObj;

        Console.WriteLine($"[StateSync] Agent {agentId} acquired lock on {workItemId}");

        await Task.CompletedTask;
        return true;
    }

    /// <summary>
    /// Release work lock
    /// Called when agent completes work
    /// </summary>
    public async Task ReleaseWorkLockAsync(string agentId, string workItemId)
    {
        if (_workLocks.TryGetValue(workItemId, out var lockObj))
        {
            if (lockObj.AgentId == agentId)
            {
                _workLocks.TryRemove(workItemId, out _);
                Console.WriteLine($"[StateSync] Agent {agentId} released lock on {workItemId}");
            }
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Update agent state data
    /// Agents can store arbitrary state for coordination
    /// </summary>
    public async Task UpdateStateAsync(string agentId, string key, object value)
    {
        if (_agentStates.TryGetValue(agentId, out var state))
        {
            state.StateData[key] = value;
            Console.WriteLine($"[StateSync] Agent {agentId} updated state: {key}");
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Get agent state data
    /// </summary>
    public async Task<object?> GetStateAsync(string agentId, string key)
    {
        if (_agentStates.TryGetValue(agentId, out var state))
        {
            state.StateData.TryGetValue(key, out var value);
            return value;
        }

        await Task.CompletedTask;
        return null;
    }

    /// <summary>
    /// Get all active agents
    /// </summary>
    public List<AgentState> GetActiveAgents()
    {
        // Clean up stale agents (no heartbeat in 5 minutes)
        var staleThreshold = DateTime.UtcNow - TimeSpan.FromMinutes(5);

        foreach (var state in _agentStates.Values.ToList())
        {
            if (state.LastHeartbeat < staleThreshold)
            {
                state.Status = AgentStatus.Stale;
                Console.WriteLine($"[StateSync] Agent {state.AgentId} marked as stale");
            }
        }

        return _agentStates.Values
            .Where(s => s.Status == AgentStatus.Active)
            .ToList();
    }

    /// <summary>
    /// Unregister agent
    /// Called when agent completes or stops
    /// </summary>
    public async Task UnregisterAgentAsync(string agentId)
    {
        if (_agentStates.TryGetValue(agentId, out var state))
        {
            state.Status = AgentStatus.Stopped;
            state.StoppedAt = DateTime.UtcNow;

            // Release all locks held by this agent
            foreach (var lockObj in _workLocks.Values.Where(l => l.AgentId == agentId).ToList())
            {
                _workLocks.TryRemove(lockObj.WorkItemId, out _);
                Console.WriteLine($"[StateSync] Released lock {lockObj.WorkItemId} from stopped agent {agentId}");
            }

            Console.WriteLine($"[StateSync] Unregistered agent {agentId}");
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Get synchronization statistics
    /// </summary>
    public SyncStatistics GetStatistics()
    {
        return new SyncStatistics
        {
            TotalAgents = _agentStates.Count,
            ActiveAgents = _agentStates.Values.Count(s => s.Status == AgentStatus.Active),
            StaleAgents = _agentStates.Values.Count(s => s.Status == AgentStatus.Stale),
            ActiveLocks = _workLocks.Count,
            ExpiredLocks = _workLocks.Values.Count(l => DateTime.UtcNow - l.AcquiredAt > _lockTimeout)
        };
    }
}

/// <summary>
/// Agent state
/// </summary>
public class AgentState
{
    public string AgentId { get; set; } = string.Empty;
    public AgentMetadata Metadata { get; set; } = new();
    public AgentStatus Status { get; set; }
    public DateTime RegisteredAt { get; set; }
    public DateTime LastHeartbeat { get; set; }
    public DateTime? StoppedAt { get; set; }
    public Dictionary<string, object> StateData { get; set; } = new();
}

/// <summary>
/// Agent metadata
/// </summary>
public class AgentMetadata
{
    public string Type { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, string> Tags { get; set; } = new();
}

/// <summary>
/// Work lock
/// Prevents duplicate work
/// </summary>
public class WorkLock
{
    public string WorkItemId { get; set; } = string.Empty;
    public string AgentId { get; set; } = string.Empty;
    public DateTime AcquiredAt { get; set; }
}

public enum AgentStatus
{
    Active,
    Stale,
    Stopped
}

/// <summary>
/// Synchronization statistics
/// </summary>
public class SyncStatistics
{
    public int TotalAgents { get; set; }
    public int ActiveAgents { get; set; }
    public int StaleAgents { get; set; }
    public int ActiveLocks { get; set; }
    public int ExpiredLocks { get; set; }
}
