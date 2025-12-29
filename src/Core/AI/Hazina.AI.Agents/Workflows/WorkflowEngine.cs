using Hazina.AI.Agents.Core;

namespace Hazina.AI.Agents.Workflows;

/// <summary>
/// Orchestrates multi-step workflows
/// </summary>
public class WorkflowEngine
{
    private readonly List<Agent> _agents = new();
    private readonly WorkflowConfig _config;

    public WorkflowEngine(WorkflowConfig? config = null)
    {
        _config = config ?? new WorkflowConfig();
    }

    /// <summary>
    /// Register an agent
    /// </summary>
    public void RegisterAgent(Agent agent)
    {
        if (!_agents.Any(a => a.Name == agent.Name))
        {
            _agents.Add(agent);
        }
    }

    /// <summary>
    /// Execute a workflow
    /// </summary>
    public async Task<WorkflowResult> ExecuteWorkflowAsync(
        Workflow workflow,
        Dictionary<string, object>? initialContext = null,
        CancellationToken cancellationToken = default)
    {
        var result = new WorkflowResult
        {
            WorkflowName = workflow.Name,
            StartTime = DateTime.UtcNow
        };

        var context = initialContext ?? new Dictionary<string, object>();

        try
        {
            foreach (var step in workflow.Steps)
            {
                var stepResult = await ExecuteStepAsync(step, context, cancellationToken);
                result.StepResults.Add(stepResult);

                if (!stepResult.Success && !step.ContinueOnFailure)
                {
                    result.Success = false;
                    result.Error = $"Step '{step.Name}' failed: {stepResult.Error}";
                    break;
                }

                // Update context with step output
                if (stepResult.Success && !string.IsNullOrEmpty(step.OutputKey))
                {
                    context[step.OutputKey] = stepResult.Output;
                }
            }

            result.Success = result.StepResults.All(r => r.Success);
            result.FinalContext = context;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
        }

        result.EndTime = DateTime.UtcNow;
        result.Duration = result.EndTime - result.StartTime;
        return result;
    }

    /// <summary>
    /// Execute a workflow step
    /// </summary>
    private async Task<StepResult> ExecuteStepAsync(
        WorkflowStep step,
        Dictionary<string, object> context,
        CancellationToken cancellationToken)
    {
        var stepResult = new StepResult
        {
            StepName = step.Name,
            StartTime = DateTime.UtcNow
        };

        try
        {
            switch (step.Type)
            {
                case StepType.AgentTask:
                    stepResult = await ExecuteAgentStepAsync(step, context, cancellationToken);
                    break;

                case StepType.Parallel:
                    stepResult = await ExecuteParallelStepAsync(step, context, cancellationToken);
                    break;

                case StepType.Conditional:
                    stepResult = await ExecuteConditionalStepAsync(step, context, cancellationToken);
                    break;

                case StepType.Loop:
                    stepResult = await ExecuteLoopStepAsync(step, context, cancellationToken);
                    break;

                default:
                    stepResult.Success = false;
                    stepResult.Error = $"Unknown step type: {step.Type}";
                    break;
            }
        }
        catch (Exception ex)
        {
            stepResult.Success = false;
            stepResult.Error = ex.Message;
        }

        stepResult.EndTime = DateTime.UtcNow;
        stepResult.Duration = stepResult.EndTime - stepResult.StartTime;
        return stepResult;
    }

    /// <summary>
    /// Execute agent task step
    /// </summary>
    private async Task<StepResult> ExecuteAgentStepAsync(
        WorkflowStep step,
        Dictionary<string, object> context,
        CancellationToken cancellationToken)
    {
        var agent = _agents.FirstOrDefault(a => a.Name == step.AgentName);
        if (agent == null)
        {
            return new StepResult
            {
                StepName = step.Name,
                Success = false,
                Error = $"Agent '{step.AgentName}' not found"
            };
        }

        var task = ReplaceContextVariables(step.Task, context);
        var response = await agent.ExecuteAsync(task, context, cancellationToken);

        return new StepResult
        {
            StepName = step.Name,
            Success = response.Success,
            Output = response.Result,
            Error = response.Error,
            StartTime = response.StartTime,
            EndTime = response.EndTime,
            Duration = response.Duration
        };
    }

    /// <summary>
    /// Execute parallel steps
    /// </summary>
    private async Task<StepResult> ExecuteParallelStepAsync(
        WorkflowStep step,
        Dictionary<string, object> context,
        CancellationToken cancellationToken)
    {
        if (step.ParallelSteps == null || step.ParallelSteps.Count == 0)
        {
            return new StepResult
            {
                StepName = step.Name,
                Success = false,
                Error = "No parallel steps defined"
            };
        }

        var tasks = step.ParallelSteps.Select(s => ExecuteStepAsync(s, context, cancellationToken));
        var results = await Task.WhenAll(tasks);

        return new StepResult
        {
            StepName = step.Name,
            Success = results.All(r => r.Success),
            Output = string.Join("\n", results.Select(r => r.Output)),
            Error = results.Any(r => !r.Success)
                ? string.Join("; ", results.Where(r => !r.Success).Select(r => r.Error))
                : null
        };
    }

    /// <summary>
    /// Execute conditional step
    /// </summary>
    private async Task<StepResult> ExecuteConditionalStepAsync(
        WorkflowStep step,
        Dictionary<string, object> context,
        CancellationToken cancellationToken)
    {
        if (step.Condition == null)
        {
            return new StepResult
            {
                StepName = step.Name,
                Success = false,
                Error = "No condition defined"
            };
        }

        var conditionMet = EvaluateCondition(step.Condition, context);
        var branchStep = conditionMet ? step.ThenStep : step.ElseStep;

        if (branchStep == null)
        {
            return new StepResult
            {
                StepName = step.Name,
                Success = true,
                Output = $"Condition evaluated to {conditionMet}, no branch defined"
            };
        }

        return await ExecuteStepAsync(branchStep, context, cancellationToken);
    }

    /// <summary>
    /// Execute loop step
    /// </summary>
    private async Task<StepResult> ExecuteLoopStepAsync(
        WorkflowStep step,
        Dictionary<string, object> context,
        CancellationToken cancellationToken)
    {
        if (step.LoopStep == null)
        {
            return new StepResult
            {
                StepName = step.Name,
                Success = false,
                Error = "No loop step defined"
            };
        }

        var iterations = 0;
        var maxIterations = step.MaxIterations ?? 10;
        var results = new List<StepResult>();

        while (iterations < maxIterations)
        {
            if (step.LoopCondition != null && !EvaluateCondition(step.LoopCondition, context))
            {
                break;
            }

            var loopResult = await ExecuteStepAsync(step.LoopStep, context, cancellationToken);
            results.Add(loopResult);

            if (!loopResult.Success)
            {
                break;
            }

            iterations++;
        }

        return new StepResult
        {
            StepName = step.Name,
            Success = results.All(r => r.Success),
            Output = $"Completed {iterations} iterations",
            Error = results.Any(r => !r.Success)
                ? results.First(r => !r.Success).Error
                : null
        };
    }

    /// <summary>
    /// Evaluate a condition
    /// </summary>
    private bool EvaluateCondition(WorkflowCondition condition, Dictionary<string, object> context)
    {
        if (!context.ContainsKey(condition.Variable))
        {
            return false;
        }

        var value = context[condition.Variable];

        return condition.Operator switch
        {
            ConditionOperator.Equals => value?.ToString() == condition.Value?.ToString(),
            ConditionOperator.NotEquals => value?.ToString() != condition.Value?.ToString(),
            ConditionOperator.Contains => value?.ToString()?.Contains(condition.Value?.ToString() ?? "") ?? false,
            ConditionOperator.Exists => true,
            _ => false
        };
    }

    /// <summary>
    /// Replace context variables in string
    /// </summary>
    private string ReplaceContextVariables(string text, Dictionary<string, object> context)
    {
        var result = text;
        foreach (var kvp in context)
        {
            result = result.Replace($"{{{kvp.Key}}}", kvp.Value?.ToString() ?? "");
        }
        return result;
    }
}

/// <summary>
/// Workflow configuration
/// </summary>
public class WorkflowConfig
{
    public int DefaultMaxIterations { get; set; } = 10;
    public bool StopOnFirstFailure { get; set; } = true;
}

/// <summary>
/// Workflow definition
/// </summary>
public class Workflow
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<WorkflowStep> Steps { get; set; } = new();
}

/// <summary>
/// Workflow step
/// </summary>
public class WorkflowStep
{
    public string Name { get; set; } = string.Empty;
    public StepType Type { get; set; }
    public string AgentName { get; set; } = string.Empty;
    public string Task { get; set; } = string.Empty;
    public string OutputKey { get; set; } = string.Empty;
    public bool ContinueOnFailure { get; set; } = false;

    // Parallel execution
    public List<WorkflowStep>? ParallelSteps { get; set; }

    // Conditional execution
    public WorkflowCondition? Condition { get; set; }
    public WorkflowStep? ThenStep { get; set; }
    public WorkflowStep? ElseStep { get; set; }

    // Loop execution
    public WorkflowStep? LoopStep { get; set; }
    public WorkflowCondition? LoopCondition { get; set; }
    public int? MaxIterations { get; set; }
}

/// <summary>
/// Step types
/// </summary>
public enum StepType
{
    AgentTask,
    Parallel,
    Conditional,
    Loop
}

/// <summary>
/// Workflow condition
/// </summary>
public class WorkflowCondition
{
    public string Variable { get; set; } = string.Empty;
    public ConditionOperator Operator { get; set; }
    public object? Value { get; set; }
}

/// <summary>
/// Condition operators
/// </summary>
public enum ConditionOperator
{
    Equals,
    NotEquals,
    Contains,
    Exists
}

/// <summary>
/// Workflow result
/// </summary>
public class WorkflowResult
{
    public string WorkflowName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
    public List<StepResult> StepResults { get; set; } = new();
    public Dictionary<string, object>? FinalContext { get; set; }
}

/// <summary>
/// Step result
/// </summary>
public class StepResult
{
    public string StepName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public bool Success { get; set; }
    public string Output { get; set; } = string.Empty;
    public string? Error { get; set; }
}
