using System.Collections.Concurrent;
using Hazina.LLMs.GoogleADK.Core;
using Hazina.LLMs.GoogleADK.DeveloperUI.Models;
using Microsoft.Extensions.Logging;

namespace Hazina.LLMs.GoogleADK.DeveloperUI;

/// <summary>
/// Controls and manages agents for debugging
/// </summary>
public class AgentController
{
    private readonly ConcurrentDictionary<string, BaseAgent> _agents = new();
    private readonly ConcurrentDictionary<string, DebugInfo> _executions = new();
    private readonly ILogger? _logger;

    public AgentController(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Register an agent for control
    /// </summary>
    public void RegisterAgent(BaseAgent agent)
    {
        _agents[agent.AgentId] = agent;
        _logger?.LogInformation("Registered agent for control: {AgentId}", agent.AgentId);
    }

    /// <summary>
    /// Execute agent with debugging
    /// </summary>
    public async Task<DebugInfo> ExecuteWithDebuggingAsync(
        string agentId,
        string input,
        CancellationToken cancellationToken = default)
    {
        if (!_agents.TryGetValue(agentId, out var agent))
        {
            throw new InvalidOperationException($"Agent not found: {agentId}");
        }

        var debugInfo = new DebugInfo
        {
            AgentId = agentId,
            Input = input,
            StartTime = DateTime.UtcNow,
            Status = AgentStatus.Running
        };

        _executions[debugInfo.ExecutionId] = debugInfo;

        try
        {
            var result = await agent.ExecuteAsync(input, cancellationToken);

            debugInfo.Output = result.Output;
            debugInfo.Status = result.Success ? AgentStatus.Completed : AgentStatus.Error;
            debugInfo.EndTime = DateTime.UtcNow;
            debugInfo.Duration = debugInfo.EndTime - debugInfo.StartTime;
            debugInfo.State = agent.GetContext().State.GetSnapshot();

            return debugInfo;
        }
        catch (Exception ex)
        {
            debugInfo.Status = AgentStatus.Error;
            debugInfo.EndTime = DateTime.UtcNow;
            debugInfo.Duration = debugInfo.EndTime - debugInfo.StartTime;
            debugInfo.Errors.Add(ex.Message);

            _logger?.LogError(ex, "Error executing agent {AgentId}", agentId);

            return debugInfo;
        }
    }

    /// <summary>
    /// Get agent state
    /// </summary>
    public AgentState? GetAgentState(string agentId)
    {
        if (_agents.TryGetValue(agentId, out var agent))
        {
            return agent.GetContext().State;
        }

        return null;
    }

    /// <summary>
    /// Get agent configuration
    /// </summary>
    public Dictionary<string, object>? GetAgentConfiguration(string agentId)
    {
        if (_agents.TryGetValue(agentId, out var agent))
        {
            return agent.Configuration;
        }

        return null;
    }

    /// <summary>
    /// Update agent configuration
    /// </summary>
    public bool UpdateAgentConfiguration(string agentId, Dictionary<string, object> configuration)
    {
        if (_agents.TryGetValue(agentId, out var agent))
        {
            foreach (var kvp in configuration)
            {
                agent.Configuration[kvp.Key] = kvp.Value;
            }

            _logger?.LogInformation("Updated configuration for agent: {AgentId}", agentId);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Get execution history
    /// </summary>
    public List<DebugInfo> GetExecutionHistory(string? agentId = null, int limit = 50)
    {
        var executions = _executions.Values.AsEnumerable();

        if (!string.IsNullOrEmpty(agentId))
        {
            executions = executions.Where(e => e.AgentId == agentId);
        }

        return executions
            .OrderByDescending(e => e.StartTime)
            .Take(limit)
            .ToList();
    }

    /// <summary>
    /// Get execution by ID
    /// </summary>
    public DebugInfo? GetExecution(string executionId)
    {
        _executions.TryGetValue(executionId, out var execution);
        return execution;
    }

    /// <summary>
    /// Pause agent execution (if supported)
    /// </summary>
    public bool PauseAgent(string agentId)
    {
        if (_agents.TryGetValue(agentId, out var agent))
        {
            agent.GetContext().State.Status = AgentStatus.Paused;
            _logger?.LogInformation("Paused agent: {AgentId}", agentId);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Resume agent execution
    /// </summary>
    public bool ResumeAgent(string agentId)
    {
        if (_agents.TryGetValue(agentId, out var agent))
        {
            agent.GetContext().State.Status = AgentStatus.Idle;
            _logger?.LogInformation("Resumed agent: {AgentId}", agentId);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Get performance profile
    /// </summary>
    public PerformanceProfile? GetPerformanceProfile(string agentId)
    {
        var executions = _executions.Values
            .Where(e => e.AgentId == agentId && e.Duration.HasValue)
            .ToList();

        if (executions.Count == 0)
        {
            return null;
        }

        var durations = executions.Select(e => e.Duration!.Value).ToList();

        return new PerformanceProfile
        {
            AgentId = agentId,
            TotalExecutions = executions.Count,
            AverageExecutionTime = TimeSpan.FromMilliseconds(durations.Average(d => d.TotalMilliseconds)),
            MinExecutionTime = durations.Min(),
            MaxExecutionTime = durations.Max(),
            SuccessRate = (double)executions.Count(e => e.Status == AgentStatus.Completed) / executions.Count
        };
    }
}
