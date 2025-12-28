using Hazina.LLMs.GoogleADK.Events;
using Microsoft.Extensions.Logging;

namespace Hazina.LLMs.GoogleADK.Core;

/// <summary>
/// Execution context for agent operations.
/// Provides access to state, events, logging, and runtime services.
/// </summary>
public class AgentContext
{
    /// <summary>
    /// Agent state management
    /// </summary>
    public AgentState State { get; set; }

    /// <summary>
    /// Event bus for publishing and subscribing to events
    /// </summary>
    public EventBus EventBus { get; set; }

    /// <summary>
    /// Logger instance for this agent
    /// </summary>
    public ILogger? Logger { get; set; }

    /// <summary>
    /// Cancellation token for this execution
    /// </summary>
    public CancellationToken CancellationToken { get; set; }

    /// <summary>
    /// Parent context if this is a child agent
    /// </summary>
    public AgentContext? ParentContext { get; set; }

    /// <summary>
    /// Service provider for dependency injection
    /// </summary>
    public IServiceProvider? ServiceProvider { get; set; }

    /// <summary>
    /// Additional runtime data that can be attached to the context
    /// </summary>
    public Dictionary<string, object> RuntimeData { get; set; } = new();

    public AgentContext(AgentState state, EventBus eventBus, ILogger? logger = null)
    {
        State = state;
        EventBus = eventBus;
        Logger = logger;
        CancellationToken = CancellationToken.None;
    }

    /// <summary>
    /// Create a child context for nested agent execution
    /// </summary>
    public AgentContext CreateChildContext(AgentState childState)
    {
        return new AgentContext(childState, EventBus, Logger)
        {
            ParentContext = this,
            ServiceProvider = ServiceProvider,
            CancellationToken = CancellationToken
        };
    }

    /// <summary>
    /// Emit an event through the event bus
    /// </summary>
    public void EmitEvent<TEvent>(TEvent evt) where TEvent : AgentEvent
    {
        evt.AgentId = State.AgentId;
        evt.SessionId = State.SessionId;
        EventBus.Publish(evt);
    }

    /// <summary>
    /// Emit an event asynchronously
    /// </summary>
    public async Task EmitEventAsync<TEvent>(TEvent evt) where TEvent : AgentEvent
    {
        evt.AgentId = State.AgentId;
        evt.SessionId = State.SessionId;
        await EventBus.PublishAsync(evt);
    }

    /// <summary>
    /// Log a message with context information
    /// </summary>
    public void Log(LogLevel level, string message, params object[] args)
    {
        Logger?.Log(level, $"[{State.AgentName}:{State.AgentId}] {message}", args);
    }

    /// <summary>
    /// Check if cancellation was requested
    /// </summary>
    public void ThrowIfCancellationRequested()
    {
        CancellationToken.ThrowIfCancellationRequested();
    }
}
