using Hazina.LLMs.GoogleADK.Core;
using Microsoft.Extensions.Logging;

namespace Hazina.LLMs.GoogleADK.Workflows;

/// <summary>
/// Workflow agent that repeats steps in a loop based on conditions.
/// Supports iteration limits, break conditions, and result collection.
/// </summary>
public class LoopAgent : WorkflowAgent
{
    /// <summary>
    /// Loop configuration
    /// </summary>
    public LoopConfiguration LoopConfig { get; set; } = new();

    public LoopAgent(string name, AgentRuntime? runtime = null, AgentContext? context = null)
        : base(name, runtime, context)
    {
    }

    /// <summary>
    /// Set the continue condition for the loop
    /// </summary>
    public LoopAgent WithContinueCondition(Func<WorkflowContext, bool> condition)
    {
        LoopConfig.ContinueCondition = condition;
        return this;
    }

    /// <summary>
    /// Set maximum iterations
    /// </summary>
    public LoopAgent WithMaxIterations(int maxIterations)
    {
        LoopConfig.MaxIterations = maxIterations;
        return this;
    }

    /// <summary>
    /// Configure whether to break on error
    /// </summary>
    public LoopAgent WithBreakOnError(bool breakOnError)
    {
        LoopConfig.BreakOnError = breakOnError;
        return this;
    }

    protected override async Task<AgentResult> OnExecuteAsync(string input, CancellationToken cancellationToken)
    {
        if (Steps.Count == 0)
        {
            return AgentResult.CreateFailure("No steps configured in loop workflow");
        }

        var startTime = DateTime.UtcNow;
        WorkflowContext = new WorkflowContext
        {
            MaxIterations = LoopConfig.MaxIterations
        };
        WorkflowContext.Set("initialInput", input);

        Context.Log(LogLevel.Information,
            "Starting loop workflow with {StepCount} steps, max {MaxIterations} iterations",
            Steps.Count, LoopConfig.MaxIterations);

        var allIterationResults = new List<Dictionary<string, AgentResult>>();
        var errors = new List<string>();
        var totalStepsExecuted = 0;

        // Loop execution
        for (int iteration = 0; iteration < LoopConfig.MaxIterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            WorkflowContext.Iteration = iteration;

            Context.Log(LogLevel.Information, "Starting loop iteration {Iteration}", iteration + 1);

            var iterationResults = new Dictionary<string, AgentResult>();
            var iterationFailed = false;

            // Execute all steps in this iteration
            foreach (var step in Steps)
            {
                try
                {
                    // Use initial input for first iteration, lastResult for subsequent
                    var stepInput = iteration == 0 && string.IsNullOrEmpty(step.Input)
                        ? input
                        : step.Input;

                    step.Input = stepInput;

                    var result = await ExecuteStepAsync(step, WorkflowContext, cancellationToken);
                    WorkflowContext.StepResults[step.Id] = result;
                    iterationResults[step.Id] = result;
                    totalStepsExecuted++;

                    if (!result.Success)
                    {
                        errors.Add($"Iteration {iteration + 1}, Step {step.Name}: {result.Output}");
                        iterationFailed = true;

                        if (LoopConfig.BreakOnError)
                        {
                            Context.Log(LogLevel.Warning,
                                "Breaking loop at iteration {Iteration} due to error in step {StepName}",
                                iteration + 1, step.Name);
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    var errorMsg = $"Iteration {iteration + 1}, Step {step.Name}: {ex.Message}";
                    errors.Add(errorMsg);
                    iterationFailed = true;

                    if (LoopConfig.BreakOnError)
                    {
                        Context.Log(LogLevel.Error, "Breaking loop due to exception: {Error}", errorMsg);
                        break;
                    }
                }
            }

            if (LoopConfig.CollectResults)
            {
                allIterationResults.Add(iterationResults);
            }

            // Check if we should break due to error
            if (iterationFailed && LoopConfig.BreakOnError)
            {
                Context.Log(LogLevel.Warning, "Loop terminated at iteration {Iteration} due to error", iteration + 1);
                break;
            }

            // Check continue condition
            if (LoopConfig.ContinueCondition != null)
            {
                if (!LoopConfig.ContinueCondition(WorkflowContext))
                {
                    Context.Log(LogLevel.Information,
                        "Loop terminated at iteration {Iteration} due to continue condition",
                        iteration + 1);
                    break;
                }
            }

            Context.Log(LogLevel.Information, "Completed loop iteration {Iteration}", iteration + 1);
        }

        var duration = DateTime.UtcNow - startTime;

        // Build final output
        var lastResult = WorkflowContext.GetLastResult();
        var output = lastResult?.Output ?? "No iterations completed successfully";

        if (LoopConfig.CollectResults && allIterationResults.Count > 0)
        {
            var iterationOutputs = allIterationResults.Select((results, idx) =>
            {
                var outputs = results.Values
                    .Where(r => r.Success)
                    .Select(r => r.Output);
                return $"Iteration {idx + 1}: {string.Join(", ", outputs)}";
            });
            output = string.Join("\n", iterationOutputs);
        }

        var workflowResult = new WorkflowResult
        {
            Success = errors.Count == 0,
            Output = output,
            StepResults = new Dictionary<string, AgentResult>(WorkflowContext.StepResults),
            Duration = duration,
            StepsExecuted = totalStepsExecuted,
            Errors = errors
        };

        workflowResult.Metadata["iterations"] = WorkflowContext.Iteration + 1;
        workflowResult.Metadata["maxIterations"] = LoopConfig.MaxIterations;

        // Convert to AgentResult
        var agentResult = workflowResult.Success
            ? AgentResult.CreateSuccess(workflowResult.Output)
            : AgentResult.CreateFailure($"Loop completed with errors: {string.Join("; ", errors)}");

        agentResult.Metadata["workflowResult"] = workflowResult;
        agentResult.Metadata["iterations"] = WorkflowContext.Iteration + 1;
        agentResult.Metadata["totalStepsExecuted"] = totalStepsExecuted;
        agentResult.Metadata["errors"] = errors;

        if (LoopConfig.CollectResults)
        {
            agentResult.Metadata["allIterationResults"] = allIterationResults;
        }

        Context.Log(LogLevel.Information,
            "Loop workflow completed: {Iterations} iterations, {TotalSteps} total steps executed",
            WorkflowContext.Iteration + 1, totalStepsExecuted);

        return agentResult;
    }
}
