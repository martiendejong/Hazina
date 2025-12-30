using Hazina.Agents.Coding.Memory;

namespace Hazina.Agents.Coding;

/// <summary>
/// Agent loop implementation - enforces exact plan → act → observe flow.
/// Stop conditions are decided here, not by the GLM model.
/// </summary>
public class AgentLoop
{
    private readonly GlmPlanner _planner;
    private readonly ToolExecutor _executor;
    private readonly AgentSummaryStore _memoryStore;
    private readonly string _taskId;

    public AgentLoop(
        GlmPlanner planner,
        ToolExecutor executor,
        AgentSummaryStore memoryStore,
        string taskId)
    {
        _planner = planner ?? throw new ArgumentNullException(nameof(planner));
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _memoryStore = memoryStore ?? throw new ArgumentNullException(nameof(memoryStore));
        _taskId = taskId ?? throw new ArgumentNullException(nameof(taskId));
    }

    /// <summary>
    /// Execute the agent loop for a coding task
    /// Follows exact flow:
    /// 1. Load task + memory summary
    /// 2. Ask GLM for plan + actions
    /// 3. Validate JSON schema
    /// 4. Execute actions sequentially
    /// 5. Capture results
    /// 6. Summarize outcome (max 500 tokens)
    /// 7. Store summary
    /// 8. Stop when: no actions, tests succeed, or max iterations
    /// </summary>
    public async Task<AgentRunResult> RunAsync(CodingTask task)
    {
        int iteration = 0;
        string? memorySummary = await _memoryStore.LoadSummariesAsync(_taskId);
        List<ExecutionResult>? previousResults = null;
        string finalSummary = string.Empty;

        try
        {
            while (iteration < task.MaxIterations)
            {
                iteration++;

                // Step 1: Load context
                var context = new AgentContext
                {
                    Task = task.Description,
                    WorkingDirectory = task.WorkingDirectory,
                    MemorySummary = memorySummary,
                    PreviousResults = previousResults,
                    Iteration = iteration
                };

                // Step 2: Ask GLM for plan
                PlanResult plan;
                try
                {
                    plan = await _planner.GeneratePlanAsync(context);
                }
                catch (Exception ex)
                {
                    return new AgentRunResult
                    {
                        Success = false,
                        Iterations = iteration,
                        Summary = $"Failed to generate plan: {ex.Message}",
                        Error = ex.Message
                    };
                }

                // Step 3: Validate schema (already done in planner, but double-check)
                var (valid, error) = ToolRegistry.ValidateActions(plan.Actions);
                if (!valid)
                {
                    return new AgentRunResult
                    {
                        Success = false,
                        Iterations = iteration,
                        Summary = $"Invalid actions: {error}",
                        Error = error
                    };
                }

                // Step 4: Execute actions sequentially
                var iterationResults = new List<ExecutionResult>();

                foreach (var action in plan.Actions)
                {
                    var result = await _executor.ExecuteAsync(action);
                    iterationResults.Add(result);

                    if (!result.Success)
                    {
                        break;
                    }
                }

                previousResults = iterationResults;

                // Step 5: Summarize outcome (max 500 tokens)
                var iterationSummary = SummarizeIteration(plan, iterationResults);

                // Step 6: Store summary
                await _memoryStore.StoreSummaryAsync(_taskId, iteration, iterationSummary);

                finalSummary = iterationSummary;

                // Step 7: Check stop conditions
                // Condition 1: No actions returned
                if (plan.Actions.Count == 0)
                {
                    return new AgentRunResult
                    {
                        Success = true,
                        Iterations = iteration,
                        Summary = "Task completed - no further actions needed",
                        MemorySummary = await _memoryStore.LoadSummariesAsync(_taskId)
                    };
                }

                // Condition 2: Tests succeed (if test command provided)
                if (!string.IsNullOrWhiteSpace(task.TestCommand))
                {
                    var testResult = await _executor.ExecuteAsync(new ToolAction
                    {
                        Tool = "run",
                        Command = task.TestCommand
                    });

                    if (testResult.Success)
                    {
                        return new AgentRunResult
                        {
                            Success = true,
                            Iterations = iteration,
                            Summary = "Task completed - tests passed",
                            MemorySummary = await _memoryStore.LoadSummariesAsync(_taskId)
                        };
                    }
                }

                // Update memory summary for next iteration
                memorySummary = await _memoryStore.LoadSummariesAsync(_taskId);
            }

            // Condition 3: Max iterations reached
            return new AgentRunResult
            {
                Success = false,
                Iterations = iteration,
                Summary = $"Max iterations ({task.MaxIterations}) reached",
                MemorySummary = await _memoryStore.LoadSummariesAsync(_taskId),
                Error = "Max iterations exceeded"
            };
        }
        catch (Exception ex)
        {
            return new AgentRunResult
            {
                Success = false,
                Iterations = iteration,
                Summary = $"Agent loop failed: {ex.Message}",
                MemorySummary = await _memoryStore.LoadSummariesAsync(_taskId),
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Summarize iteration outcome (max 500 tokens)
    /// </summary>
    private string SummarizeIteration(PlanResult plan, List<ExecutionResult> results)
    {
        var summary = $"Plan: {string.Join(", ", plan.Plan)}\n\n";

        summary += "Actions executed:\n";

        for (int i = 0; i < results.Count; i++)
        {
            var result = results[i];
            summary += $"- {result.Tool}: {(result.Success ? "Success" : "Failed")}";

            if (!result.Success && !string.IsNullOrWhiteSpace(result.Error))
            {
                var errorPreview = result.Error.Length > 100
                    ? result.Error.Substring(0, 100) + "..."
                    : result.Error;
                summary += $" ({errorPreview})";
            }

            summary += "\n";
        }

        var maxChars = 500 * 4;
        if (summary.Length > maxChars)
        {
            summary = summary.Substring(0, maxChars) + "... [truncated]";
        }

        return summary;
    }
}
