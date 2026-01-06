using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Hazina.AI.PromptManagement.Reflection;
using Npgsql;

namespace Hazina.AI.PromptManagement.Storage.PostgreSQL;

/// <summary>
/// PostgreSQL implementation of reflection report storage
/// </summary>
public class PostgresReflectionStore : IReflectionStore
{
    private readonly string _connectionString;

    public PostgresReflectionStore(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    private async Task<IDbConnection> GetConnectionAsync()
    {
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        return connection;
    }

    public async Task SaveReportAsync(
        ReflectionReport report,
        CancellationToken cancellationToken = default)
    {
        using var connection = await GetConnectionAsync();

        const string sql = @"
            INSERT INTO reflection_reports (
                report_id, prompt_id, start_date, end_date, total_runs,
                success_rate, failure_patterns, success_patterns,
                improvement_hypotheses, overall_assessment, created_at, metadata
            )
            VALUES (
                @ReportId, @PromptId, @StartDate, @EndDate, @TotalRuns,
                @SuccessRate, @FailurePatterns::jsonb, @SuccessPatterns::jsonb,
                @ImprovementHypotheses::jsonb, @OverallAssessment, @CreatedAt, @Metadata::jsonb
            )
            ON CONFLICT (report_id)
            DO UPDATE SET
                total_runs = EXCLUDED.total_runs,
                success_rate = EXCLUDED.success_rate,
                failure_patterns = EXCLUDED.failure_patterns,
                success_patterns = EXCLUDED.success_patterns,
                improvement_hypotheses = EXCLUDED.improvement_hypotheses,
                overall_assessment = EXCLUDED.overall_assessment,
                metadata = EXCLUDED.metadata";

        await connection.ExecuteAsync(sql, new
        {
            report.ReportId,
            report.PromptId,
            report.StartDate,
            report.EndDate,
            report.TotalRuns,
            report.SuccessRate,
            FailurePatterns = JsonSerializer.Serialize(report.FailurePatterns),
            SuccessPatterns = JsonSerializer.Serialize(report.SuccessPatterns),
            ImprovementHypotheses = JsonSerializer.Serialize(report.ImprovementHypotheses),
            report.OverallAssessment,
            report.CreatedAt,
            Metadata = JsonSerializer.Serialize(report.Metadata)
        });
    }

    public async Task<ReflectionReport?> GetReportAsync(
        string reportId,
        CancellationToken cancellationToken = default)
    {
        using var connection = await GetConnectionAsync();

        const string sql = @"
            SELECT *
            FROM reflection_reports
            WHERE report_id = @ReportId";

        var result = await connection.QueryFirstOrDefaultAsync<dynamic>(sql, new { ReportId = reportId });

        if (result == null)
        {
            return null;
        }

        return MapToReflectionReport(result);
    }

    public async Task<List<ReflectionReport>> GetReportHistoryAsync(
        string promptId,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        using var connection = await GetConnectionAsync();

        const string sql = @"
            SELECT *
            FROM reflection_reports
            WHERE prompt_id = @PromptId
            ORDER BY created_at DESC
            LIMIT @Limit";

        var results = await connection.QueryAsync<dynamic>(sql, new
        {
            PromptId = promptId,
            Limit = limit
        });

        return results.Select(r => MapToReflectionReport(r)).ToList();
    }

    public async Task<ReflectionReport?> GetLatestReportAsync(
        string promptId,
        CancellationToken cancellationToken = default)
    {
        using var connection = await GetConnectionAsync();

        const string sql = @"
            SELECT *
            FROM reflection_reports
            WHERE prompt_id = @PromptId
            ORDER BY created_at DESC
            LIMIT 1";

        var result = await connection.QueryFirstOrDefaultAsync<dynamic>(sql, new { PromptId = promptId });

        if (result == null)
        {
            return null;
        }

        return MapToReflectionReport(result);
    }

    public async Task<int> DeleteOldReportsAsync(
        DateTime olderThan,
        CancellationToken cancellationToken = default)
    {
        using var connection = await GetConnectionAsync();

        const string sql = @"
            DELETE FROM reflection_reports
            WHERE created_at < @OlderThan";

        return await connection.ExecuteAsync(sql, new { OlderThan = olderThan });
    }

    // Helper method to map database record to ReflectionReport
    private ReflectionReport MapToReflectionReport(dynamic record)
    {
        var report = new ReflectionReport
        {
            ReportId = record.report_id,
            PromptId = record.prompt_id,
            StartDate = record.start_date,
            EndDate = record.end_date,
            TotalRuns = record.total_runs,
            SuccessRate = record.success_rate,
            CreatedAt = record.created_at,
            OverallAssessment = record.overall_assessment ?? ""
        };

        // Parse failure patterns
        if (record.failure_patterns != null)
        {
            try
            {
                report.FailurePatterns = JsonSerializer.Deserialize<List<FailurePatternSummary>>(
                    record.failure_patterns.ToString()
                ) ?? new List<FailurePatternSummary>();
            }
            catch (JsonException)
            {
                report.FailurePatterns = new List<FailurePatternSummary>();
            }
        }

        // Parse success patterns
        if (record.success_patterns != null)
        {
            try
            {
                report.SuccessPatterns = JsonSerializer.Deserialize<List<SuccessPatternSummary>>(
                    record.success_patterns.ToString()
                ) ?? new List<SuccessPatternSummary>();
            }
            catch (JsonException)
            {
                report.SuccessPatterns = new List<SuccessPatternSummary>();
            }
        }

        // Parse improvement hypotheses
        if (record.improvement_hypotheses != null)
        {
            try
            {
                report.ImprovementHypotheses = JsonSerializer.Deserialize<List<ImprovementHypothesis>>(
                    record.improvement_hypotheses.ToString()
                ) ?? new List<ImprovementHypothesis>();
            }
            catch (JsonException)
            {
                report.ImprovementHypotheses = new List<ImprovementHypothesis>();
            }
        }

        // Parse metadata
        if (record.metadata != null)
        {
            try
            {
                report.Metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(
                    record.metadata.ToString()
                ) ?? new Dictionary<string, object>();
            }
            catch (JsonException)
            {
                report.Metadata = new Dictionary<string, object>();
            }
        }

        // Parse average metrics from metadata if available
        if (report.Metadata.ContainsKey("average_metrics"))
        {
            try
            {
                var metricsJson = JsonSerializer.Serialize(report.Metadata["average_metrics"]);
                report.AverageMetrics = JsonSerializer.Deserialize<Dictionary<string, double>>(metricsJson)
                    ?? new Dictionary<string, double>();
            }
            catch
            {
                report.AverageMetrics = new Dictionary<string, double>();
            }
        }

        return report;
    }
}
