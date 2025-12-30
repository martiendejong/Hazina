namespace Hazina.AI.Agents.Tracing;

/// <summary>
/// Comprehensive trace of agent execution for debugging and auditing.
/// Records all tool calls, decisions, and retrieval operations.
/// </summary>
public class AgentTrace
{
    /// <summary>
    /// Unique identifier for this trace
    /// </summary>
    public required string TraceId { get; init; }

    /// <summary>
    /// Agent name
    /// </summary>
    public required string AgentName { get; init; }

    /// <summary>
    /// Original task or goal
    /// </summary>
    public required string Task { get; init; }

    /// <summary>
    /// When tracing started
    /// </summary>
    public DateTime StartTime { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// When tracing ended
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// Ordered list of trace events
    /// </summary>
    public List<TraceEvent> Events { get; init; } = new();

    /// <summary>
    /// Metadata for the trace
    /// </summary>
    public Dictionary<string, object>? Metadata { get; init; }

    /// <summary>
    /// Add a tool call event
    /// </summary>
    public void AddToolCall(string toolName, Dictionary<string, object> arguments, string result)
    {
        Events.Add(new ToolCallEvent
        {
            EventId = Guid.NewGuid().ToString(),
            Timestamp = DateTime.UtcNow,
            ToolName = toolName,
            Arguments = arguments,
            Result = result
        });
    }

    /// <summary>
    /// Add a decision event
    /// </summary>
    public void AddDecision(string decision, string reasoning, Dictionary<string, object>? context = null)
    {
        Events.Add(new DecisionEvent
        {
            EventId = Guid.NewGuid().ToString(),
            Timestamp = DateTime.UtcNow,
            Decision = decision,
            Reasoning = reasoning,
            Context = context
        });
    }

    /// <summary>
    /// Add a retrieval event
    /// </summary>
    public void AddRetrieval(string query, List<string> retrievedChunkIds, string rerankerUsed)
    {
        Events.Add(new RetrievalEvent
        {
            EventId = Guid.NewGuid().ToString(),
            Timestamp = DateTime.UtcNow,
            Query = query,
            RetrievedChunkIds = retrievedChunkIds,
            RerankerUsed = rerankerUsed
        });
    }

    /// <summary>
    /// Add a custom log event
    /// </summary>
    public void AddLog(string message, string level = "Info")
    {
        Events.Add(new LogEvent
        {
            EventId = Guid.NewGuid().ToString(),
            Timestamp = DateTime.UtcNow,
            Message = message,
            Level = level
        });
    }

    /// <summary>
    /// Get all tool calls in the trace
    /// </summary>
    public List<ToolCallEvent> GetToolCalls() =>
        Events.OfType<ToolCallEvent>().ToList();

    /// <summary>
    /// Get all decisions in the trace
    /// </summary>
    public List<DecisionEvent> GetDecisions() =>
        Events.OfType<DecisionEvent>().ToList();

    /// <summary>
    /// Get all retrievals in the trace
    /// </summary>
    public List<RetrievalEvent> GetRetrievals() =>
        Events.OfType<RetrievalEvent>().ToList();

    /// <summary>
    /// Calculate total duration
    /// </summary>
    public TimeSpan? GetDuration() =>
        EndTime.HasValue ? EndTime.Value - StartTime : null;
}

/// <summary>
/// Base class for trace events
/// </summary>
public abstract class TraceEvent
{
    public required string EventId { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public abstract string EventType { get; }
}

/// <summary>
/// Tool call event
/// </summary>
public class ToolCallEvent : TraceEvent
{
    public override string EventType => "ToolCall";
    public required string ToolName { get; init; }
    public required Dictionary<string, object> Arguments { get; init; }
    public required string Result { get; init; }
}

/// <summary>
/// Decision event
/// </summary>
public class DecisionEvent : TraceEvent
{
    public override string EventType => "Decision";
    public required string Decision { get; init; }
    public required string Reasoning { get; init; }
    public Dictionary<string, object>? Context { get; init; }
}

/// <summary>
/// Retrieval event
/// </summary>
public class RetrievalEvent : TraceEvent
{
    public override string EventType => "Retrieval";
    public required string Query { get; init; }
    public required List<string> RetrievedChunkIds { get; init; }
    public required string RerankerUsed { get; init; }
}

/// <summary>
/// Log event
/// </summary>
public class LogEvent : TraceEvent
{
    public override string EventType => "Log";
    public required string Message { get; init; }
    public required string Level { get; init; }
}
