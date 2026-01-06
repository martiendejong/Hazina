using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Hazina.AI.PromptManagement.Safety;
using Npgsql;

namespace Hazina.AI.PromptManagement.Storage.PostgreSQL;

/// <summary>
/// PostgreSQL implementation of sandbox test storage
/// </summary>
public class PostgresSandboxTestStore : ISandboxTestStore
{
    private readonly string _connectionString;

    public PostgresSandboxTestStore(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    private async Task<IDbConnection> GetConnectionAsync()
    {
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        return connection;
    }

    public async Task SaveTestResultAsync(
        SandboxTestResult result,
        CancellationToken cancellationToken = default)
    {
        using var connection = await GetConnectionAsync();

        const string sql = @"
            INSERT INTO sandbox_tests (
                test_id, proposal_id, test_set_id,
                started_at, completed_at,
                baseline_metrics, proposed_metrics,
                improvement_pct, passed, errors, metadata
            )
            VALUES (
                @TestId, @ProposalId, @TestSetId,
                @StartedAt, @CompletedAt,
                @BaselineMetrics::jsonb, @ProposedMetrics::jsonb,
                @ImprovementPct, @Passed, @Errors, @Metadata::jsonb
            )
            ON CONFLICT (test_id)
            DO UPDATE SET
                completed_at = EXCLUDED.completed_at,
                baseline_metrics = EXCLUDED.baseline_metrics,
                proposed_metrics = EXCLUDED.proposed_metrics,
                improvement_pct = EXCLUDED.improvement_pct,
                passed = EXCLUDED.passed,
                errors = EXCLUDED.errors,
                metadata = EXCLUDED.metadata";

        await connection.ExecuteAsync(sql, new
        {
            result.TestId,
            result.ProposalId,
            result.TestSetId,
            result.StartedAt,
            result.CompletedAt,
            BaselineMetrics = JsonSerializer.Serialize(result.BaselineMetrics),
            ProposedMetrics = JsonSerializer.Serialize(result.ProposedMetrics),
            ImprovementPct = result.OverallImprovement,
            Passed = result.MeetsThreshold,
            result.Errors,
            Metadata = JsonSerializer.Serialize(new Dictionary<string, object>(result.Metadata)
            {
                ["improvement_percent"] = result.ImprovementPercent
            })
        });
    }

    public async Task<SandboxTestResult?> GetTestResultAsync(
        string testId,
        CancellationToken cancellationToken = default)
    {
        using var connection = await GetConnectionAsync();

        const string sql = @"
            SELECT *
            FROM sandbox_tests
            WHERE test_id = @TestId";

        var result = await connection.QueryFirstOrDefaultAsync<dynamic>(sql, new { TestId = testId });

        if (result == null)
        {
            return null;
        }

        return MapToSandboxTestResult(result);
    }

    public async Task<List<SandboxTestResult>> GetTestResultsForProposalAsync(
        string proposalId,
        CancellationToken cancellationToken = default)
    {
        using var connection = await GetConnectionAsync();

        const string sql = @"
            SELECT *
            FROM sandbox_tests
            WHERE proposal_id = @ProposalId
            ORDER BY started_at DESC";

        var results = await connection.QueryAsync<dynamic>(sql, new { ProposalId = proposalId });

        return results.Select(r => MapToSandboxTestResult(r)).ToList();
    }

    public async Task<int> DeleteOldTestResultsAsync(
        DateTime olderThan,
        CancellationToken cancellationToken = default)
    {
        using var connection = await GetConnectionAsync();

        const string sql = @"
            DELETE FROM sandbox_tests
            WHERE started_at < @OlderThan";

        return await connection.ExecuteAsync(sql, new { OlderThan = olderThan });
    }

    private SandboxTestResult MapToSandboxTestResult(dynamic record)
    {
        var result = new SandboxTestResult
        {
            TestId = record.test_id,
            ProposalId = record.proposal_id,
            TestSetId = record.test_set_id,
            StartedAt = record.started_at,
            CompletedAt = record.completed_at,
            OverallImprovement = record.improvement_pct,
            MeetsThreshold = record.passed,
            Errors = record.errors
        };

        if (record.baseline_metrics != null)
        {
            try
            {
                result.BaselineMetrics = JsonSerializer.Deserialize<Dictionary<string, double>>(
                    record.baseline_metrics.ToString()
                ) ?? new Dictionary<string, double>();
            }
            catch
            {
                result.BaselineMetrics = new Dictionary<string, double>();
            }
        }

        if (record.proposed_metrics != null)
        {
            try
            {
                result.ProposedMetrics = JsonSerializer.Deserialize<Dictionary<string, double>>(
                    record.proposed_metrics.ToString()
                ) ?? new Dictionary<string, double>();
            }
            catch
            {
                result.ProposedMetrics = new Dictionary<string, double>();
            }
        }

        if (record.metadata != null)
        {
            try
            {
                var metadata = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                    record.metadata.ToString()
                ) ?? new Dictionary<string, JsonElement>();

                result.Metadata = metadata.ToDictionary(
                    kv => kv.Key,
                    kv => (object)kv.Value
                );

                if (metadata.ContainsKey("improvement_percent"))
                {
                    result.ImprovementPercent = JsonSerializer.Deserialize<Dictionary<string, double>>(
                        metadata["improvement_percent"].GetRawText()
                    ) ?? new Dictionary<string, double>();
                }
            }
            catch
            {
                result.Metadata = new Dictionary<string, object>();
            }
        }

        return result;
    }
}
