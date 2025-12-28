using Hazina.LLMs.GoogleADK.Core;
using Microsoft.Extensions.Logging;

namespace Hazina.LLMs.GoogleADK.Workflows;

/// <summary>
/// Engine for executing and composing complex workflows.
/// Provides builders and helpers for creating workflow chains.
/// </summary>
public class WorkflowEngine
{
    private readonly AgentRuntime _runtime;
    private readonly ILogger<WorkflowEngine>? _logger;

    public WorkflowEngine(AgentRuntime runtime, ILogger<WorkflowEngine>? logger = null)
    {
        _runtime = runtime;
        _logger = logger;
    }

    /// <summary>
    /// Create a sequential workflow builder
    /// </summary>
    public SequentialWorkflowBuilder Sequential(string name)
    {
        return new SequentialWorkflowBuilder(name, _runtime);
    }

    /// <summary>
    /// Create a parallel workflow builder
    /// </summary>
    public ParallelWorkflowBuilder Parallel(string name)
    {
        return new ParallelWorkflowBuilder(name, _runtime);
    }

    /// <summary>
    /// Create a loop workflow builder
    /// </summary>
    public LoopWorkflowBuilder Loop(string name)
    {
        return new LoopWorkflowBuilder(name, _runtime);
    }

    /// <summary>
    /// Execute a workflow and return result
    /// </summary>
    public async Task<WorkflowResult> ExecuteWorkflowAsync(
        WorkflowAgent workflow,
        string input,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Executing workflow: {WorkflowName}", workflow.Name);

        var result = await workflow.ExecuteAsync(input, cancellationToken);

        if (result.Metadata.TryGetValue("workflowResult", out var workflowResultObj) &&
            workflowResultObj is WorkflowResult workflowResult)
        {
            return workflowResult;
        }

        // Fallback if workflowResult not in metadata
        return new WorkflowResult
        {
            Success = result.Success,
            Output = result.Output,
            StepResults = new Dictionary<string, AgentResult>(),
            Duration = TimeSpan.Zero,
            StepsExecuted = 0
        };
    }
}

/// <summary>
/// Builder for sequential workflows
/// </summary>
public class SequentialWorkflowBuilder
{
    private readonly SequentialAgent _agent;

    public SequentialWorkflowBuilder(string name, AgentRuntime runtime)
    {
        var context = runtime.CreateContext(name);
        _agent = new SequentialAgent(name, runtime, context);
    }

    public SequentialWorkflowBuilder AddStep(string stepName, string agentId, string input)
    {
        _agent.AddStep(stepName, agentId, input);
        return this;
    }

    public SequentialWorkflowBuilder AddStep(string stepName, Func<WorkflowContext, Task<AgentResult>> action)
    {
        _agent.AddStep(stepName, action);
        return this;
    }

    public SequentialWorkflowBuilder AddConditionalStep(
        string stepName,
        Func<WorkflowContext, bool> condition,
        string agentId,
        string input)
    {
        _agent.AddConditionalStep(stepName, condition, agentId, input);
        return this;
    }

    public SequentialWorkflowBuilder StopOnError(bool stopOnError = true)
    {
        _agent.StopOnError = stopOnError;
        return this;
    }

    public SequentialAgent Build()
    {
        return _agent;
    }
}

/// <summary>
/// Builder for parallel workflows
/// </summary>
public class ParallelWorkflowBuilder
{
    private readonly ParallelAgent _agent;

    public ParallelWorkflowBuilder(string name, AgentRuntime runtime)
    {
        var context = runtime.CreateContext(name);
        _agent = new ParallelAgent(name, runtime, context);
    }

    public ParallelWorkflowBuilder AddStep(string stepName, string agentId, string input)
    {
        _agent.AddStep(stepName, agentId, input);
        return this;
    }

    public ParallelWorkflowBuilder AddStep(string stepName, Func<WorkflowContext, Task<AgentResult>> action)
    {
        _agent.AddStep(stepName, action);
        return this;
    }

    public ParallelWorkflowBuilder WithMaxDegreeOfParallelism(int maxDegreeOfParallelism)
    {
        _agent.MaxDegreeOfParallelism = maxDegreeOfParallelism;
        return this;
    }

    public ParallelWorkflowBuilder WaitForAll(bool waitForAll = true)
    {
        _agent.WaitForAll = waitForAll;
        return this;
    }

    public ParallelAgent Build()
    {
        return _agent;
    }
}

/// <summary>
/// Builder for loop workflows
/// </summary>
public class LoopWorkflowBuilder
{
    private readonly LoopAgent _agent;

    public LoopWorkflowBuilder(string name, AgentRuntime runtime)
    {
        var context = runtime.CreateContext(name);
        _agent = new LoopAgent(name, runtime, context);
    }

    public LoopWorkflowBuilder AddStep(string stepName, string agentId, string input)
    {
        _agent.AddStep(stepName, agentId, input);
        return this;
    }

    public LoopWorkflowBuilder AddStep(string stepName, Func<WorkflowContext, Task<AgentResult>> action)
    {
        _agent.AddStep(stepName, action);
        return this;
    }

    public LoopWorkflowBuilder WithMaxIterations(int maxIterations)
    {
        _agent.WithMaxIterations(maxIterations);
        return this;
    }

    public LoopWorkflowBuilder WithContinueCondition(Func<WorkflowContext, bool> condition)
    {
        _agent.WithContinueCondition(condition);
        return this;
    }

    public LoopWorkflowBuilder WithBreakOnError(bool breakOnError = true)
    {
        _agent.WithBreakOnError(breakOnError);
        return this;
    }

    public LoopWorkflowBuilder CollectResults(bool collectResults = true)
    {
        _agent.LoopConfig.CollectResults = collectResults;
        return this;
    }

    public LoopAgent Build()
    {
        return _agent;
    }
}
