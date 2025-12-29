namespace Hazina.AI.FaultDetection.Core;

/// <summary>
/// Result of LLM response validation
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; set; }
    public double ConfidenceScore { get; set; } = 1.0; // 0-1, higher is better
    public List<ValidationIssue> Issues { get; set; } = new();
    public string? CorrectedResponse { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();

    public bool HasIssues => Issues.Any();
    public bool HasCriticalIssues => Issues.Any(i => i.Severity == IssueSeverity.Critical);
    public bool RequiresCorrection => !IsValid || HasCriticalIssues;

    public static ValidationResult Valid(double confidence = 1.0) =>
        new() { IsValid = true, ConfidenceScore = confidence };

    public static ValidationResult Invalid(string reason, IssueSeverity severity = IssueSeverity.Error) =>
        new()
        {
            IsValid = false,
            Issues = new List<ValidationIssue>
            {
                new() { Description = reason, Severity = severity }
            }
        };
}

/// <summary>
/// Validation issue details
/// </summary>
public class ValidationIssue
{
    public string Description { get; set; } = string.Empty;
    public IssueSeverity Severity { get; set; } = IssueSeverity.Warning;
    public IssueCategory Category { get; set; } = IssueCategory.General;
    public string? SuggestedFix { get; set; }
    public int? LineNumber { get; set; }
    public int? CharacterPosition { get; set; }
}

/// <summary>
/// Severity of validation issue
/// </summary>
public enum IssueSeverity
{
    Info,
    Warning,
    Error,
    Critical
}

/// <summary>
/// Category of validation issue
/// </summary>
public enum IssueCategory
{
    General,
    Hallucination,
    LogicalInconsistency,
    FormatError,
    MissingInformation,
    Contradiction,
    FactualError,
    SyntaxError,
    TypeMismatch
}
