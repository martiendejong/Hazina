using Hazina.LLMs.GoogleADK.Core;

namespace Hazina.LLMs.GoogleADK.Workflows;

/// <summary>
/// Represents a single step in a workflow
/// </summary>
public class WorkflowStep
{
    /// <summary>
    /// Unique identifier for this step
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Name of the step
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Agent ID to execute this step
    /// </summary>
    public string? AgentId { get; set; }

    /// <summary>
    /// Direct action to execute (if no agent specified)
    /// </summary>
    public Func<WorkflowContext, Task<AgentResult>>? Action { get; set; }

    /// <summary>
    /// Input for this step (can use template variables like {previousResult})
    /// </summary>
    public string Input { get; set; } = string.Empty;

    /// <summary>
    /// Condition to evaluate before executing this step
    /// </summary>
    public Func<WorkflowContext, bool>? Condition { get; set; }

    /// <summary>
    /// Whether to continue workflow if this step fails
    /// </summary>
    public bool ContinueOnError { get; set; } = false;

    /// <summary>
    /// Metadata for this step
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Context passed between workflow steps
/// </summary>
public class WorkflowContext
{
    /// <summary>
    /// Results from all executed steps
    /// </summary>
    public Dictionary<string, AgentResult> StepResults { get; set; } = new();

    /// <summary>
    /// Shared data between steps
    /// </summary>
    public Dictionary<string, object> Data { get; set; } = new();

    /// <summary>
    /// Current iteration (for loop workflows)
    /// </summary>
    public int Iteration { get; set; } = 0;

    /// <summary>
    /// Maximum iterations (for loop workflows)
    /// </summary>
    public int MaxIterations { get; set; } = 100;

    /// <summary>
    /// Get result from a previous step
    /// </summary>
    public AgentResult? GetStepResult(string stepId)
    {
        return StepResults.GetValueOrDefault(stepId);
    }

    /// <summary>
    /// Get the last executed step result
    /// </summary>
    public AgentResult? GetLastResult()
    {
        return StepResults.Values.LastOrDefault();
    }

    /// <summary>
    /// Set a value in the workflow data
    /// </summary>
    public void Set<T>(string key, T value)
    {
        Data[key] = value!;
    }

    /// <summary>
    /// Get a value from the workflow data
    /// </summary>
    public T? Get<T>(string key)
    {
        if (Data.TryGetValue(key, out var value))
        {
            return (T)value;
        }
        return default;
    }

    /// <summary>
    /// Process input template with current context
    /// </summary>
    public string ProcessTemplate(string template)
    {
        var result = template;

        // Replace {lastResult}
        var lastResult = GetLastResult();
        if (lastResult != null)
        {
            result = result.Replace("{lastResult}", lastResult.Output);
        }

        // Replace {stepId.output}
        foreach (var stepResult in StepResults)
        {
            result = result.Replace($"{{{stepResult.Key}.output}}", stepResult.Value.Output);
        }

        // Replace {data.key}
        foreach (var data in Data)
        {
            if (data.Value != null)
            {
                result = result.Replace($"{{data.{data.Key}}}", data.Value.ToString());
            }
        }

        return result;
    }
}

/// <summary>
/// Result of a workflow execution
/// </summary>
public class WorkflowResult
{
    /// <summary>
    /// Whether the workflow completed successfully
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Final output from the workflow
    /// </summary>
    public string Output { get; set; } = string.Empty;

    /// <summary>
    /// Results from all executed steps
    /// </summary>
    public Dictionary<string, AgentResult> StepResults { get; set; } = new();

    /// <summary>
    /// Total execution time
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Number of steps executed
    /// </summary>
    public int StepsExecuted { get; set; }

    /// <summary>
    /// Errors that occurred during execution
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Metadata about the workflow execution
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    public static WorkflowResult CreateSuccess(string output, Dictionary<string, AgentResult> stepResults, TimeSpan duration)
    {
        return new WorkflowResult
        {
            Success = true,
            Output = output,
            StepResults = stepResults,
            Duration = duration,
            StepsExecuted = stepResults.Count
        };
    }

    public static WorkflowResult CreateFailure(string error, Dictionary<string, AgentResult> stepResults, TimeSpan duration)
    {
        return new WorkflowResult
        {
            Success = false,
            Output = error,
            StepResults = stepResults,
            Duration = duration,
            StepsExecuted = stepResults.Count,
            Errors = new List<string> { error }
        };
    }
}

/// <summary>
/// Configuration for loop behavior
/// </summary>
public class LoopConfiguration
{
    /// <summary>
    /// Maximum number of iterations
    /// </summary>
    public int MaxIterations { get; set; } = 10;

    /// <summary>
    /// Condition to continue looping
    /// </summary>
    public Func<WorkflowContext, bool>? ContinueCondition { get; set; }

    /// <summary>
    /// Break on first error
    /// </summary>
    public bool BreakOnError { get; set; } = true;

    /// <summary>
    /// Collect results from all iterations
    /// </summary>
    public bool CollectResults { get; set; } = true;
}
