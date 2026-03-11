namespace Hazina.EventSourcing.Events;

/// <summary>
/// Base interface for all platform events.
/// All events are immutable records of what happened.
/// </summary>
public interface IPlatformEvent
{
    DateTime Timestamp { get; }
}

/// <summary>
/// User submitted an intent (natural language input).
/// Example: "show me all Python files modified today"
/// </summary>
public record IntentReceivedEvent(
    string IntentId,
    string UserInput,
    DateTime Timestamp
) : IPlatformEvent;

/// <summary>
/// Intent was parsed into structured format by LLM.
/// </summary>
public record IntentParsedEvent(
    string IntentId,
    string IntentType,
    string? TargetEntity,
    Dictionary<string, object>? Filters,
    double Confidence,
    DateTime Timestamp
) : IPlatformEvent;

/// <summary>
/// Task was created from parsed intent.
/// </summary>
public record TaskCreatedEvent(
    string TaskId,
    string IntentId,
    string TaskType,
    Dictionary<string, object> Parameters,
    DateTime Timestamp
) : IPlatformEvent;

/// <summary>
/// Task execution started.
/// </summary>
public record TaskStartedEvent(
    string TaskId,
    DateTime Timestamp
) : IPlatformEvent;

/// <summary>
/// Task execution completed successfully.
/// </summary>
public record TaskCompletedEvent(
    string TaskId,
    string? Result,
    TimeSpan Duration,
    DateTime Timestamp
) : IPlatformEvent;

/// <summary>
/// Task execution failed with error.
/// </summary>
public record TaskFailedEvent(
    string TaskId,
    string ErrorMessage,
    string? StackTrace,
    DateTime Timestamp
) : IPlatformEvent;

/// <summary>
/// UI view was opened to display results.
/// </summary>
public record ViewOpenedEvent(
    string ViewId,
    string ComponentId,
    string TaskId,
    object ViewData,
    DateTime Timestamp
) : IPlatformEvent;

/// <summary>
/// UI view was updated with new data.
/// </summary>
public record ViewUpdatedEvent(
    string ViewId,
    object ViewData,
    DateTime Timestamp
) : IPlatformEvent;

/// <summary>
/// UI view was closed by user or system.
/// </summary>
public record ViewClosedEvent(
    string ViewId,
    DateTime Timestamp
) : IPlatformEvent;

/// <summary>
/// User approved a pending action (e.g., file write, command execution).
/// </summary>
public record ApprovalGrantedEvent(
    string ApprovalId,
    string TaskId,
    string ActionType,
    string? ActionTarget,
    DateTime Timestamp
) : IPlatformEvent;

/// <summary>
/// User denied a pending action.
/// </summary>
public record ApprovalDeniedEvent(
    string ApprovalId,
    string TaskId,
    string ActionType,
    string? Reason,
    DateTime Timestamp
) : IPlatformEvent;

/// <summary>
/// Indexation started for a directory.
/// </summary>
public record IndexationStartedEvent(
    string IndexationId,
    string RootPath,
    string Phase, // "A", "B", or "C"
    DateTime Timestamp
) : IPlatformEvent;

/// <summary>
/// Indexation progress update.
/// </summary>
public record IndexationProgressEvent(
    string IndexationId,
    int FilesScanned,
    int FilesIndexed,
    string CurrentFile,
    double PercentComplete,
    DateTime Timestamp
) : IPlatformEvent;

/// <summary>
/// Indexation completed successfully.
/// </summary>
public record IndexationCompletedEvent(
    string IndexationId,
    int TotalFiles,
    TimeSpan Duration,
    DateTime Timestamp
) : IPlatformEvent;

/// <summary>
/// Capability (permission) was requested by agent.
/// </summary>
public record CapabilityRequestedEvent(
    string RequestId,
    string TaskId,
    string CapabilityType,
    string Context,
    DateTime Timestamp
) : IPlatformEvent;

/// <summary>
/// Capability was granted by user.
/// </summary>
public record CapabilityGrantedEvent(
    string RequestId,
    string CapabilityType,
    bool GrantAlways,
    DateTime Timestamp
) : IPlatformEvent;

/// <summary>
/// Capability was denied by user.
/// </summary>
public record CapabilityDeniedEvent(
    string RequestId,
    string CapabilityType,
    string? Reason,
    DateTime Timestamp
) : IPlatformEvent;
