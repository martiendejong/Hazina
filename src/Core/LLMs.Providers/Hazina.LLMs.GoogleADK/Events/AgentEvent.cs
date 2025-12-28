namespace Hazina.LLMs.GoogleADK.Events;

/// <summary>
/// Base class for all agent events following Google ADK event patterns.
/// Events represent all significant occurrences during agent execution.
/// </summary>
public abstract class AgentEvent
{
    /// <summary>
    /// Unique identifier for this event
    /// </summary>
    public string EventId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Type of event (e.g., "agent.started", "tool.called", "message.received")
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the event occurred
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// ID of the agent that generated this event
    /// </summary>
    public string AgentId { get; set; } = string.Empty;

    /// <summary>
    /// ID of the session this event belongs to
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// Optional metadata associated with this event
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Event emitted when an agent starts execution
/// </summary>
public class AgentStartedEvent : AgentEvent
{
    public AgentStartedEvent()
    {
        EventType = "agent.started";
    }

    public string AgentName { get; set; } = string.Empty;
    public Dictionary<string, object> Configuration { get; set; } = new();
}

/// <summary>
/// Event emitted when an agent completes execution
/// </summary>
public class AgentCompletedEvent : AgentEvent
{
    public AgentCompletedEvent()
    {
        EventType = "agent.completed";
    }

    public bool Success { get; set; }
    public string? Result { get; set; }
    public TimeSpan Duration { get; set; }
}

/// <summary>
/// Event emitted when an agent encounters an error
/// </summary>
public class AgentErrorEvent : AgentEvent
{
    public AgentErrorEvent()
    {
        EventType = "agent.error";
    }

    public string ErrorMessage { get; set; } = string.Empty;
    public string? ErrorType { get; set; }
    public string? StackTrace { get; set; }
}

/// <summary>
/// Event emitted when a tool is called by an agent
/// </summary>
public class ToolCalledEvent : AgentEvent
{
    public ToolCalledEvent()
    {
        EventType = "tool.called";
    }

    public string ToolName { get; set; } = string.Empty;
    public Dictionary<string, object> Arguments { get; set; } = new();
}

/// <summary>
/// Event emitted when a tool returns a result
/// </summary>
public class ToolResultEvent : AgentEvent
{
    public ToolResultEvent()
    {
        EventType = "tool.result";
    }

    public string ToolName { get; set; } = string.Empty;
    public object? Result { get; set; }
    public bool Success { get; set; }
    public TimeSpan Duration { get; set; }
}

/// <summary>
/// Event emitted when a message is sent or received
/// </summary>
public class MessageEvent : AgentEvent
{
    public MessageEvent()
    {
        EventType = "message.received";
    }

    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public List<string>? ImageUrls { get; set; }
}

/// <summary>
/// Event emitted when agent state changes
/// </summary>
public class StateChangedEvent : AgentEvent
{
    public StateChangedEvent()
    {
        EventType = "state.changed";
    }

    public string StateKey { get; set; } = string.Empty;
    public object? OldValue { get; set; }
    public object? NewValue { get; set; }
}

/// <summary>
/// Event emitted during streaming responses
/// </summary>
public class StreamChunkEvent : AgentEvent
{
    public StreamChunkEvent()
    {
        EventType = "stream.chunk";
    }

    public string Chunk { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
}
