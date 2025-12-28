using Hazina.LLMs.GoogleADK.Core;
using Microsoft.Extensions.Logging;

namespace Hazina.LLMs.GoogleADK.Workflows;

/// <summary>
/// Workflow agent that executes steps concurrently in parallel.
/// All steps start simultaneously and results are collected when all complete.
/// </summary>
public class ParallelAgent : WorkflowAgent
{
    /// <summary>
    /// Maximum degree of parallelism (0 = unlimited)
    /// </summary>
    public int MaxDegreeOfParallelism { get; set; } = 0;

    /// <summary>
    /// Whether to wait for all steps to complete or stop on first error
    /// </summary>
    public bool WaitForAll { get; set; } = true;

    public ParallelAgent(string name, AgentRuntime? runtime = null, AgentContext? context = null)
        : base(name, runtime, context)
    {
    }

    protected override async Task<AgentResult> OnExecuteAsync(string input, CancellationToken cancellationToken)
    {
        if (Steps.Count == 0)
        {
            return AgentResult.CreateFailure("No steps configured in parallel workflow");
        }

        var startTime = DateTime.UtcNow;
        WorkflowContext = new WorkflowContext();
        WorkflowContext.Set("initialInput", input);

        Context.Log(LogLevel.Information, "Starting parallel workflow with {StepCount} steps", Steps.Count);

        var errors = new List<string>();
        var stepTasks = new List<Task<(WorkflowStep step, AgentResult result)>>();

        // Create tasks for all steps
        foreach (var step in Steps)
        {
            var stepTask = ExecuteStepWithContextAsync(step, input, cancellationToken);
            stepTasks.Add(stepTask);
        }

        // Execute based on parallelism settings
        List<(WorkflowStep step, AgentResult result)> results;

        if (MaxDegreeOfParallelism > 0)
        {
            // Execute with limited parallelism
            results = new List<(WorkflowStep, AgentResult)>();
            var semaphore = new SemaphoreSlim(MaxDegreeOfParallelism);

            var limitedTasks = stepTasks.Select(async task =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    return await task;
                }
                finally
                {
                    semaphore.Release();
                }
            });

            results.AddRange(await Task.WhenAll(limitedTasks));
        }
        else
        {
            // Execute all in parallel
            if (WaitForAll)
            {
                results = (await Task.WhenAll(stepTasks)).ToList();
            }
            else
            {
                // Stop on first completion or error
                var completedTask = await Task.WhenAny(stepTasks);
                results = new List<(WorkflowStep, AgentResult)> { await completedTask };
            }
        }

        // Collect results
        var executedSteps = 0;
        foreach (var (step, result) in results)
        {
            WorkflowContext.StepResults[step.Id] = result;
            executedSteps++;

            if (!result.Success)
            {
                errors.Add($"Step {step.Name} failed: {result.Output}");
            }
        }

        var duration = DateTime.UtcNow - startTime;

        // Combine outputs from all steps
        var outputs = results
            .Where(r => r.result.Success)
            .Select(r => $"[{r.step.Name}]: {r.result.Output}")
            .ToList();

        var combinedOutput = outputs.Count > 0
            ? string.Join("\n", outputs)
            : "No steps completed successfully";

        var workflowResult = new WorkflowResult
        {
            Success = errors.Count == 0,
            Output = combinedOutput,
            StepResults = new Dictionary<string, AgentResult>(WorkflowContext.StepResults),
            Duration = duration,
            StepsExecuted = executedSteps,
            Errors = errors
        };

        // Convert to AgentResult
        var agentResult = workflowResult.Success
            ? AgentResult.CreateSuccess(workflowResult.Output)
            : AgentResult.CreateFailure($"Parallel workflow completed with errors: {string.Join("; ", errors)}");

        agentResult.Metadata["workflowResult"] = workflowResult;
        agentResult.Metadata["stepsExecuted"] = executedSteps;
        agentResult.Metadata["totalSteps"] = Steps.Count;
        agentResult.Metadata["successfulSteps"] = outputs.Count;
        agentResult.Metadata["failedSteps"] = errors.Count;

        Context.Log(LogLevel.Information,
            "Parallel workflow completed: {SuccessfulSteps}/{TotalSteps} successful, {FailedSteps} failed",
            outputs.Count, Steps.Count, errors.Count);

        return agentResult;
    }

    private async Task<(WorkflowStep step, AgentResult result)> ExecuteStepWithContextAsync(
        WorkflowStep step,
        string input,
        CancellationToken cancellationToken)
    {
        try
        {
            // Use initial input if step input is not specified
            var stepInput = string.IsNullOrEmpty(step.Input) ? input : step.Input;
            step.Input = stepInput;

            Context.Log(LogLevel.Information, "Starting parallel step: {StepName}", step.Name);

            var result = await ExecuteStepAsync(step, WorkflowContext, cancellationToken);

            Context.Log(LogLevel.Information, "Completed parallel step: {StepName}, Success: {Success}",
                step.Name, result.Success);

            return (step, result);
        }
        catch (Exception ex)
        {
            Context.Log(LogLevel.Error, "Error in parallel step {StepName}: {Error}", step.Name, ex.Message);
            return (step, AgentResult.CreateFailure($"Exception: {ex.Message}"));
        }
    }
}
