namespace Hazina.AI.Orchestration.Tasks;

/// <summary>
/// Interface for orchestrating multi-step AI tasks
/// </summary>
public interface ITaskOrchestrator
{
    /// <summary>
    /// Execute a task definition
    /// </summary>
    Task<TaskExecutionResult> ExecuteTaskAsync(
        TaskDefinition task,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Execute a task with progress reporting
    /// </summary>
    Task<TaskExecutionResult> ExecuteTaskAsync(
        TaskDefinition task,
        Action<StepResult> onStepCompleted,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get task execution status
    /// </summary>
    TaskExecutionResult? GetTaskStatus(string taskId);

    /// <summary>
    /// Cancel running task
    /// </summary>
    void CancelTask(string taskId);
}
