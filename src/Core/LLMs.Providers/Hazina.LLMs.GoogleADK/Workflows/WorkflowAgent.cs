using Hazina.LLMs.GoogleADK.Core;
using Hazina.LLMs.GoogleADK.Events;
using Microsoft.Extensions.Logging;

namespace Hazina.LLMs.GoogleADK.Workflows;

/// <summary>
/// Base class for workflow agents that execute deterministic control flows.
/// Workflow agents don't use LLMs for decision-making.
/// </summary>
public abstract class WorkflowAgent : BaseAgent
{
    protected readonly List<WorkflowStep> Steps = new();
    protected readonly AgentRuntime? Runtime;

    /// <summary>
    /// Workflow context for this execution
    /// </summary>
    protected WorkflowContext WorkflowContext { get; set; } = new();

    protected WorkflowAgent(string name, AgentRuntime? runtime = null, AgentContext? context = null)
        : base(name, context)
    {
        Runtime = runtime;
    }

    /// <summary>
    /// Add a step to the workflow
    /// </summary>
    public WorkflowAgent AddStep(WorkflowStep step)
    {
        Steps.Add(step);
        return this;
    }

    /// <summary>
    /// Add a step with an agent
    /// </summary>
    public WorkflowAgent AddStep(string name, string agentId, string input)
    {
        Steps.Add(new WorkflowStep
        {
            Name = name,
            AgentId = agentId,
            Input = input
        });
        return this;
    }

    /// <summary>
    /// Add a step with a custom action
    /// </summary>
    public WorkflowAgent AddStep(string name, Func<WorkflowContext, Task<AgentResult>> action, string input = "")
    {
        Steps.Add(new WorkflowStep
        {
            Name = name,
            Action = action,
            Input = input
        });
        return this;
    }

    /// <summary>
    /// Add a conditional step
    /// </summary>
    public WorkflowAgent AddConditionalStep(string name, Func<WorkflowContext, bool> condition, string agentId, string input)
    {
        Steps.Add(new WorkflowStep
        {
            Name = name,
            AgentId = agentId,
            Input = input,
            Condition = condition
        });
        return this;
    }

    /// <summary>
    /// Execute a single workflow step
    /// </summary>
    protected async Task<AgentResult> ExecuteStepAsync(WorkflowStep step, WorkflowContext context, CancellationToken cancellationToken)
    {
        // Check condition
        if (step.Condition != null && !step.Condition(context))
        {
            Context.Log(LogLevel.Information, "Step {StepName} skipped due to condition", step.Name);
            return AgentResult.CreateSuccess($"Skipped: {step.Name}");
        }

        // Process input template
        var processedInput = context.ProcessTemplate(step.Input);

        Context.EmitEvent(new ToolCalledEvent
        {
            ToolName = step.Name,
            Arguments = new Dictionary<string, object> { { "input", processedInput } }
        });

        var stepStartTime = DateTime.UtcNow;

        try
        {
            AgentResult result;

            // Execute via agent or direct action
            if (step.Action != null)
            {
                result = await step.Action(context);
            }
            else if (!string.IsNullOrEmpty(step.AgentId) && Runtime != null)
            {
                result = await Runtime.ExecuteAgentAsync(step.AgentId, processedInput, cancellationToken);
            }
            else
            {
                throw new InvalidOperationException($"Step {step.Name} has no action or agent configured");
            }

            var duration = DateTime.UtcNow - stepStartTime;
            Context.EmitEvent(new ToolResultEvent
            {
                ToolName = step.Name,
                Result = result.Output,
                Success = result.Success,
                Duration = duration
            });

            return result;
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - stepStartTime;
            Context.Log(LogLevel.Error, "Error executing step {StepName}: {Error}", step.Name, ex.Message);

            Context.EmitEvent(new ToolResultEvent
            {
                ToolName = step.Name,
                Result = ex.Message,
                Success = false,
                Duration = duration
            });

            if (!step.ContinueOnError)
            {
                throw;
            }

            return AgentResult.CreateFailure($"Error in {step.Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Clear all steps
    /// </summary>
    public void ClearSteps()
    {
        Steps.Clear();
    }

    /// <summary>
    /// Get all configured steps
    /// </summary>
    public IReadOnlyList<WorkflowStep> GetSteps()
    {
        return Steps.AsReadOnly();
    }
}
