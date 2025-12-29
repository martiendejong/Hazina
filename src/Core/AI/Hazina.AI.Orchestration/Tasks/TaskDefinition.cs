namespace Hazina.AI.Orchestration.Tasks;

/// <summary>
/// Defines a multi-step AI task
/// </summary>
public class TaskDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<TaskStep> Steps { get; set; } = new();
    public Dictionary<string, object> Parameters { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Single step in a multi-step task
/// </summary>
public class TaskStep
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public int Order { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public List<string> DependsOn { get; set; } = new(); // Step IDs this depends on
    public Dictionary<string, object> Parameters { get; set; } = new();
    public TaskStepType Type { get; set; } = TaskStepType.LLMQuery;
    public bool RequiresHumanApproval { get; set; } = false;
}

/// <summary>
/// Type of task step
/// </summary>
public enum TaskStepType
{
    LLMQuery,        // Query LLM
    DataRetrieval,   // Retrieve data
    Validation,      // Validate result
    Transformation,  // Transform data
    HumanApproval    // Wait for human input
}

/// <summary>
/// Execution result for a task
/// </summary>
public class TaskExecutionResult
{
    public string TaskId { get; set; } = string.Empty;
    public TaskExecutionStatus Status { get; set; }
    public Dictionary<string, StepResult> StepResults { get; set; } = new();
    public string? FinalResult { get; set; }
    public List<string> Errors { get; set; } = new();
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public TimeSpan Duration => (CompletedAt ?? DateTime.UtcNow) - StartedAt;
}

/// <summary>
/// Result for a single step
/// </summary>
public class StepResult
{
    public string StepId { get; set; } = string.Empty;
    public string StepName { get; set; } = string.Empty;
    public StepExecutionStatus Status { get; set; }
    public string? Result { get; set; }
    public string? Error { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public TimeSpan Duration => (CompletedAt ?? DateTime.UtcNow) - StartedAt;
}

/// <summary>
/// Task execution status
/// </summary>
public enum TaskExecutionStatus
{
    NotStarted,
    InProgress,
    Completed,
    Failed,
    Cancelled,
    PendingApproval
}

/// <summary>
/// Step execution status
/// </summary>
public enum StepExecutionStatus
{
    Pending,
    InProgress,
    Completed,
    Failed,
    Skipped
}
