using Hazina.AI.Orchestration.Context;
using Hazina.AI.Providers.Core;

namespace Hazina.AI.Orchestration.Tasks;

/// <summary>
/// Orchestrates multi-step AI tasks with context management
/// </summary>
public class TaskOrchestrator : ITaskOrchestrator
{
    private readonly IProviderOrchestrator _providerOrchestrator;
    private readonly IContextManager _contextManager;
    private readonly Dictionary<string, TaskExecutionResult> _runningTasks = new();
    private readonly Dictionary<string, CancellationTokenSource> _taskCancellations = new();
    private readonly object _lock = new();

    public TaskOrchestrator(
        IProviderOrchestrator providerOrchestrator,
        IContextManager contextManager)
    {
        _providerOrchestrator = providerOrchestrator ?? throw new ArgumentNullException(nameof(providerOrchestrator));
        _contextManager = contextManager ?? throw new ArgumentNullException(nameof(contextManager));
    }

    /// <summary>
    /// Execute a task definition
    /// </summary>
    public async Task<TaskExecutionResult> ExecuteTaskAsync(
        TaskDefinition task,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteTaskAsync(task, null, cancellationToken);
    }

    /// <summary>
    /// Execute a task with progress reporting
    /// </summary>
    public async Task<TaskExecutionResult> ExecuteTaskAsync(
        TaskDefinition task,
        Action<StepResult>? onStepCompleted,
        CancellationToken cancellationToken = default)
    {
        var result = new TaskExecutionResult
        {
            TaskId = task.Id,
            Status = TaskExecutionStatus.InProgress,
            StartedAt = DateTime.UtcNow
        };

        lock (_lock)
        {
            _runningTasks[task.Id] = result;
            _taskCancellations[task.Id] = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        }

        // Create conversation context for this task
        var context = _contextManager.CreateContext();

        try
        {
            // Execute steps in order, respecting dependencies
            var orderedSteps = task.Steps.OrderBy(s => s.Order).ToList();

            foreach (var step in orderedSteps)
            {
                // Check if task was cancelled
                if (_taskCancellations[task.Id].Token.IsCancellationRequested)
                {
                    result.Status = TaskExecutionStatus.Cancelled;
                    break;
                }

                // Check dependencies
                if (!await CheckDependenciesAsync(step, result))
                {
                    result.Errors.Add($"Step {step.Name}: Dependencies not met");
                    result.Status = TaskExecutionStatus.Failed;
                    break;
                }

                // Execute step
                var stepResult = await ExecuteStepAsync(step, context, result, _taskCancellations[task.Id].Token);
                result.StepResults[step.Id] = stepResult;

                // Notify progress
                onStepCompleted?.Invoke(stepResult);

                // Check if step failed
                if (stepResult.Status == StepExecutionStatus.Failed)
                {
                    result.Status = TaskExecutionStatus.Failed;
                    result.Errors.Add($"Step {step.Name} failed: {stepResult.Error}");
                    break;
                }

                // Check if requires human approval
                if (step.RequiresHumanApproval)
                {
                    result.Status = TaskExecutionStatus.PendingApproval;
                    // Wait for approval (implementation specific)
                    break;
                }
            }

            // Set final status if not already set
            if (result.Status == TaskExecutionStatus.InProgress)
            {
                result.Status = TaskExecutionStatus.Completed;

                // Combine step results as final result
                var lastStep = result.StepResults.Values.LastOrDefault();
                result.FinalResult = lastStep?.Result;
            }
        }
        catch (Exception ex)
        {
            result.Status = TaskExecutionStatus.Failed;
            result.Errors.Add(ex.Message);
        }
        finally
        {
            result.CompletedAt = DateTime.UtcNow;

            lock (_lock)
            {
                _taskCancellations[task.Id].Dispose();
                _taskCancellations.Remove(task.Id);
            }

            // Clean up context
            _contextManager.DeleteContext(context.Id);
        }

        return result;
    }

    /// <summary>
    /// Get task execution status
    /// </summary>
    public TaskExecutionResult? GetTaskStatus(string taskId)
    {
        lock (_lock)
        {
            return _runningTasks.TryGetValue(taskId, out var result) ? result : null;
        }
    }

    /// <summary>
    /// Cancel running task
    /// </summary>
    public void CancelTask(string taskId)
    {
        lock (_lock)
        {
            if (_taskCancellations.TryGetValue(taskId, out var cts))
            {
                cts.Cancel();
            }
        }
    }

    #region Private Methods

    private async Task<bool> CheckDependenciesAsync(TaskStep step, TaskExecutionResult taskResult)
    {
        if (!step.DependsOn.Any())
            return true;

        foreach (var dependencyId in step.DependsOn)
        {
            if (!taskResult.StepResults.TryGetValue(dependencyId, out var dependencyResult))
                return false;

            if (dependencyResult.Status != StepExecutionStatus.Completed)
                return false;
        }

        return true;
    }

    private async Task<StepResult> ExecuteStepAsync(
        TaskStep step,
        ConversationContext context,
        TaskExecutionResult taskResult,
        CancellationToken cancellationToken)
    {
        var stepResult = new StepResult
        {
            StepId = step.Id,
            StepName = step.Name,
            Status = StepExecutionStatus.InProgress,
            StartedAt = DateTime.UtcNow
        };

        try
        {
            switch (step.Type)
            {
                case TaskStepType.LLMQuery:
                    await ExecuteLLMQueryAsync(step, context, stepResult, taskResult, cancellationToken);
                    break;

                case TaskStepType.Validation:
                    ExecuteValidation(step, stepResult, taskResult);
                    break;

                default:
                    stepResult.Status = StepExecutionStatus.Skipped;
                    stepResult.Result = "Step type not implemented";
                    break;
            }

            if (stepResult.Status == StepExecutionStatus.InProgress)
            {
                stepResult.Status = StepExecutionStatus.Completed;
            }
        }
        catch (Exception ex)
        {
            stepResult.Status = StepExecutionStatus.Failed;
            stepResult.Error = ex.Message;
        }
        finally
        {
            stepResult.CompletedAt = DateTime.UtcNow;
        }

        return stepResult;
    }

    private async Task ExecuteLLMQueryAsync(
        TaskStep step,
        ConversationContext context,
        StepResult stepResult,
        TaskExecutionResult taskResult,
        CancellationToken cancellationToken)
    {
        // Build prompt with context from previous steps
        var prompt = BuildPromptWithDependencies(step, taskResult);

        // Add to conversation context
        var message = new HazinaChatMessage
        {
            Role = HazinaMessageRole.User,
            Text = prompt
        };
        context.AddMessage(message);

        // Get messages within token limit
        var messages = context.GetMessages(context.MaxTokens);

        // Execute LLM query
        var response = await _providerOrchestrator.GetResponse(
            messages,
            HazinaChatResponseFormat.Text,
            null,
            null,
            cancellationToken
        );

        // Add response to context
        context.AddMessage(new HazinaChatMessage
        {
            Role = HazinaMessageRole.Assistant,
            Text = response.Result
        });

        stepResult.Result = response.Result;
    }

    private void ExecuteValidation(
        TaskStep step,
        StepResult stepResult,
        TaskExecutionResult taskResult)
    {
        // Get previous step result
        if (step.DependsOn.Any())
        {
            var previousStepId = step.DependsOn.First();
            if (taskResult.StepResults.TryGetValue(previousStepId, out var previousResult))
            {
                // Simple validation - check if result is not empty
                if (string.IsNullOrWhiteSpace(previousResult.Result))
                {
                    stepResult.Status = StepExecutionStatus.Failed;
                    stepResult.Error = "Previous step result is empty";
                }
                else
                {
                    stepResult.Result = "Validation passed";
                }
            }
        }
    }

    private string BuildPromptWithDependencies(TaskStep step, TaskExecutionResult taskResult)
    {
        var prompt = step.Prompt;

        // Replace placeholders with dependency results
        foreach (var dependencyId in step.DependsOn)
        {
            if (taskResult.StepResults.TryGetValue(dependencyId, out var dependencyResult))
            {
                // Replace {step.name} with result
                var placeholder = $"{{{dependencyResult.StepName}}}";
                if (prompt.Contains(placeholder))
                {
                    prompt = prompt.Replace(placeholder, dependencyResult.Result ?? "");
                }
            }
        }

        return prompt;
    }

    #endregion
}
