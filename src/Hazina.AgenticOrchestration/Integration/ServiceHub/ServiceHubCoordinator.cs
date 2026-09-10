using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Hazina.AgenticOrchestration.Integration.ServiceHub;

/// <summary>
/// Service Hub coordinator for multi-agent orchestration
/// Manages agent lifecycle, work distribution, and state synchronization
/// </summary>
public class ServiceHubCoordinator
{
    private readonly ConcurrentDictionary<string, AgentInstance> _agents = new();
    private readonly ConcurrentDictionary<string, WorkAssignment> _workAssignments = new();
    private readonly SemaphoreSlim _semaphore;
    private readonly int _maxConcurrentAgents;

    public ServiceHubCoordinator(int maxConcurrentAgents = 3)
    {
        _maxConcurrentAgents = maxConcurrentAgents;
        _semaphore = new SemaphoreSlim(maxConcurrentAgents, maxConcurrentAgents);
    }

    /// <summary>
    /// Spawn new agent instance
    /// Returns agent ID for tracking
    /// </summary>
    public async Task<string> SpawnAgentAsync(AgentType type, AgentConfiguration config, CancellationToken cancellationToken = default)
    {
        // Wait for available slot
        await _semaphore.WaitAsync(cancellationToken);

        var agentId = Guid.NewGuid().ToString("N")[..8]; // Short ID

        var agent = new AgentInstance
        {
            AgentId = agentId,
            Type = type,
            Configuration = config,
            State = AgentState.Starting,
            SpawnedAt = DateTime.UtcNow
        };

        _agents[agentId] = agent;

        Console.WriteLine($"[ServiceHub] Spawned agent {agentId} ({type})");

        // Start agent asynchronously
        _ = Task.Run(async () =>
        {
            try
            {
                agent.State = AgentState.Running;
                await ExecuteAgentAsync(agent, cancellationToken);
                agent.State = AgentState.Completed;
                agent.CompletedAt = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                agent.State = AgentState.Failed;
                agent.Error = ex.Message;
                Console.WriteLine($"[ServiceHub] Agent {agentId} failed: {ex.Message}");
            }
            finally
            {
                _semaphore.Release();
            }
        }, cancellationToken);

        return agentId;
    }

    /// <summary>
    /// Execute agent logic
    /// This is where the agent does its work
    /// </summary>
    private async Task ExecuteAgentAsync(AgentInstance agent, CancellationToken cancellationToken)
    {
        Console.WriteLine($"[ServiceHub] Agent {agent.AgentId} executing...");

        // Agent execution logic here
        // This would spawn Claude sessions, execute tasks, etc.
        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken); // Placeholder

        Console.WriteLine($"[ServiceHub] Agent {agent.AgentId} completed");
    }

    /// <summary>
    /// Assign work to agent
    /// Tracks work assignments for coordination
    /// </summary>
    public async Task<bool> AssignWorkAsync(string agentId, string workItemId, object workData)
    {
        if (!_agents.TryGetValue(agentId, out var agent))
        {
            Console.WriteLine($"[ServiceHub] Agent {agentId} not found");
            return false;
        }

        if (agent.State != AgentState.Running)
        {
            Console.WriteLine($"[ServiceHub] Agent {agentId} not running (state: {agent.State})");
            return false;
        }

        var assignment = new WorkAssignment
        {
            AssignmentId = Guid.NewGuid().ToString(),
            AgentId = agentId,
            WorkItemId = workItemId,
            WorkData = workData,
            AssignedAt = DateTime.UtcNow,
            Status = WorkStatus.Assigned
        };

        _workAssignments[assignment.AssignmentId] = assignment;
        agent.CurrentWork = assignment;

        Console.WriteLine($"[ServiceHub] Assigned work {workItemId} to agent {agentId}");

        return true;
    }

    /// <summary>
    /// Get agent status
    /// </summary>
    public AgentInstance? GetAgent(string agentId)
    {
        _agents.TryGetValue(agentId, out var agent);
        return agent;
    }

    /// <summary>
    /// Get all agents
    /// </summary>
    public List<AgentInstance> GetAllAgents()
    {
        return _agents.Values.ToList();
    }

    /// <summary>
    /// Get available capacity
    /// Returns number of agents that can be spawned
    /// </summary>
    public int GetAvailableCapacity()
    {
        return _semaphore.CurrentCount;
    }

    /// <summary>
    /// Shutdown all agents
    /// </summary>
    public async Task ShutdownAsync()
    {
        Console.WriteLine("[ServiceHub] Shutting down all agents...");

        foreach (var agent in _agents.Values.Where(a => a.State == AgentState.Running))
        {
            agent.State = AgentState.Stopped;
        }

        // Wait for all agents to complete
        var timeout = TimeSpan.FromSeconds(30);
        var start = DateTime.UtcNow;

        while (_agents.Values.Any(a => a.State == AgentState.Running))
        {
            if (DateTime.UtcNow - start > timeout)
            {
                Console.WriteLine("[ServiceHub] Shutdown timeout - forcing stop");
                break;
            }

            await Task.Delay(100);
        }

        Console.WriteLine("[ServiceHub] All agents stopped");
    }
}

/// <summary>
/// Agent instance state
/// </summary>
public class AgentInstance
{
    public string AgentId { get; set; } = string.Empty;
    public AgentType Type { get; set; }
    public AgentConfiguration Configuration { get; set; } = new();
    public AgentState State { get; set; }
    public DateTime SpawnedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Error { get; set; }
    public WorkAssignment? CurrentWork { get; set; }
}

/// <summary>
/// Work assignment tracking
/// </summary>
public class WorkAssignment
{
    public string AssignmentId { get; set; } = string.Empty;
    public string AgentId { get; set; } = string.Empty;
    public string WorkItemId { get; set; } = string.Empty;
    public object? WorkData { get; set; }
    public DateTime AssignedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public WorkStatus Status { get; set; }
}

public enum AgentType
{
    ClickUpTask,
    GitHubPR,
    GitHubIssue,
    CodeReview,
    Testing
}

public enum AgentState
{
    Starting,
    Running,
    Completed,
    Failed,
    Stopped
}

public enum WorkStatus
{
    Assigned,
    InProgress,
    Completed,
    Failed
}

public class AgentConfiguration
{
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, string> Parameters { get; set; } = new();
}
