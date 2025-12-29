namespace Hazina.Neurochain.Core.Learning;

/// <summary>
/// Record of a reasoning failure for learning purposes
/// </summary>
public class FailureRecord
{
    /// <summary>
    /// Unique failure ID
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Timestamp of failure
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Original prompt that led to failure
    /// </summary>
    public string Prompt { get; set; } = string.Empty;

    /// <summary>
    /// Failed response
    /// </summary>
    public string Response { get; set; } = string.Empty;

    /// <summary>
    /// Expected/correct response (if known)
    /// </summary>
    public string? ExpectedResponse { get; set; }

    /// <summary>
    /// Failure category
    /// </summary>
    public FailureCategory Category { get; set; }

    /// <summary>
    /// Severity of the failure (0-1)
    /// </summary>
    public double Severity { get; set; }

    /// <summary>
    /// Layer that failed
    /// </summary>
    public string? FailedLayer { get; set; }

    /// <summary>
    /// Provider that failed
    /// </summary>
    public string? FailedProvider { get; set; }

    /// <summary>
    /// Validation issues identified
    /// </summary>
    public List<ValidationIssue> ValidationIssues { get; set; } = new();

    /// <summary>
    /// Reasoning context at time of failure
    /// </summary>
    public Dictionary<string, object> Context { get; set; } = new();

    /// <summary>
    /// Root cause analysis
    /// </summary>
    public string? RootCause { get; set; }

    /// <summary>
    /// Whether this failure has been resolved
    /// </summary>
    public bool Resolved { get; set; }

    /// <summary>
    /// Resolution details
    /// </summary>
    public string? Resolution { get; set; }

    /// <summary>
    /// How the failure was resolved
    /// </summary>
    public ResolutionMethod? ResolutionMethod { get; set; }

    /// <summary>
    /// Tags for categorization
    /// </summary>
    public List<string> Tags { get; set; } = new();
}

/// <summary>
/// Categories of failures
/// </summary>
public enum FailureCategory
{
    /// <summary>
    /// Hallucination - fabricated or false information
    /// </summary>
    Hallucination,

    /// <summary>
    /// Logical error in reasoning
    /// </summary>
    LogicalError,

    /// <summary>
    /// Contradiction with known facts
    /// </summary>
    Contradiction,

    /// <summary>
    /// Low confidence result
    /// </summary>
    LowConfidence,

    /// <summary>
    /// Format error (invalid JSON, etc.)
    /// </summary>
    FormatError,

    /// <summary>
    /// Timeout or performance issue
    /// </summary>
    Performance,

    /// <summary>
    /// Provider or API failure
    /// </summary>
    ProviderFailure,

    /// <summary>
    /// Other/unknown failure
    /// </summary>
    Other
}

/// <summary>
/// How a failure was resolved
/// </summary>
public enum ResolutionMethod
{
    /// <summary>
    /// Retry with same parameters succeeded
    /// </summary>
    Retry,

    /// <summary>
    /// Retry with different provider succeeded
    /// </summary>
    ProviderSwitch,

    /// <summary>
    /// Retry with refined prompt succeeded
    /// </summary>
    PromptRefinement,

    /// <summary>
    /// Used different reasoning layer
    /// </summary>
    LayerSwitch,

    /// <summary>
    /// Increased confidence threshold
    /// </summary>
    ConfidenceAdjustment,

    /// <summary>
    /// Manual intervention
    /// </summary>
    Manual,

    /// <summary>
    /// Cross-validation caught the issue
    /// </summary>
    CrossValidation,

    /// <summary>
    /// Not resolved
    /// </summary>
    Unresolved
}
