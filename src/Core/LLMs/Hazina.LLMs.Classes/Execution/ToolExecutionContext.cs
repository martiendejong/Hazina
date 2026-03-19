using System.Diagnostics;

namespace Hazina.LLMs.Execution;

/// <summary>
/// Context for tool execution with correlation tracking
/// </summary>
public record ToolExecutionContext
{
    /// <summary>
    /// Unique identifier for this execution
    /// </summary>
    public string ExecutionId { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Correlation ID to track related executions across tool calls
    /// </summary>
    public string CorrelationId { get; init; } = Activity.Current?.Id ?? Guid.NewGuid().ToString("N");

    /// <summary>
    /// Parent execution ID if this is a nested tool call
    /// </summary>
    public string? ParentId { get; init; }

    /// <summary>
    /// Trace ID for distributed tracing (W3C Trace Context compatible)
    /// </summary>
    public string? TraceId { get; init; } = Activity.Current?.TraceId.ToString();

    /// <summary>
    /// Span ID for distributed tracing
    /// </summary>
    public string? SpanId { get; init; } = Activity.Current?.SpanId.ToString();

    /// <summary>
    /// User ID who initiated this execution
    /// </summary>
    public string? UserId { get; init; }

    /// <summary>
    /// Tool name being executed
    /// </summary>
    public string? ToolName { get; init; }

    /// <summary>
    /// Session ID for grouping multiple tool executions
    /// </summary>
    public string? SessionId { get; init; }

    /// <summary>
    /// Timestamp when execution started
    /// </summary>
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Custom metadata for this execution
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new();

    /// <summary>
    /// Create a child context for nested tool execution
    /// </summary>
    public ToolExecutionContext CreateChild(string childToolName)
    {
        return new ToolExecutionContext
        {
            CorrelationId = CorrelationId,
            ParentId = ExecutionId,
            TraceId = TraceId,
            UserId = UserId,
            ToolName = childToolName,
            SessionId = SessionId,
            Metadata = new Dictionary<string, object>(Metadata)
        };
    }

    /// <summary>
    /// Create context from current Activity (for distributed tracing)
    /// </summary>
    public static ToolExecutionContext FromActivity(Activity? activity = null)
    {
        var current = activity ?? Activity.Current;
        return new ToolExecutionContext
        {
            CorrelationId = current?.Id ?? Guid.NewGuid().ToString("N"),
            TraceId = current?.TraceId.ToString(),
            SpanId = current?.SpanId.ToString()
        };
    }

    /// <summary>
    /// Add metadata to context
    /// </summary>
    public ToolExecutionContext WithMetadata(string key, object value)
    {
        Metadata[key] = value;
        return this;
    }

    /// <summary>
    /// Add user to context
    /// </summary>
    public ToolExecutionContext WithUser(string userId)
    {
        return this with { UserId = userId };
    }

    /// <summary>
    /// Add session to context
    /// </summary>
    public ToolExecutionContext WithSession(string sessionId)
    {
        return this with { SessionId = sessionId };
    }

    /// <summary>
    /// Get all correlation properties for logging
    /// </summary>
    public Dictionary<string, object?> GetCorrelationProperties()
    {
        return new Dictionary<string, object?>
        {
            ["ExecutionId"] = ExecutionId,
            ["CorrelationId"] = CorrelationId,
            ["ParentId"] = ParentId,
            ["TraceId"] = TraceId,
            ["SpanId"] = SpanId,
            ["UserId"] = UserId,
            ["ToolName"] = ToolName,
            ["SessionId"] = SessionId,
            ["StartedAt"] = StartedAt
        };
    }
}
