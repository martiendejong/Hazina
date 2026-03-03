using System;

namespace Hazina.App.HazinaCoder.Core.Streaming;

/// <summary>
/// Base class for all agent events that can be streamed.
/// Supports real-time progress feedback via IAsyncEnumerable.
///
/// Improvement #86: IAsyncEnumerable Streaming API (Value: 10/10, Effort: 6/10, Ratio: 1.67)
/// </summary>
public abstract class AgentEvent
{
    public string EventId { get; set; } = Guid.NewGuid().ToString();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public AgentEventType EventType { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Event types for agent activities
/// </summary>
public enum AgentEventType
{
    // Planning events
    PlanningStarted,
    PlanningStep,
    PlanningCompleted,

    // Action events
    ActionStarted,
    ActionProgress,
    ActionCompleted,
    ActionFailed,

    // Tool events
    ToolExecutionStarted,
    ToolExecutionCompleted,
    ToolExecutionFailed,

    // Build/Test events
    BuildStarted,
    BuildProgress,
    BuildCompleted,
    BuildFailed,
    TestStarted,
    TestProgress,
    TestCompleted,
    TestFailed,

    // File events
    FileChanged,
    FileCreated,
    FileDeleted,

    // Token/Cost events
    TokenUsageUpdate,
    CostUpdate,

    // Summary events
    SummaryGenerated,
    SessionCompleted
}

/// <summary>
/// Planning event
/// </summary>
public class PlanningEvent : AgentEvent
{
    public string Step { get; set; } = string.Empty;
    public int StepNumber { get; set; }
    public int TotalSteps { get; set; }
}

/// <summary>
/// Action event
/// </summary>
public class ActionEvent : AgentEvent
{
    public string ActionType { get; set; } = string.Empty;
    public double ProgressPercentage { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Tool execution event
/// </summary>
public class ToolExecutionEvent : AgentEvent
{
    public string ToolName { get; set; } = string.Empty;
    public string? ToolParameters { get; set; }
    public string? ToolResult { get; set; }
    public TimeSpan? ExecutionDuration { get; set; }
}

/// <summary>
/// Build event
/// </summary>
public class BuildEvent : AgentEvent
{
    public string ProjectPath { get; set; } = string.Empty;
    public string OutputLine { get; set; } = string.Empty;
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
    public bool Success { get; set; }
}

/// <summary>
/// Test event
/// </summary>
public class TestEvent : AgentEvent
{
    public string TestName { get; set; } = string.Empty;
    public int TotalTests { get; set; }
    public int PassedTests { get; set; }
    public int FailedTests { get; set; }
    public bool Success { get; set; }
}

/// <summary>
/// File change event
/// </summary>
public class FileChangeEvent : AgentEvent
{
    public string FilePath { get; set; } = string.Empty;
    public FileChangeType ChangeType { get; set; }
    public long FileSizeBytes { get; set; }
}

public enum FileChangeType
{
    Created,
    Modified,
    Deleted,
    Renamed
}

/// <summary>
/// Token usage event
/// Improvement #91: Token/Cost Streaming (Value: 7/10, Effort: 3/10, Ratio: 2.33)
/// </summary>
public class TokenUsageEvent : AgentEvent
{
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int TotalTokens { get; set; }
    public decimal CostUSD { get; set; }
    public string ModelName { get; set; } = string.Empty;
}

/// <summary>
/// Progress event with percentage and ETA
/// Improvement #89: Progress Bars & Status Updates (Value: 8/10, Effort: 4/10, Ratio: 2.00)
/// </summary>
public class ProgressEvent : AgentEvent
{
    public string TaskName { get; set; } = string.Empty;
    public double ProgressPercentage { get; set; }
    public TimeSpan? EstimatedTimeRemaining { get; set; }
    public string CurrentStep { get; set; } = string.Empty;
}

/// <summary>
/// Summary event (final result)
/// </summary>
public class SummaryEvent : AgentEvent
{
    public string Summary { get; set; } = string.Empty;
    public int TotalActions { get; set; }
    public int SuccessfulActions { get; set; }
    public int FailedActions { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public decimal TotalCostUSD { get; set; }
}

/// <summary>
/// Generic streaming message event (for LLM token streaming)
/// </summary>
public class StreamingMessageEvent : AgentEvent
{
    public string Data { get; set; } = string.Empty;
    public int TokenCount { get; set; }
}
