using Hazina.LLMs.GoogleADK.Events;
using Microsoft.Extensions.Logging;

namespace Hazina.LLMs.GoogleADK.Core;

/// <summary>
/// Base class for all agents in the Google ADK architecture.
/// Provides core lifecycle, state management, and event handling.
/// </summary>
public abstract class BaseAgent
{
    /// <summary>
    /// Agent execution context
    /// </summary>
    protected AgentContext Context { get; private set; }

    /// <summary>
    /// Agent name
    /// </summary>
    public string Name => Context.State.AgentName;

    /// <summary>
    /// Agent ID
    /// </summary>
    public string AgentId => Context.State.AgentId;

    /// <summary>
    /// Current agent status
    /// </summary>
    public AgentStatus Status => Context.State.Status;

    /// <summary>
    /// Agent configuration
    /// </summary>
    public Dictionary<string, object> Configuration => Context.State.Configuration;

    protected BaseAgent(string name, AgentContext? context = null)
    {
        Context = context ?? CreateDefaultContext(name);
        Context.State.AgentName = name;
    }

    /// <summary>
    /// Create a default context if none provided
    /// </summary>
    private static AgentContext CreateDefaultContext(string name)
    {
        var state = new AgentState { AgentName = name };
        var eventBus = new EventBus();
        return new AgentContext(state, eventBus);
    }

    /// <summary>
    /// Initialize the agent. Called before execution starts.
    /// </summary>
    public virtual async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        Context.CancellationToken = cancellationToken;
        Context.State.Status = AgentStatus.Initializing;

        Context.EmitEvent(new AgentStartedEvent
        {
            AgentName = Name,
            Configuration = new Dictionary<string, object>(Configuration)
        });

        await OnInitializeAsync(cancellationToken);

        Context.State.Status = AgentStatus.Idle;
    }

    /// <summary>
    /// Execute the agent with a given input.
    /// This is the main entry point for agent execution.
    /// </summary>
    public async Task<AgentResult> ExecuteAsync(string input, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        Context.CancellationToken = cancellationToken;

        try
        {
            Context.State.Status = AgentStatus.Running;
            Context.State.LastActiveAt = DateTime.UtcNow;

            Context.Log(LogLevel.Information, "Executing agent with input: {Input}", input);

            var result = await OnExecuteAsync(input, cancellationToken);

            Context.State.Status = AgentStatus.Completed;

            var duration = DateTime.UtcNow - startTime;
            Context.EmitEvent(new AgentCompletedEvent
            {
                Success = result.Success,
                Result = result.Output,
                Duration = duration
            });

            return result;
        }
        catch (OperationCanceledException)
        {
            Context.State.Status = AgentStatus.Cancelled;
            Context.Log(LogLevel.Warning, "Agent execution was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            Context.State.Status = AgentStatus.Error;

            Context.EmitEvent(new AgentErrorEvent
            {
                ErrorMessage = ex.Message,
                ErrorType = ex.GetType().Name,
                StackTrace = ex.StackTrace
            });

            Context.Log(LogLevel.Error, "Agent execution failed: {Error}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Execute the agent with structured input
    /// </summary>
    public async Task<AgentResult> ExecuteAsync(AgentInput input, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(input.ToString(), cancellationToken);
    }

    /// <summary>
    /// Pause agent execution
    /// </summary>
    public virtual void Pause()
    {
        if (Context.State.Status == AgentStatus.Running)
        {
            Context.State.Status = AgentStatus.Paused;
            Context.Log(LogLevel.Information, "Agent paused");
        }
    }

    /// <summary>
    /// Resume agent execution
    /// </summary>
    public virtual void Resume()
    {
        if (Context.State.Status == AgentStatus.Paused)
        {
            Context.State.Status = AgentStatus.Running;
            Context.Log(LogLevel.Information, "Agent resumed");
        }
    }

    /// <summary>
    /// Cleanup agent resources
    /// </summary>
    public virtual async Task DisposeAsync()
    {
        await OnDisposeAsync();
        Context.EventBus.Clear();
    }

    /// <summary>
    /// Override to implement initialization logic
    /// </summary>
    protected virtual Task OnInitializeAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Override to implement agent execution logic
    /// </summary>
    protected abstract Task<AgentResult> OnExecuteAsync(string input, CancellationToken cancellationToken);

    /// <summary>
    /// Override to implement cleanup logic
    /// </summary>
    protected virtual Task OnDisposeAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Subscribe to agent events
    /// </summary>
    public void SubscribeToEvent<TEvent>(Action<TEvent> handler) where TEvent : AgentEvent
    {
        Context.EventBus.Subscribe(handler);
    }

    /// <summary>
    /// Update agent configuration
    /// </summary>
    public void UpdateConfiguration(Dictionary<string, object> config)
    {
        foreach (var kvp in config)
        {
            Configuration[kvp.Key] = kvp.Value;
        }
    }

    /// <summary>
    /// Get agent state snapshot
    /// </summary>
    public Dictionary<string, object> GetStateSnapshot()
    {
        return Context.State.GetSnapshot();
    }
}

/// <summary>
/// Input for agent execution
/// </summary>
public class AgentInput
{
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, object> Parameters { get; set; } = new();
    public List<string>? ImageUrls { get; set; }

    public override string ToString()
    {
        return Message;
    }
}

/// <summary>
/// Result of agent execution
/// </summary>
public class AgentResult
{
    public bool Success { get; set; }
    public string Output { get; set; } = string.Empty;
    public Dictionary<string, object> Metadata { get; set; } = new();
    public List<AgentEvent> Events { get; set; } = new();

    public static AgentResult CreateSuccess(string output)
    {
        return new AgentResult { Success = true, Output = output };
    }

    public static AgentResult CreateFailure(string error)
    {
        return new AgentResult { Success = false, Output = error };
    }
}
