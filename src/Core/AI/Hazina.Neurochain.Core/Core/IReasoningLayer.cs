namespace Hazina.Neurochain.Core;

/// <summary>
/// Interface for reasoning layers in the Neurochain
/// Each layer provides independent reasoning with different characteristics
/// </summary>
public interface IReasoningLayer
{
    /// <summary>
    /// Layer name (e.g., "Fast", "Deep", "Verification")
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Layer type
    /// </summary>
    LayerType Type { get; }

    /// <summary>
    /// Typical response time category
    /// </summary>
    ResponseSpeed Speed { get; }

    /// <summary>
    /// Typical cost category
    /// </summary>
    CostLevel Cost { get; }

    /// <summary>
    /// Execute reasoning on the given prompt
    /// </summary>
    Task<ReasoningResult> ReasonAsync(
        string prompt,
        ReasoningContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate another layer's reasoning
    /// </summary>
    Task<ValidationResult> ValidateAsync(
        ReasoningResult result,
        ReasoningContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Type of reasoning layer
/// </summary>
public enum LayerType
{
    /// <summary>
    /// Fast, initial reasoning layer
    /// </summary>
    Fast,

    /// <summary>
    /// Deep, thorough reasoning layer
    /// </summary>
    Deep,

    /// <summary>
    /// Verification and cross-validation layer
    /// </summary>
    Verification
}

/// <summary>
/// Response speed category
/// </summary>
public enum ResponseSpeed
{
    Fast,      // < 2 seconds
    Medium,    // 2-10 seconds
    Slow       // > 10 seconds
}

/// <summary>
/// Cost level category
/// </summary>
public enum CostLevel
{
    Low,       // < $0.001 per request
    Medium,    // $0.001 - $0.01 per request
    High       // > $0.01 per request
}

/// <summary>
/// Validation result from a layer
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Whether the reasoning is valid
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Confidence in the validation (0-1)
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Issues identified
    /// </summary>
    public List<ValidationIssue> Issues { get; set; } = new();

    /// <summary>
    /// Suggestions for improvement
    /// </summary>
    public List<string> Suggestions { get; set; } = new();
}

/// <summary>
/// A validation issue
/// </summary>
public class ValidationIssue
{
    /// <summary>
    /// Issue type
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Issue description
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Severity (0-1, where 1 is critical)
    /// </summary>
    public double Severity { get; set; }

    /// <summary>
    /// Location in reasoning chain (if applicable)
    /// </summary>
    public int? StepIndex { get; set; }
}

/// <summary>
/// Context for reasoning
/// </summary>
public class ReasoningContext
{
    /// <summary>
    /// Conversation history
    /// </summary>
    public List<HazinaChatMessage> History { get; set; } = new();

    /// <summary>
    /// Ground truth facts
    /// </summary>
    public Dictionary<string, string> GroundTruth { get; set; } = new();

    /// <summary>
    /// Required confidence threshold
    /// </summary>
    public double MinConfidence { get; set; } = 0.7;

    /// <summary>
    /// Maximum reasoning steps
    /// </summary>
    public int MaxSteps { get; set; } = 10;

    /// <summary>
    /// Whether to include reasoning chain
    /// </summary>
    public bool IncludeReasoning { get; set; } = true;

    /// <summary>
    /// Domain-specific context
    /// </summary>
    public string? Domain { get; set; }

    /// <summary>
    /// Additional metadata
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}
