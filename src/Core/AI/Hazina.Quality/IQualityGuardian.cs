namespace Hazina.Quality;

/// <summary>
/// Proactive quality monitoring and violation detection
/// </summary>
public interface IQualityGuardian
{
    /// <summary>
    /// Detect quality violations in a workflow
    /// </summary>
    /// <param name="context">Workflow context</param>
    /// <returns>List of violations found</returns>
    Task<List<Violation>> DetectViolations(WorkflowContext context);

    /// <summary>
    /// Post a violation notification
    /// </summary>
    /// <param name="targetId">Where to post (task ID, PR ID, etc)</param>
    /// <param name="violation">Violation details</param>
    Task PostViolation(string targetId, Violation violation);

    /// <summary>
    /// Generate quality report for a time period
    /// </summary>
    /// <param name="period">Time period to analyze</param>
    /// <returns>Quality metrics and trends</returns>
    Task<QualityReport> GenerateReport(TimeSpan period);

    /// <summary>
    /// Register a custom quality check
    /// </summary>
    /// <param name="check">Quality check to register</param>
    Task RegisterCheck(QualityCheck check);
}

/// <summary>
/// Context for workflow quality checks
/// </summary>
public class WorkflowContext
{
    /// <summary>Workflow identifier</summary>
    public required string Id { get; set; }

    /// <summary>Type of workflow</summary>
    public required string Type { get; set; }

    /// <summary>Current state</summary>
    public required string State { get; set; }

    /// <summary>Metadata for checks</summary>
    public Dictionary<string, object> Metadata { get; set; } = [];

    /// <summary>When workflow started</summary>
    public DateTime StartedAt { get; set; }
}

/// <summary>
/// Detected quality violation
/// </summary>
public class Violation
{
    /// <summary>Violation severity</summary>
    public ViolationSeverity Severity { get; set; }

    /// <summary>Type of violation</summary>
    public required string Type { get; set; }

    /// <summary>Human-readable description</summary>
    public required string Description { get; set; }

    /// <summary>Suggested fix</summary>
    public string? SuggestedFix { get; set; }

    /// <summary>When violation was detected</summary>
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Additional context</summary>
    public Dictionary<string, object> Context { get; set; } = [];
}

/// <summary>
/// Severity levels for violations
/// </summary>
public enum ViolationSeverity
{
    Info,
    Warning,
    Error,
    Critical
}

/// <summary>
/// Quality report for a time period
/// </summary>
public class QualityReport
{
    /// <summary>Time period covered</summary>
    public TimeSpan Period { get; set; }

    /// <summary>Total workflows analyzed</summary>
    public int TotalWorkflows { get; set; }

    /// <summary>Violations by severity</summary>
    public Dictionary<ViolationSeverity, int> ViolationsBySeverity { get; set; } = [];

    /// <summary>Violations by type</summary>
    public Dictionary<string, int> ViolationsByType { get; set; } = [];

    /// <summary>Trend direction</summary>
    public TrendDirection Trend { get; set; }

    /// <summary>Generated timestamp</summary>
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Trend direction for quality metrics
/// </summary>
public enum TrendDirection
{
    Improving,
    Stable,
    Degrading
}

/// <summary>
/// Custom quality check definition
/// </summary>
public class QualityCheck
{
    /// <summary>Check identifier</summary>
    public required string Id { get; set; }

    /// <summary>Check name</summary>
    public required string Name { get; set; }

    /// <summary>Check function</summary>
    public required Func<WorkflowContext, Task<Violation?>> CheckFunc { get; set; }

    /// <summary>Severity if violated</summary>
    public ViolationSeverity Severity { get; set; } = ViolationSeverity.Warning;
}
