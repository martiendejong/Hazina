using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Hazina.AI.PromptManagement.Evaluation;

/// <summary>
/// Storage interface for evaluation data
/// </summary>
public interface IEvaluationStore
{
    // Test Sets
    Task<TestSet?> GetTestSetAsync(string testSetId, CancellationToken cancellationToken = default);
    Task<List<TestSet>> GetTestSetsAsync(string? category = null, CancellationToken cancellationToken = default);
    Task<string> SaveTestSetAsync(TestSet testSet, CancellationToken cancellationToken = default);
    Task DeleteTestSetAsync(string testSetId, CancellationToken cancellationToken = default);

    // Evaluation Runs
    Task<EvaluationRunResult?> GetRunAsync(string runId, CancellationToken cancellationToken = default);
    Task<List<EvaluationRunResult>> GetRunsAsync(string? promptId = null, string? versionId = null, CancellationToken cancellationToken = default);
    Task SaveRunAsync(EvaluationRunResult run, CancellationToken cancellationToken = default);

    // Schedules
    Task<EvaluationSchedule?> GetScheduleAsync(string scheduleId, CancellationToken cancellationToken = default);
    Task<List<EvaluationSchedule>> GetSchedulesAsync(string? promptId = null, CancellationToken cancellationToken = default);
    Task SaveScheduleAsync(EvaluationSchedule schedule, CancellationToken cancellationToken = default);

    // Regression Reports
    Task<RegressionReport?> GetRegressionReportAsync(string reportId, CancellationToken cancellationToken = default);
    Task<List<RegressionReport>> GetRegressionReportsAsync(string? promptId = null, CancellationToken cancellationToken = default);
    Task SaveRegressionReportAsync(RegressionReport report, CancellationToken cancellationToken = default);
}

/// <summary>
/// Test set with test cases and rubrics
/// </summary>
public class TestSet
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<TestCase> Cases { get; set; } = new();
    public List<string>? Rubrics { get; set; }  // Names of rubrics to use
    public string? Category { get; set; }
    public System.DateTime CreatedAt { get; set; }
    public System.DateTime UpdatedAt { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Individual test case
/// </summary>
public class TestCase
{
    public string Id { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
    public string? ExpectedOutput { get; set; }
    public Dictionary<string, object>? Variables { get; set; }  // For template rendering
    public Dictionary<string, object>? Metadata { get; set; }
}
