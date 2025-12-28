using Hazina.LLMs.GoogleADK.Events;
using Microsoft.Extensions.Logging;

namespace Hazina.LLMs.GoogleADK.Core;

/// <summary>
/// Runtime for managing agent lifecycle, execution, and coordination.
/// Provides factory methods and orchestration for multiple agents.
/// </summary>
public class AgentRuntime
{
    private readonly Dictionary<string, BaseAgent> _agents = new();
    private readonly EventBus _globalEventBus;
    private readonly ILogger<AgentRuntime>? _logger;
    private readonly IServiceProvider? _serviceProvider;
    private readonly object _lock = new();

    public AgentRuntime(ILogger<AgentRuntime>? logger = null, IServiceProvider? serviceProvider = null)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _globalEventBus = new EventBus(logger as ILogger<EventBus>);
    }

    /// <summary>
    /// Register an agent with the runtime
    /// </summary>
    public void RegisterAgent(BaseAgent agent)
    {
        lock (_lock)
        {
            _agents[agent.AgentId] = agent;
            _logger?.LogInformation("Agent {AgentName} ({AgentId}) registered", agent.Name, agent.AgentId);
        }
    }

    /// <summary>
    /// Unregister an agent from the runtime
    /// </summary>
    public void UnregisterAgent(string agentId)
    {
        lock (_lock)
        {
            if (_agents.Remove(agentId, out var agent))
            {
                _logger?.LogInformation("Agent {AgentName} ({AgentId}) unregistered", agent.Name, agentId);
            }
        }
    }

    /// <summary>
    /// Get an agent by ID
    /// </summary>
    public BaseAgent? GetAgent(string agentId)
    {
        lock (_lock)
        {
            return _agents.GetValueOrDefault(agentId);
        }
    }

    /// <summary>
    /// Get all registered agents
    /// </summary>
    public IReadOnlyCollection<BaseAgent> GetAllAgents()
    {
        lock (_lock)
        {
            return _agents.Values.ToList();
        }
    }

    /// <summary>
    /// Create a new agent context with global event bus
    /// </summary>
    public AgentContext CreateContext(string agentName, string? sessionId = null)
    {
        var state = new AgentState
        {
            AgentName = agentName,
            SessionId = sessionId ?? Guid.NewGuid().ToString()
        };

        var context = new AgentContext(state, _globalEventBus, _logger as ILogger)
        {
            ServiceProvider = _serviceProvider
        };

        return context;
    }

    /// <summary>
    /// Execute an agent by ID
    /// </summary>
    public async Task<AgentResult> ExecuteAgentAsync(
        string agentId,
        string input,
        CancellationToken cancellationToken = default)
    {
        var agent = GetAgent(agentId);
        if (agent == null)
        {
            throw new InvalidOperationException($"Agent with ID {agentId} not found");
        }

        return await agent.ExecuteAsync(input, cancellationToken);
    }

    /// <summary>
    /// Execute multiple agents in sequence
    /// </summary>
    public async Task<List<AgentResult>> ExecuteSequentialAsync(
        List<(string agentId, string input)> tasks,
        CancellationToken cancellationToken = default)
    {
        var results = new List<AgentResult>();

        foreach (var (agentId, input) in tasks)
        {
            var result = await ExecuteAgentAsync(agentId, input, cancellationToken);
            results.Add(result);

            // Stop if any agent fails
            if (!result.Success)
            {
                _logger?.LogWarning("Sequential execution stopped at agent {AgentId} due to failure", agentId);
                break;
            }
        }

        return results;
    }

    /// <summary>
    /// Execute multiple agents in parallel
    /// </summary>
    public async Task<List<AgentResult>> ExecuteParallelAsync(
        List<(string agentId, string input)> tasks,
        CancellationToken cancellationToken = default)
    {
        var executionTasks = tasks.Select(async task =>
        {
            try
            {
                return await ExecuteAgentAsync(task.agentId, task.input, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error executing agent {AgentId}", task.agentId);
                return AgentResult.CreateFailure($"Error: {ex.Message}");
            }
        });

        var results = await Task.WhenAll(executionTasks);
        return results.ToList();
    }

    /// <summary>
    /// Subscribe to global events from all agents
    /// </summary>
    public void SubscribeToGlobalEvent<TEvent>(Action<TEvent> handler) where TEvent : AgentEvent
    {
        _globalEventBus.Subscribe(handler);
    }

    /// <summary>
    /// Dispose all registered agents
    /// </summary>
    public async Task DisposeAllAsync()
    {
        var agents = GetAllAgents();
        foreach (var agent in agents)
        {
            await agent.DisposeAsync();
        }

        lock (_lock)
        {
            _agents.Clear();
        }

        _globalEventBus.Clear();
        _logger?.LogInformation("All agents disposed");
    }

    /// <summary>
    /// Get runtime statistics
    /// </summary>
    public RuntimeStatistics GetStatistics()
    {
        lock (_lock)
        {
            var agents = _agents.Values.ToList();
            return new RuntimeStatistics
            {
                TotalAgents = agents.Count,
                RunningAgents = agents.Count(a => a.Status == AgentStatus.Running),
                IdleAgents = agents.Count(a => a.Status == AgentStatus.Idle),
                ErrorAgents = agents.Count(a => a.Status == AgentStatus.Error),
                CompletedAgents = agents.Count(a => a.Status == AgentStatus.Completed)
            };
        }
    }
}

/// <summary>
/// Runtime statistics
/// </summary>
public class RuntimeStatistics
{
    public int TotalAgents { get; set; }
    public int RunningAgents { get; set; }
    public int IdleAgents { get; set; }
    public int ErrorAgents { get; set; }
    public int CompletedAgents { get; set; }
}
