using Hazina.AI.Agents.Core;

namespace Hazina.AI.Workflows.Configuration;

/// <summary>
/// Complete configuration for a single workflow step
/// </summary>
public class WorkflowStepConfig
{
    // Step Identity
    public string Name { get; set; } = string.Empty;
    public StepType Type { get; set; }
    public string AgentName { get; set; } = string.Empty;
    public string Input { get; set; } = string.Empty;
    public string OutputKey { get; set; } = string.Empty;
    public bool ContinueOnFailure { get; set; } = false;
    public int StepTimeout { get; set; } = 60000; // milliseconds

    // LLM Configuration
    public LLMStepConfig? LLMConfig { get; set; }

    // RAG Configuration
    public RAGStepConfig? RAGConfig { get; set; }

    // Guardrails
    public List<string> Guardrails { get; set; } = new();

    // Conditional Branching
    public WorkflowCondition? Condition { get; set; }
    public WorkflowStepConfig? ThenStep { get; set; }
    public WorkflowStepConfig? ElseStep { get; set; }

    // Loop Configuration
    public WorkflowStepConfig? LoopStep { get; set; }
    public WorkflowCondition? LoopCondition { get; set; }
    public int? MaxIterations { get; set; }

    // Parallel Steps
    public List<WorkflowStepConfig>? ParallelSteps { get; set; }
}

/// <summary>
/// LLM configuration for a step
/// </summary>
public class LLMStepConfig
{
    public string Model { get; set; } = "gpt-3.5-turbo";
    public string? FallbackModel { get; set; }
    public float Temperature { get; set; } = 0.7f;
    public int MaxTokens { get; set; } = 1000;
    public float TopP { get; set; } = 1.0f;
    public float FrequencyPenalty { get; set; } = 0.0f;
    public float PresencePenalty { get; set; } = 0.0f;
}

/// <summary>
/// RAG configuration for a step
/// </summary>
public class RAGStepConfig
{
    public string StoreName { get; set; } = string.Empty;
    public int TopK { get; set; } = 5;
    public double MinSimilarity { get; set; } = 0.7;
    public bool UseEmbeddings { get; set; } = true;
    public string? MetadataFilter { get; set; }
    public int MaxContextLength { get; set; } = 4000;
}

/// <summary>
/// Complete workflow configuration
/// </summary>
public class WorkflowConfig
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Version { get; set; } = "2.0";
    public List<WorkflowStepConfig> Steps { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Workflow condition for branching
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
    Exists,
    GreaterThan,
    LessThan
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
