using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Hazina.AI.PromptManagement.Evaluation;

/// <summary>
/// Automated evaluation pipeline for prompt templates
/// </summary>
public interface IEvaluationPipeline
{
    /// <summary>
    /// Run evaluation on a specific prompt version
    /// </summary>
    /// <param name="promptId">Prompt ID to evaluate</param>
    /// <param name="versionId">Version ID (optional, defaults to current)</param>
    /// <param name="testSetId">Test set ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Evaluation run result</returns>
    Task<EvaluationRunResult> RunAsync(
        string promptId,
        string testSetId,
        string? versionId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Schedule recurring evaluation runs
    /// </summary>
    /// <param name="promptId">Prompt ID to evaluate</param>
    /// <param name="testSetId">Test set ID</param>
    /// <param name="cronExpression">Cron expression for scheduling</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<string> ScheduleAsync(
        string promptId,
        string testSetId,
        string cronExpression,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancel a scheduled evaluation
    /// </summary>
    /// <param name="scheduleId">Schedule ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task CancelScheduleAsync(
        string scheduleId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all scheduled evaluations
    /// </summary>
    /// <param name="promptId">Optional prompt ID filter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of scheduled evaluations</returns>
    Task<List<EvaluationSchedule>> GetSchedulesAsync(
        string? promptId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Detect performance regressions between two versions
    /// </summary>
    /// <param name="promptId">Prompt ID</param>
    /// <param name="baselineVersionId">Baseline version to compare against</param>
    /// <param name="newVersionId">New version to evaluate</param>
    /// <param name="testSetId">Test set ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Regression analysis report</returns>
    Task<RegressionReport> DetectRegressionsAsync(
        string promptId,
        string baselineVersionId,
        string newVersionId,
        string testSetId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of an evaluation run
/// </summary>
public class EvaluationRunResult
{
    public string RunId { get; set; } = string.Empty;
    public string PromptId { get; set; } = string.Empty;
    public string VersionId { get; set; } = string.Empty;
    public string TestSetId { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Status { get; set; } = "running";  // "running" | "completed" | "failed"
    public Dictionary<string, double>? Metrics { get; set; }
    public List<EvaluationCaseResult>? CaseResults { get; set; }
    public string? Errors { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Result for a single evaluation case
/// </summary>
public class EvaluationCaseResult
{
    public string CaseId { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
    public string Response { get; set; } = string.Empty;
    public string? ExpectedOutput { get; set; }
    public Dictionary<string, double> Scores { get; set; } = new();
    public TimeSpan Duration { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Scheduled evaluation configuration
/// </summary>
public class EvaluationSchedule
{
    public string ScheduleId { get; set; } = string.Empty;
    public string PromptId { get; set; } = string.Empty;
    public string TestSetId { get; set; } = string.Empty;
    public string CronExpression { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastRunAt { get; set; }
    public DateTime? NextRunAt { get; set; }
    public string? LastRunId { get; set; }
}

/// <summary>
/// Regression detection report
/// </summary>
public class RegressionReport
{
    public string ReportId { get; set; } = string.Empty;
    public string PromptId { get; set; } = string.Empty;
    public string BaselineVersionId { get; set; } = string.Empty;
    public string NewVersionId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    // Metrics comparison
    public Dictionary<string, double> BaselineMetrics { get; set; } = new();
    public Dictionary<string, double> NewMetrics { get; set; } = new();
    public Dictionary<string, double> Changes { get; set; } = new();  // % change

    // Regression detection
    public bool HasRegression { get; set; }
    public List<RegressionIssue> Issues { get; set; } = new();

    // Statistical significance
    public Dictionary<string, double>? PValues { get; set; }
    public Dictionary<string, bool>? IsSignificant { get; set; }
}

/// <summary>
/// A specific regression issue detected
/// </summary>
public class RegressionIssue
{
    public string MetricName { get; set; } = string.Empty;
    public double BaselineValue { get; set; }
    public double NewValue { get; set; }
    public double PercentChange { get; set; }
    public string Severity { get; set; } = "medium";  // "low" | "medium" | "high" | "critical"
    public string Description { get; set; } = string.Empty;
}
