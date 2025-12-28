using System.Text.Json;
using System.Text.Json.Serialization;
using Hazina.LLMs.GoogleADK.Core;

namespace Hazina.LLMs.GoogleADK.Workflows;

/// <summary>
/// Configuration for defining workflows in JSON/code
/// </summary>
public class WorkflowConfiguration
{
    /// <summary>
    /// Workflow name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Workflow type: Sequential, Parallel, Loop
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public WorkflowType Type { get; set; } = WorkflowType.Sequential;

    /// <summary>
    /// Steps in the workflow
    /// </summary>
    public List<WorkflowStepConfiguration> Steps { get; set; } = new();

    /// <summary>
    /// Configuration specific to workflow type
    /// </summary>
    public Dictionary<string, object> Settings { get; set; } = new();

    /// <summary>
    /// Load workflow configuration from JSON file
    /// </summary>
    public static WorkflowConfiguration LoadFromFile(string filePath)
    {
        var json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<WorkflowConfiguration>(json)
            ?? throw new InvalidOperationException($"Failed to deserialize workflow from {filePath}");
    }

    /// <summary>
    /// Load workflow configuration from JSON string
    /// </summary>
    public static WorkflowConfiguration LoadFromJson(string json)
    {
        return JsonSerializer.Deserialize<WorkflowConfiguration>(json)
            ?? throw new InvalidOperationException("Failed to deserialize workflow configuration");
    }

    /// <summary>
    /// Save workflow configuration to JSON file
    /// </summary>
    public void SaveToFile(string filePath)
    {
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });
        File.WriteAllText(filePath, json);
    }
}

/// <summary>
/// Configuration for a single workflow step
/// </summary>
public class WorkflowStepConfiguration
{
    /// <summary>
    /// Step name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Agent ID to execute
    /// </summary>
    public string? AgentId { get; set; }

    /// <summary>
    /// Input template for the step
    /// </summary>
    public string Input { get; set; } = string.Empty;

    /// <summary>
    /// Continue on error flag
    /// </summary>
    public bool ContinueOnError { get; set; } = false;

    /// <summary>
    /// Condition expression (simple string comparison for now)
    /// </summary>
    public string? ConditionExpression { get; set; }

    /// <summary>
    /// Metadata
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Workflow type enumeration
/// </summary>
public enum WorkflowType
{
    Sequential,
    Parallel,
    Loop
}

/// <summary>
/// Factory for creating workflows from configuration
/// </summary>
public class WorkflowFactory
{
    private readonly AgentRuntime _runtime;

    public WorkflowFactory(AgentRuntime runtime)
    {
        _runtime = runtime;
    }

    /// <summary>
    /// Create a workflow agent from configuration
    /// </summary>
    public WorkflowAgent CreateFromConfiguration(WorkflowConfiguration config)
    {
        var context = _runtime.CreateContext(config.Name);

        WorkflowAgent agent = config.Type switch
        {
            WorkflowType.Sequential => CreateSequentialAgent(config, context),
            WorkflowType.Parallel => CreateParallelAgent(config, context),
            WorkflowType.Loop => CreateLoopAgent(config, context),
            _ => throw new ArgumentException($"Unknown workflow type: {config.Type}")
        };

        return agent;
    }

    private SequentialAgent CreateSequentialAgent(WorkflowConfiguration config, AgentContext context)
    {
        var agent = new SequentialAgent(config.Name, _runtime, context);

        // Apply settings
        if (config.Settings.TryGetValue("stopOnError", out var stopOnError))
        {
            agent.StopOnError = Convert.ToBoolean(stopOnError);
        }

        // Add steps
        foreach (var stepConfig in config.Steps)
        {
            var step = CreateStep(stepConfig);
            agent.AddStep(step);
        }

        return agent;
    }

    private ParallelAgent CreateParallelAgent(WorkflowConfiguration config, AgentContext context)
    {
        var agent = new ParallelAgent(config.Name, _runtime, context);

        // Apply settings
        if (config.Settings.TryGetValue("maxDegreeOfParallelism", out var maxDegree))
        {
            agent.MaxDegreeOfParallelism = Convert.ToInt32(maxDegree);
        }

        if (config.Settings.TryGetValue("waitForAll", out var waitForAll))
        {
            agent.WaitForAll = Convert.ToBoolean(waitForAll);
        }

        // Add steps
        foreach (var stepConfig in config.Steps)
        {
            var step = CreateStep(stepConfig);
            agent.AddStep(step);
        }

        return agent;
    }

    private LoopAgent CreateLoopAgent(WorkflowConfiguration config, AgentContext context)
    {
        var agent = new LoopAgent(config.Name, _runtime, context);

        // Apply settings
        if (config.Settings.TryGetValue("maxIterations", out var maxIterations))
        {
            agent.WithMaxIterations(Convert.ToInt32(maxIterations));
        }

        if (config.Settings.TryGetValue("breakOnError", out var breakOnError))
        {
            agent.WithBreakOnError(Convert.ToBoolean(breakOnError));
        }

        if (config.Settings.TryGetValue("collectResults", out var collectResults))
        {
            agent.LoopConfig.CollectResults = Convert.ToBoolean(collectResults);
        }

        // Add steps
        foreach (var stepConfig in config.Steps)
        {
            var step = CreateStep(stepConfig);
            agent.AddStep(step);
        }

        return agent;
    }

    private WorkflowStep CreateStep(WorkflowStepConfiguration config)
    {
        var step = new WorkflowStep
        {
            Name = config.Name,
            AgentId = config.AgentId,
            Input = config.Input,
            ContinueOnError = config.ContinueOnError,
            Metadata = config.Metadata
        };

        // Parse simple condition expression if provided
        if (!string.IsNullOrEmpty(config.ConditionExpression))
        {
            step.Condition = ParseConditionExpression(config.ConditionExpression);
        }

        return step;
    }

    private Func<WorkflowContext, bool>? ParseConditionExpression(string expression)
    {
        // Simple expression parser for basic conditions
        // Format: "data.key == value" or "iteration < 5"

        if (expression.Contains("iteration <"))
        {
            var parts = expression.Split('<');
            if (parts.Length == 2 && int.TryParse(parts[1].Trim(), out var value))
            {
                return ctx => ctx.Iteration < value;
            }
        }

        if (expression.Contains("iteration >"))
        {
            var parts = expression.Split('>');
            if (parts.Length == 2 && int.TryParse(parts[1].Trim(), out var value))
            {
                return ctx => ctx.Iteration > value;
            }
        }

        // Default: always true
        return _ => true;
    }
}
