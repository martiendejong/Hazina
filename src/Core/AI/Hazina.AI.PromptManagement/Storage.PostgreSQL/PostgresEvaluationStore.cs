using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Hazina.AI.PromptManagement.Evaluation;
using Npgsql;

namespace Hazina.AI.PromptManagement.Storage.PostgreSQL;

/// <summary>
/// PostgreSQL implementation of IEvaluationStore
/// </summary>
public class PostgresEvaluationStore : IEvaluationStore
{
    private readonly string _connectionString;

    public PostgresEvaluationStore(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    private async Task<IDbConnection> GetConnectionAsync()
    {
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        return connection;
    }

    // ========================================================================
    // Test Sets
    // ========================================================================

    public async Task<TestSet?> GetTestSetAsync(string testSetId, CancellationToken cancellationToken = default)
    {
        using var connection = await GetConnectionAsync();

        const string sql = "SELECT * FROM eval_test_sets WHERE test_set_id = @TestSetId";
        var result = await connection.QueryFirstOrDefaultAsync<dynamic>(sql, new { TestSetId = testSetId });

        return result != null ? MapToTestSet(result) : null;
    }

    public async Task<List<TestSet>> GetTestSetsAsync(string? category = null, CancellationToken cancellationToken = default)
    {
        using var connection = await GetConnectionAsync();

        var sql = "SELECT * FROM eval_test_sets";

        if (category != null)
        {
            sql += " WHERE category = @Category";
        }

        sql += " ORDER BY updated_at DESC";

        var results = await connection.QueryAsync<dynamic>(sql, new { Category = category });
        return results.Select(MapToTestSet).ToList();
    }

    public async Task<string> SaveTestSetAsync(TestSet testSet, CancellationToken cancellationToken = default)
    {
        using var connection = await GetConnectionAsync();

        const string sql = @"
            INSERT INTO eval_test_sets (test_set_id, name, description, cases, rubrics, category, metadata)
            VALUES (@TestSetId, @Name, @Description, @Cases::jsonb, @Rubrics::jsonb, @Category, @Metadata::jsonb)
            ON CONFLICT (test_set_id)
            DO UPDATE SET
                name = EXCLUDED.name,
                description = EXCLUDED.description,
                cases = EXCLUDED.cases,
                rubrics = EXCLUDED.rubrics,
                category = EXCLUDED.category,
                metadata = EXCLUDED.metadata,
                updated_at = CURRENT_TIMESTAMP";

        await connection.ExecuteAsync(sql, new
        {
            TestSetId = testSet.Id,
            testSet.Name,
            testSet.Description,
            Cases = JsonSerializer.Serialize(testSet.Cases),
            Rubrics = testSet.Rubrics != null ? JsonSerializer.Serialize(testSet.Rubrics) : null,
            testSet.Category,
            Metadata = testSet.Metadata != null ? JsonSerializer.Serialize(testSet.Metadata) : "{}"
        });

        return testSet.Id;
    }

    public async Task DeleteTestSetAsync(string testSetId, CancellationToken cancellationToken = default)
    {
        using var connection = await GetConnectionAsync();

        const string sql = "DELETE FROM eval_test_sets WHERE test_set_id = @TestSetId";
        await connection.ExecuteAsync(sql, new { TestSetId = testSetId });
    }

    // ========================================================================
    // Evaluation Runs
    // ========================================================================

    public async Task<EvaluationRunResult?> GetRunAsync(string runId, CancellationToken cancellationToken = default)
    {
        using var connection = await GetConnectionAsync();

        const string sql = "SELECT * FROM eval_runs WHERE run_id = @RunId";
        var result = await connection.QueryFirstOrDefaultAsync<dynamic>(sql, new { RunId = runId });

        return result != null ? MapToEvaluationRun(result) : null;
    }

    public async Task<List<EvaluationRunResult>> GetRunsAsync(
        string? promptId = null,
        string? versionId = null,
        CancellationToken cancellationToken = default)
    {
        using var connection = await GetConnectionAsync();

        var sql = "SELECT * FROM eval_runs WHERE 1=1";
        var parameters = new DynamicParameters();

        if (promptId != null)
        {
            sql += " AND prompt_id = @PromptId";
            parameters.Add("PromptId", promptId);
        }

        if (versionId != null)
        {
            sql += " AND version_id = @VersionId";
            parameters.Add("VersionId", versionId);
        }

        sql += " ORDER BY started_at DESC LIMIT 100";

        var results = await connection.QueryAsync<dynamic>(sql, parameters);
        return results.Select(MapToEvaluationRun).ToList();
    }

    public async Task SaveRunAsync(EvaluationRunResult run, CancellationToken cancellationToken = default)
    {
        using var connection = await GetConnectionAsync();

        const string sql = @"
            INSERT INTO eval_runs (
                run_id, test_set_id, prompt_id, version_id,
                started_at, completed_at, status, metrics, case_results, errors, metadata
            )
            VALUES (
                @RunId, @TestSetId, @PromptId, @VersionId,
                @StartedAt, @CompletedAt, @Status, @Metrics::jsonb, @CaseResults::jsonb, @Errors, @Metadata::jsonb
            )
            ON CONFLICT (run_id)
            DO UPDATE SET
                completed_at = EXCLUDED.completed_at,
                status = EXCLUDED.status,
                metrics = EXCLUDED.metrics,
                case_results = EXCLUDED.case_results,
                errors = EXCLUDED.errors,
                metadata = EXCLUDED.metadata";

        await connection.ExecuteAsync(sql, new
        {
            run.RunId,
            run.TestSetId,
            run.PromptId,
            run.VersionId,
            run.StartedAt,
            run.CompletedAt,
            run.Status,
            Metrics = run.Metrics != null ? JsonSerializer.Serialize(run.Metrics) : null,
            CaseResults = run.CaseResults != null ? JsonSerializer.Serialize(run.CaseResults) : null,
            run.Errors,
            Metadata = run.Metadata != null ? JsonSerializer.Serialize(run.Metadata) : "{}"
        });
    }

    // ========================================================================
    // Schedules
    // ========================================================================

    public async Task<EvaluationSchedule?> GetScheduleAsync(string scheduleId, CancellationToken cancellationToken = default)
    {
        using var connection = await GetConnectionAsync();

        const string sql = "SELECT * FROM evaluation_schedules WHERE schedule_id = @ScheduleId";
        var result = await connection.QueryFirstOrDefaultAsync<EvaluationSchedule>(sql, new { ScheduleId = scheduleId });

        return result;
    }

    public async Task<List<EvaluationSchedule>> GetSchedulesAsync(string? promptId = null, CancellationToken cancellationToken = default)
    {
        using var connection = await GetConnectionAsync();

        var sql = "SELECT * FROM evaluation_schedules";

        if (promptId != null)
        {
            sql += " WHERE prompt_id = @PromptId";
        }

        sql += " ORDER BY created_at DESC";

        var results = await connection.QueryAsync<EvaluationSchedule>(sql, new { PromptId = promptId });
        return results.ToList();
    }

    public async Task SaveScheduleAsync(EvaluationSchedule schedule, CancellationToken cancellationToken = default)
    {
        using var connection = await GetConnectionAsync();

        const string sql = @"
            INSERT INTO evaluation_schedules (
                schedule_id, prompt_id, test_set_id, cron_expression,
                is_active, created_at, last_run_at, next_run_at, last_run_id
            )
            VALUES (
                @ScheduleId, @PromptId, @TestSetId, @CronExpression,
                @IsActive, @CreatedAt, @LastRunAt, @NextRunAt, @LastRunId
            )
            ON CONFLICT (schedule_id)
            DO UPDATE SET
                is_active = EXCLUDED.is_active,
                last_run_at = EXCLUDED.last_run_at,
                next_run_at = EXCLUDED.next_run_at,
                last_run_id = EXCLUDED.last_run_id";

        await connection.ExecuteAsync(sql, schedule);
    }

    // ========================================================================
    // Regression Reports
    // ========================================================================

    public async Task<RegressionReport?> GetRegressionReportAsync(string reportId, CancellationToken cancellationToken = default)
    {
        using var connection = await GetConnectionAsync();

        const string sql = "SELECT * FROM regression_reports WHERE report_id = @ReportId";
        var result = await connection.QueryFirstOrDefaultAsync<dynamic>(sql, new { ReportId = reportId });

        return result != null ? MapToRegressionReport(result) : null;
    }

    public async Task<List<RegressionReport>> GetRegressionReportsAsync(string? promptId = null, CancellationToken cancellationToken = default)
    {
        using var connection = await GetConnectionAsync();

        var sql = "SELECT * FROM regression_reports";

        if (promptId != null)
        {
            sql += " WHERE prompt_id = @PromptId";
        }

        sql += " ORDER BY created_at DESC LIMIT 100";

        var results = await connection.QueryAsync<dynamic>(sql, new { PromptId = promptId });
        return results.Select(MapToRegressionReport).ToList();
    }

    public async Task SaveRegressionReportAsync(RegressionReport report, CancellationToken cancellationToken = default)
    {
        using var connection = await GetConnectionAsync();

        const string sql = @"
            INSERT INTO regression_reports (
                report_id, prompt_id, baseline_version_id, new_version_id,
                created_at, baseline_metrics, new_metrics, changes,
                has_regression, issues, p_values, is_significant
            )
            VALUES (
                @ReportId, @PromptId, @BaselineVersionId, @NewVersionId,
                @CreatedAt, @BaselineMetrics::jsonb, @NewMetrics::jsonb, @Changes::jsonb,
                @HasRegression, @Issues::jsonb, @PValues::jsonb, @IsSignificant::jsonb
            )";

        await connection.ExecuteAsync(sql, new
        {
            report.ReportId,
            report.PromptId,
            report.BaselineVersionId,
            report.NewVersionId,
            report.CreatedAt,
            BaselineMetrics = JsonSerializer.Serialize(report.BaselineMetrics),
            NewMetrics = JsonSerializer.Serialize(report.NewMetrics),
            Changes = JsonSerializer.Serialize(report.Changes),
            report.HasRegression,
            Issues = JsonSerializer.Serialize(report.Issues),
            PValues = report.PValues != null ? JsonSerializer.Serialize(report.PValues) : null,
            IsSignificant = report.IsSignificant != null ? JsonSerializer.Serialize(report.IsSignificant) : null
        });
    }

    // ========================================================================
    // Helper Methods
    // ========================================================================

    private static TestSet MapToTestSet(dynamic row)
    {
        return new TestSet
        {
            Id = row.test_set_id,
            Name = row.name,
            Description = row.description,
            Cases = JsonSerializer.Deserialize<List<TestCase>>(row.cases.ToString()) ?? new List<TestCase>(),
            Rubrics = row.rubrics != null
                ? JsonSerializer.Deserialize<List<string>>(row.rubrics.ToString())
                : null,
            Category = row.category,
            CreatedAt = row.created_at,
            UpdatedAt = row.updated_at,
            Metadata = row.metadata != null
                ? JsonSerializer.Deserialize<Dictionary<string, object>>(row.metadata.ToString())
                : null
        };
    }

    private static EvaluationRunResult MapToEvaluationRun(dynamic row)
    {
        return new EvaluationRunResult
        {
            RunId = row.run_id,
            PromptId = row.prompt_id,
            VersionId = row.version_id,
            TestSetId = row.test_set_id,
            StartedAt = row.started_at,
            CompletedAt = row.completed_at,
            Status = row.status,
            Metrics = row.metrics != null
                ? JsonSerializer.Deserialize<Dictionary<string, double>>(row.metrics.ToString())
                : null,
            CaseResults = row.case_results != null
                ? JsonSerializer.Deserialize<List<EvaluationCaseResult>>(row.case_results.ToString())
                : null,
            Errors = row.errors,
            Metadata = row.metadata != null
                ? JsonSerializer.Deserialize<Dictionary<string, object>>(row.metadata.ToString())
                : null
        };
    }

    private static RegressionReport MapToRegressionReport(dynamic row)
    {
        return new RegressionReport
        {
            ReportId = row.report_id,
            PromptId = row.prompt_id,
            BaselineVersionId = row.baseline_version_id,
            NewVersionId = row.new_version_id,
            CreatedAt = row.created_at,
            BaselineMetrics = JsonSerializer.Deserialize<Dictionary<string, double>>(row.baseline_metrics.ToString()) ?? new(),
            NewMetrics = JsonSerializer.Deserialize<Dictionary<string, double>>(row.new_metrics.ToString()) ?? new(),
            Changes = JsonSerializer.Deserialize<Dictionary<string, double>>(row.changes.ToString()) ?? new(),
            HasRegression = row.has_regression,
            Issues = JsonSerializer.Deserialize<List<RegressionIssue>>(row.issues.ToString()) ?? new(),
            PValues = row.p_values != null
                ? JsonSerializer.Deserialize<Dictionary<string, double>>(row.p_values.ToString())
                : null,
            IsSignificant = row.is_significant != null
                ? JsonSerializer.Deserialize<Dictionary<string, bool>>(row.is_significant.ToString())
                : null
        };
    }
}
