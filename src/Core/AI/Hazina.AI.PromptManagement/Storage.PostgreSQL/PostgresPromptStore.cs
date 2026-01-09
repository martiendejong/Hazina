using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Hazina.AI.PromptManagement.Core;
using Hazina.AI.PromptManagement.Core.Models;
using Npgsql;

namespace Hazina.AI.PromptManagement.Storage.PostgreSQL;

/// <summary>
/// PostgreSQL implementation of IPromptStore
/// </summary>
public class PostgresPromptStore : IPromptStore
{
    private readonly string _connectionString;
    private readonly ITemplateEngineFactory _templateEngineFactory;

    public PostgresPromptStore(string connectionString, ITemplateEngineFactory templateEngineFactory)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _templateEngineFactory = templateEngineFactory ?? throw new ArgumentNullException(nameof(templateEngineFactory));
    }

    private async Task<IDbConnection> GetConnectionAsync()
    {
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        return connection;
    }

    // ========================================================================
    // Template Management
    // ========================================================================

    public async Task<PromptTemplate?> GetAsync(
        string promptId,
        string? version = null,
        CancellationToken cancellationToken = default)
    {
        using var connection = await GetConnectionAsync();

        if (version == null)
        {
            // Get current version
            const string sql = @"
                SELECT pt.*, pv.template, pv.version_number, pv.variables, pv.created_at as version_created_at
                FROM prompt_templates pt
                JOIN prompt_versions pv ON pt.current_version = pv.version_id
                WHERE pt.prompt_id = @PromptId";

            var result = await connection.QueryFirstOrDefaultAsync<dynamic>(sql, new { PromptId = promptId });
            return result != null ? MapToPromptTemplate(result) : null;
        }
        else
        {
            // Get specific version
            const string sql = @"
                SELECT pt.*, pv.template, pv.version_number, pv.variables, pv.created_at as version_created_at
                FROM prompt_templates pt
                JOIN prompt_versions pv ON pt.prompt_id = pv.prompt_id
                WHERE pt.prompt_id = @PromptId AND pv.version_id = @VersionId";

            var result = await connection.QueryFirstOrDefaultAsync<dynamic>(sql, new { PromptId = promptId, VersionId = version });
            return result != null ? MapToPromptTemplate(result) : null;
        }
    }

    public async Task<List<PromptTemplate>> GetAllAsync(
        string? category = null,
        CancellationToken cancellationToken = default)
    {
        using var connection = await GetConnectionAsync();

        var sql = @"
            SELECT pt.*, pv.template, pv.version_number, pv.variables
            FROM prompt_templates pt
            JOIN prompt_versions pv ON pt.current_version = pv.version_id";

        if (category != null)
        {
            sql += " WHERE pt.category = @Category";
        }

        sql += " ORDER BY pt.updated_at DESC";

        var results = await connection.QueryAsync<dynamic>(sql, new { Category = category });
        return results.Select(MapToPromptTemplate).ToList();
    }

    public async Task<string> CreateAsync(
        PromptTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        using var connection = await GetConnectionAsync();
        using var transaction = connection.BeginTransaction();

        try
        {
            // Generate version ID
            var versionId = GenerateVersionId(request.Id, request.Template, DateTime.UtcNow);

            // Insert template
            const string insertTemplate = @"
                INSERT INTO prompt_templates (prompt_id, current_version, name, description, template_engine, category, metadata)
                VALUES (@PromptId, @VersionId, @Name, @Description, @TemplateEngine, @Category, @Metadata::jsonb)";

            await connection.ExecuteAsync(insertTemplate, new
            {
                PromptId = request.Id,
                VersionId = versionId,
                request.Name,
                request.Description,
                request.TemplateEngine,
                request.Category,
                Metadata = request.Metadata != null ? JsonSerializer.Serialize(request.Metadata) : "{}"
            }, transaction);

            // Insert first version
            const string insertVersion = @"
                INSERT INTO prompt_versions (version_id, prompt_id, version_number, template, variables, created_by, reason, status)
                VALUES (@VersionId, @PromptId, 1, @Template, @Variables::jsonb, @CreatedBy, @Reason, 'active')";

            await connection.ExecuteAsync(insertVersion, new
            {
                VersionId = versionId,
                PromptId = request.Id,
                request.Template,
                Variables = request.Variables != null ? JsonSerializer.Serialize(request.Variables) : "{}",
                request.CreatedBy,
                request.Reason
            }, transaction);

            transaction.Commit();
            return versionId;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<string> UpdateAsync(
        PromptTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        using var connection = await GetConnectionAsync();
        using var transaction = connection.BeginTransaction();

        try
        {
            // Get current version number
            const string getVersionNumberSql = @"
                SELECT COALESCE(MAX(version_number), 0) + 1
                FROM prompt_versions
                WHERE prompt_id = @PromptId";

            var nextVersionNumber = await connection.ExecuteScalarAsync<int>(
                getVersionNumberSql,
                new { PromptId = request.Id },
                transaction);

            // Get current version ID as parent
            const string getCurrentVersionSql = "SELECT current_version FROM prompt_templates WHERE prompt_id = @PromptId";
            var parentVersion = await connection.ExecuteScalarAsync<string>(
                getCurrentVersionSql,
                new { PromptId = request.Id },
                transaction);

            // Generate new version ID
            var newVersionId = GenerateVersionId(request.Id, request.Template, DateTime.UtcNow);

            // Insert new version
            const string insertVersion = @"
                INSERT INTO prompt_versions (version_id, prompt_id, version_number, template, variables, created_by, reason, parent_version, status)
                VALUES (@VersionId, @PromptId, @VersionNumber, @Template, @Variables::jsonb, @CreatedBy, @Reason, @ParentVersion, 'active')";

            await connection.ExecuteAsync(insertVersion, new
            {
                VersionId = newVersionId,
                PromptId = request.Id,
                VersionNumber = nextVersionNumber,
                request.Template,
                Variables = request.Variables != null ? JsonSerializer.Serialize(request.Variables) : "{}",
                request.CreatedBy,
                request.Reason,
                ParentVersion = parentVersion
            }, transaction);

            // Update template to point to new version
            const string updateTemplate = @"
                UPDATE prompt_templates
                SET current_version = @VersionId,
                    name = COALESCE(@Name, name),
                    description = COALESCE(@Description, description),
                    template_engine = COALESCE(@TemplateEngine, template_engine),
                    category = COALESCE(@Category, category),
                    metadata = COALESCE(@Metadata::jsonb, metadata)
                WHERE prompt_id = @PromptId";

            await connection.ExecuteAsync(updateTemplate, new
            {
                VersionId = newVersionId,
                PromptId = request.Id,
                request.Name,
                request.Description,
                request.TemplateEngine,
                request.Category,
                Metadata = request.Metadata != null ? JsonSerializer.Serialize(request.Metadata) : null
            }, transaction);

            transaction.Commit();
            return newVersionId;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task DeleteAsync(string promptId, CancellationToken cancellationToken = default)
    {
        using var connection = await GetConnectionAsync();

        const string sql = "DELETE FROM prompt_templates WHERE prompt_id = @PromptId";
        await connection.ExecuteAsync(sql, new { PromptId = promptId });
    }

    // ========================================================================
    // Version Management
    // ========================================================================

    public async Task<List<PromptVersion>> GetVersionHistoryAsync(
        string promptId,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        using var connection = await GetConnectionAsync();

        const string sql = @"
            SELECT *
            FROM prompt_versions
            WHERE prompt_id = @PromptId
            ORDER BY version_number DESC
            LIMIT @Limit";

        var results = await connection.QueryAsync<dynamic>(sql, new { PromptId = promptId, Limit = limit });
        return results.Select(MapToPromptVersion).ToList();
    }

    public async Task<PromptVersion?> GetVersionAsync(
        string versionId,
        CancellationToken cancellationToken = default)
    {
        using var connection = await GetConnectionAsync();

        const string sql = "SELECT * FROM prompt_versions WHERE version_id = @VersionId";
        var result = await connection.QueryFirstOrDefaultAsync<dynamic>(sql, new { VersionId = versionId });
        return result != null ? MapToPromptVersion(result) : null;
    }

    public async Task<string> RollbackAsync(
        RollbackRequest request,
        CancellationToken cancellationToken = default)
    {
        using var connection = await GetConnectionAsync();
        using var transaction = connection.BeginTransaction();

        try
        {
            // Get current version
            const string getCurrentVersionSql = "SELECT current_version FROM prompt_templates WHERE prompt_id = @PromptId";
            var currentVersion = await connection.ExecuteScalarAsync<string>(
                getCurrentVersionSql,
                new { PromptId = request.PromptId },
                transaction);

            // Update current version
            const string updateSql = "UPDATE prompt_templates SET current_version = @TargetVersion WHERE prompt_id = @PromptId";
            await connection.ExecuteAsync(updateSql, new
            {
                request.PromptId,
                request.TargetVersion
            }, transaction);

            // Record rollback history
            const string insertHistorySql = @"
                INSERT INTO rollback_history (prompt_id, from_version, to_version, reason, initiated_by)
                VALUES (@PromptId, @FromVersion, @ToVersion, @Reason, @InitiatedBy)";

            await connection.ExecuteAsync(insertHistorySql, new
            {
                request.PromptId,
                FromVersion = currentVersion,
                ToVersion = request.TargetVersion,
                request.Reason,
                request.InitiatedBy
            }, transaction);

            transaction.Commit();
            return request.TargetVersion;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<List<RollbackHistory>> GetRollbackHistoryAsync(
        string promptId,
        CancellationToken cancellationToken = default)
    {
        using var connection = await GetConnectionAsync();

        const string sql = @"
            SELECT *
            FROM rollback_history
            WHERE prompt_id = @PromptId
            ORDER BY created_at DESC";

        var results = await connection.QueryAsync<RollbackHistory>(sql, new { PromptId = promptId });
        return results.ToList();
    }

    // ========================================================================
    // Metrics
    // ========================================================================

    public async Task<PromptMetrics?> GetMetricsAsync(
        string promptId,
        string? versionId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default)
    {
        using var connection = await GetConnectionAsync();

        // If no version specified, get current version
        if (versionId == null)
        {
            const string getVersionSql = "SELECT current_version FROM prompt_templates WHERE prompt_id = @PromptId";
            versionId = await connection.ExecuteScalarAsync<string>(getVersionSql, new { PromptId = promptId });
        }

        var sql = @"
            SELECT
                @PromptId as prompt_id,
                @VersionId as version_id,
                SUM(total_uses) as total_uses,
                SUM(success_count) as success_count,
                SUM(failure_count) as failure_count,
                AVG(avg_user_rating) as avg_user_rating,
                AVG(avg_confidence) as avg_confidence,
                AVG(avg_latency_ms) as avg_latency_ms,
                SUM(total_tokens_used) as total_tokens_used,
                SUM(total_cost_usd) as total_cost_usd,
                MAX(timestamp) as timestamp
            FROM prompt_metrics
            WHERE prompt_id = @PromptId AND version_id = @VersionId";

        var parameters = new DynamicParameters();
        parameters.Add("PromptId", promptId);
        parameters.Add("VersionId", versionId);

        if (startDate.HasValue)
        {
            sql += " AND timestamp >= @StartDate";
            parameters.Add("StartDate", startDate.Value);
        }

        if (endDate.HasValue)
        {
            sql += " AND timestamp <= @EndDate";
            parameters.Add("EndDate", endDate.Value);
        }

        sql += " GROUP BY prompt_id, version_id";

        var result = await connection.QueryFirstOrDefaultAsync<PromptMetrics>(sql, parameters);
        return result;
    }

    public async Task RecordUsageAsync(
        string promptId,
        string versionId,
        bool success,
        double? userRating = null,
        double? confidence = null,
        double? latencyMs = null,
        long? tokensUsed = null,
        decimal? costUsd = null,
        CancellationToken cancellationToken = default)
    {
        using var connection = await GetConnectionAsync();

        const string sql = @"
            INSERT INTO prompt_metrics (
                prompt_id, version_id, timestamp,
                total_uses, success_count, failure_count,
                avg_user_rating, avg_confidence, avg_latency_ms,
                total_tokens_used, total_cost_usd
            )
            VALUES (
                @PromptId, @VersionId, CURRENT_DATE,
                1, @SuccessCount, @FailureCount,
                @UserRating, @Confidence, @LatencyMs,
                @TokensUsed, @CostUsd
            )
            ON CONFLICT (prompt_id, version_id, date(timestamp))
            DO UPDATE SET
                total_uses = prompt_metrics.total_uses + 1,
                success_count = prompt_metrics.success_count + @SuccessCount,
                failure_count = prompt_metrics.failure_count + @FailureCount,
                avg_user_rating = CASE
                    WHEN @UserRating IS NOT NULL THEN
                        (COALESCE(prompt_metrics.avg_user_rating, 0) * prompt_metrics.total_uses + @UserRating) / (prompt_metrics.total_uses + 1)
                    ELSE prompt_metrics.avg_user_rating
                END,
                avg_confidence = CASE
                    WHEN @Confidence IS NOT NULL THEN
                        (COALESCE(prompt_metrics.avg_confidence, 0) * prompt_metrics.total_uses + @Confidence) / (prompt_metrics.total_uses + 1)
                    ELSE prompt_metrics.avg_confidence
                END,
                avg_latency_ms = CASE
                    WHEN @LatencyMs IS NOT NULL THEN
                        (COALESCE(prompt_metrics.avg_latency_ms, 0) * prompt_metrics.total_uses + @LatencyMs) / (prompt_metrics.total_uses + 1)
                    ELSE prompt_metrics.avg_latency_ms
                END,
                total_tokens_used = prompt_metrics.total_tokens_used + COALESCE(@TokensUsed, 0),
                total_cost_usd = prompt_metrics.total_cost_usd + COALESCE(@CostUsd, 0)";

        await connection.ExecuteAsync(sql, new
        {
            PromptId = promptId,
            VersionId = versionId,
            SuccessCount = success ? 1 : 0,
            FailureCount = success ? 0 : 1,
            UserRating = userRating,
            Confidence = confidence,
            LatencyMs = latencyMs,
            TokensUsed = tokensUsed ?? 0,
            CostUsd = costUsd ?? 0
        });
    }

    public async Task UpdateEvalMetricsAsync(
        string promptId,
        string versionId,
        Dictionary<string, double> evalMetrics,
        CancellationToken cancellationToken = default)
    {
        using var connection = await GetConnectionAsync();

        const string sql = @"
            UPDATE prompt_metrics
            SET eval_metrics = @EvalMetrics::jsonb
            WHERE prompt_id = @PromptId AND version_id = @VersionId AND date(timestamp) = CURRENT_DATE";

        await connection.ExecuteAsync(sql, new
        {
            PromptId = promptId,
            VersionId = versionId,
            EvalMetrics = JsonSerializer.Serialize(evalMetrics)
        });
    }

    // ========================================================================
    // Template Rendering
    // ========================================================================

    public async Task<string> RenderAsync(
        string promptId,
        Dictionary<string, object> variables,
        string? version = null,
        CancellationToken cancellationToken = default)
    {
        var template = await GetAsync(promptId, version, cancellationToken);
        if (template == null)
        {
            throw new InvalidOperationException($"Prompt template '{promptId}' not found");
        }

        var engine = _templateEngineFactory.GetEngine(template.TemplateEngine);

        // Get the actual template content from the version
        using var connection = await GetConnectionAsync();
        const string sql = "SELECT template FROM prompt_versions WHERE version_id = @VersionId";
        var templateContent = await connection.ExecuteScalarAsync<string>(sql, new { VersionId = template.CurrentVersion });

        return await engine.RenderAsync(templateContent, variables);
    }

    // ========================================================================
    // Helper Methods
    // ========================================================================

    private static string GenerateVersionId(string promptId, string template, DateTime timestamp)
    {
        var input = $"{promptId}:{template}:{timestamp:O}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private static PromptTemplate MapToPromptTemplate(dynamic row)
    {
        return new PromptTemplate
        {
            Id = row.prompt_id,
            Name = row.name,
            Description = row.description,
            CurrentVersion = row.current_version,
            TemplateEngine = row.template_engine,
            Category = row.category,
            CreatedAt = row.created_at,
            UpdatedAt = row.updated_at,
            Metadata = row.metadata != null ? JsonSerializer.Deserialize<Dictionary<string, object>>(row.metadata.ToString()) : null
        };
    }

    private static PromptVersion MapToPromptVersion(dynamic row)
    {
        return new PromptVersion
        {
            VersionId = row.version_id,
            PromptId = row.prompt_id,
            VersionNumber = row.version_number,
            Template = row.template,
            Variables = row.variables != null ? JsonSerializer.Deserialize<Dictionary<string, object>>(row.variables.ToString()) : null,
            CreatedAt = row.created_at,
            CreatedBy = row.created_by,
            Reason = row.reason,
            ParentVersion = row.parent_version,
            Status = row.status,
            Metadata = row.metadata != null ? JsonSerializer.Deserialize<Dictionary<string, object>>(row.metadata.ToString()) : null
        };
    }
}
