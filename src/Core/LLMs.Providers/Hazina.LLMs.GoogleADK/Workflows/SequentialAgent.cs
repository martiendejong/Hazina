using Hazina.LLMs.GoogleADK.Core;
using Microsoft.Extensions.Logging;

namespace Hazina.LLMs.GoogleADK.Workflows;

/// <summary>
/// Workflow agent that executes steps sequentially in order.
/// Each step waits for the previous step to complete before starting.
/// </summary>
public class SequentialAgent : WorkflowAgent
{
    /// <summary>
    /// Whether to stop execution on first error
    /// </summary>
    public bool StopOnError { get; set; } = true;

    public SequentialAgent(string name, AgentRuntime? runtime = null, AgentContext? context = null)
        : base(name, runtime, context)
    {
    }

    protected override async Task<AgentResult> OnExecuteAsync(string input, CancellationToken cancellationToken)
    {
        if (Steps.Count == 0)
        {
            return AgentResult.CreateFailure("No steps configured in sequential workflow");
        }

        var startTime = DateTime.UtcNow;
        WorkflowContext = new WorkflowContext();
        WorkflowContext.Set("initialInput", input);

        Context.Log(LogLevel.Information, "Starting sequential workflow with {StepCount} steps", Steps.Count);

        var executedSteps = 0;
        var errors = new List<string>();

        // Execute each step in sequence
        for (int i = 0; i < Steps.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var step = Steps[i];
            Context.Log(LogLevel.Information, "Executing step {StepIndex}/{TotalSteps}: {StepName}",
                i + 1, Steps.Count, step.Name);

            try
            {
                // Use initial input for first step, or step's configured input
                var stepInput = i == 0 && string.IsNullOrEmpty(step.Input)
                    ? input
                    : step.Input;

                step.Input = stepInput;

                var result = await ExecuteStepAsync(step, WorkflowContext, cancellationToken);
                WorkflowContext.StepResults[step.Id] = result;

                executedSteps++;

                if (!result.Success)
                {
                    errors.Add($"Step {step.Name} failed: {result.Output}");

                    if (StopOnError)
                    {
                        Context.Log(LogLevel.Warning, "Stopping workflow due to error in step {StepName}", step.Name);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                var errorMsg = $"Step {step.Name} threw exception: {ex.Message}";
                errors.Add(errorMsg);
                Context.Log(LogLevel.Error, errorMsg);

                if (StopOnError)
                {
                    break;
                }
            }
        }

        var duration = DateTime.UtcNow - startTime;

        // Get final output from last successful step
        var lastResult = WorkflowContext.GetLastResult();
        var output = lastResult?.Output ?? "No steps completed successfully";

        if (errors.Count > 0 && StopOnError)
        {
            return AgentResult.CreateFailure($"Workflow failed: {string.Join("; ", errors)}");
        }

        var workflowResult = new WorkflowResult
        {
            Success = errors.Count == 0,
            Output = output,
            StepResults = new Dictionary<string, AgentResult>(WorkflowContext.StepResults),
            Duration = duration,
            StepsExecuted = executedSteps,
            Errors = errors
        };

        // Convert to AgentResult
        var agentResult = workflowResult.Success
            ? AgentResult.CreateSuccess(workflowResult.Output)
            : AgentResult.CreateFailure(workflowResult.Output);

        agentResult.Metadata["workflowResult"] = workflowResult;
        agentResult.Metadata["stepsExecuted"] = executedSteps;
        agentResult.Metadata["totalSteps"] = Steps.Count;
        agentResult.Metadata["errors"] = errors;

        Context.Log(LogLevel.Information,
            "Sequential workflow completed: {StepsExecuted}/{TotalSteps} steps, Success: {Success}",
            executedSteps, Steps.Count, workflowResult.Success);

        return agentResult;
    }
}
