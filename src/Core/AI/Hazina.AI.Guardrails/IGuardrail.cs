namespace Hazina.AI.Guardrails;

/// <summary>
/// Defines the stage at which a guardrail is executed
/// </summary>
public enum GuardrailStage
{
    /// <summary>
    /// Before LLM call - validates inputs
    /// </summary>
    PreExecution,

    /// <summary>
    /// After LLM call - validates outputs
    /// </summary>
    PostExecution
}

/// <summary>
/// Core interface for implementing guardrails (safety, validation, content filtering)
/// </summary>
public interface IGuardrail
{
    /// <summary>
    /// Unique name of the guardrail (e.g., "no-pii", "token-limit")
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Execution stage for this guardrail
    /// </summary>
    GuardrailStage Stage { get; }

    /// <summary>
    /// Validate content against guardrail rules
    /// </summary>
    Task<GuardrailResult> ValidateAsync(
        string content,
        GuardrailContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of guardrail validation
/// </summary>
public class GuardrailResult
{
    /// <summary>
    /// Whether validation passed
    /// </summary>
    public bool Passed { get; set; }

    /// <summary>
    /// Reason for failure (null if passed)
    /// </summary>
    public string? FailureReason { get; set; }

    /// <summary>
    /// Additional metadata about the validation
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Context information for guardrail execution
/// </summary>
public class GuardrailContext
{
    /// <summary>
    /// Name of the workflow being executed
    /// </summary>
    public string WorkflowName { get; set; } = string.Empty;

    /// <summary>
    /// Name of the current step
    /// </summary>
    public string StepName { get; set; } = string.Empty;

    /// <summary>
    /// Additional parameters for guardrail configuration
    /// </summary>
    public Dictionary<string, object> Parameters { get; set; } = new();
}
