namespace Hazina.AI.FaultDetection.Core;

/// <summary>
/// Interface for validating LLM responses
/// </summary>
public interface IResponseValidator
{
    /// <summary>
    /// Validate an LLM response
    /// </summary>
    Task<ValidationResult> ValidateAsync(
        string response,
        ValidationContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate and correct if needed
    /// </summary>
    Task<ValidationResult> ValidateAndCorrectAsync(
        string response,
        ValidationContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Context for validation
/// </summary>
public class ValidationContext
{
    /// <summary>
    /// Original user prompt/query
    /// </summary>
    public string Prompt { get; set; } = string.Empty;

    /// <summary>
    /// Conversation history
    /// </summary>
    public List<HazinaChatMessage> ConversationHistory { get; set; } = new();

    /// <summary>
    /// Expected response format (if known)
    /// </summary>
    public string? ExpectedFormat { get; set; }

    /// <summary>
    /// Expected response type (JSON, Code, Text, etc.)
    /// </summary>
    public ResponseType ResponseType { get; set; } = ResponseType.Text;

    /// <summary>
    /// Domain-specific context
    /// </summary>
    public string? Domain { get; set; }

    /// <summary>
    /// Ground truth facts (if available)
    /// </summary>
    public Dictionary<string, string> GroundTruth { get; set; } = new();

    /// <summary>
    /// Validation rules to apply
    /// </summary>
    public List<ValidationRule> Rules { get; set; } = new();

    /// <summary>
    /// Minimum confidence threshold (0-1)
    /// </summary>
    public double MinConfidenceThreshold { get; set; } = 0.7;
}

/// <summary>
/// Response type enum
/// </summary>
public enum ResponseType
{
    Text,
    Json,
    Code,
    Markdown,
    Xml,
    Html
}

/// <summary>
/// Validation rule
/// </summary>
public class ValidationRule
{
    public string Name { get; set; } = string.Empty;
    public Func<string, Task<bool>> Validator { get; set; } = _ => Task.FromResult(true);
    public string Description { get; set; } = string.Empty;
    public IssueSeverity SeverityIfFailed { get; set; } = IssueSeverity.Error;
}
