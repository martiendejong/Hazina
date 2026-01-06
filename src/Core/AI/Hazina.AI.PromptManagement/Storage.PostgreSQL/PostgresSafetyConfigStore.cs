using System;
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
/// PostgreSQL implementation of safety config storage
/// </summary>
public class PostgresSafetyConfigStore : ISafetyConfigStore
{
    private readonly string _connectionString;

    public PostgresSafetyConfigStore(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    private async Task<IDbConnection> GetConnectionAsync()
    {
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        return connection;
    }

    public async Task<SafetyConfig?> GetConfigAsync(
        string promptId,
        CancellationToken cancellationToken = default)
    {
        using var connection = await GetConnectionAsync();

        const string sql = @"
            SELECT *
            FROM safety_configs
            WHERE prompt_id = @PromptId";

        var result = await connection.QueryFirstOrDefaultAsync<dynamic>(sql, new { PromptId = promptId });

        if (result == null)
        {
            return null;
        }

        return MapToSafetyConfig(result);
    }

    public async Task SaveConfigAsync(
        SafetyConfig config,
        CancellationToken cancellationToken = default)
    {
        using var connection = await GetConnectionAsync();

        const string sql = @"
            INSERT INTO safety_configs (
                prompt_id, cooldown_period, enforce_cooldown,
                min_performance_ratio, enforce_performance_threshold,
                min_semantic_similarity, enforce_semantic_similarity,
                require_sandbox_test, default_test_set_id,
                emergency_stop_active, emergency_stop_reason,
                emergency_stop_activated_at, emergency_stop_activated_by,
                metadata
            )
            VALUES (
                @PromptId, @CooldownPeriod, @EnforceCooldown,
                @MinPerformanceRatio, @EnforcePerformanceThreshold,
                @MinSemanticSimilarity, @EnforceSemanticSimilarity,
                @RequireSandboxTest, @DefaultTestSetId,
                @EmergencyStopActive, @EmergencyStopReason,
                @EmergencyStopActivatedAt, @EmergencyStopActivatedBy,
                @Metadata::jsonb
            )
            ON CONFLICT (prompt_id)
            DO UPDATE SET
                cooldown_period = EXCLUDED.cooldown_period,
                enforce_cooldown = EXCLUDED.enforce_cooldown,
                min_performance_ratio = EXCLUDED.min_performance_ratio,
                enforce_performance_threshold = EXCLUDED.enforce_performance_threshold,
                min_semantic_similarity = EXCLUDED.min_semantic_similarity,
                enforce_semantic_similarity = EXCLUDED.enforce_semantic_similarity,
                require_sandbox_test = EXCLUDED.require_sandbox_test,
                default_test_set_id = EXCLUDED.default_test_set_id,
                emergency_stop_active = EXCLUDED.emergency_stop_active,
                emergency_stop_reason = EXCLUDED.emergency_stop_reason,
                emergency_stop_activated_at = EXCLUDED.emergency_stop_activated_at,
                emergency_stop_activated_by = EXCLUDED.emergency_stop_activated_by,
                metadata = EXCLUDED.metadata";

        await connection.ExecuteAsync(sql, new
        {
            config.PromptId,
            config.CooldownPeriod,
            config.EnforceCooldown,
            config.MinPerformanceRatio,
            config.EnforcePerformanceThreshold,
            config.MinSemanticSimilarity,
            config.EnforceSemanticSimilarity,
            config.RequireSandboxTest,
            config.DefaultTestSetId,
            config.EmergencyStopActive,
            config.EmergencyStopReason,
            config.EmergencyStopActivatedAt,
            config.EmergencyStopActivatedBy,
            Metadata = JsonSerializer.Serialize(config.Metadata)
        });
    }

    public async Task DeleteConfigAsync(
        string promptId,
        CancellationToken cancellationToken = default)
    {
        using var connection = await GetConnectionAsync();

        const string sql = "DELETE FROM safety_configs WHERE prompt_id = @PromptId";

        await connection.ExecuteAsync(sql, new { PromptId = promptId });
    }

    private SafetyConfig MapToSafetyConfig(dynamic record)
    {
        var config = new SafetyConfig
        {
            PromptId = record.prompt_id,
            CooldownPeriod = (TimeSpan)record.cooldown_period,
            EnforceCooldown = record.enforce_cooldown,
            MinPerformanceRatio = record.min_performance_ratio,
            EnforcePerformanceThreshold = record.enforce_performance_threshold,
            MinSemanticSimilarity = record.min_semantic_similarity,
            EnforceSemanticSimilarity = record.enforce_semantic_similarity,
            RequireSandboxTest = record.require_sandbox_test,
            DefaultTestSetId = record.default_test_set_id,
            EmergencyStopActive = record.emergency_stop_active,
            EmergencyStopReason = record.emergency_stop_reason,
            EmergencyStopActivatedAt = record.emergency_stop_activated_at,
            EmergencyStopActivatedBy = record.emergency_stop_activated_by
        };

        if (record.metadata != null)
        {
            try
            {
                config.Metadata = JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, object>>(
                    record.metadata.ToString()
                ) ?? new System.Collections.Generic.Dictionary<string, object>();
            }
            catch
            {
                config.Metadata = new System.Collections.Generic.Dictionary<string, object>();
            }
        }

        return config;
    }
}
