using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Hazina.AI.PromptManagement.Approval;
using Npgsql;

namespace Hazina.AI.PromptManagement.Storage.PostgreSQL;

/// <summary>
/// PostgreSQL implementation of approval storage
/// </summary>
public class PostgresApprovalStore : IApprovalStore
{
    private readonly string _connectionString;

    public PostgresApprovalStore(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    private async Task<IDbConnection> GetConnectionAsync()
    {
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        return connection;
    }

    public async Task SaveRequestAsync(
        ApprovalRequest request,
        CancellationToken cancellationToken = default)
    {
        using var connection = await GetConnectionAsync();

        const string sql = @"
            INSERT INTO approval_requests (
                request_id, proposal_id, prompt_id,
                status, risk_level,
                submitted_by, submitted_at,
                assigned_to, reviewed_by, reviewed_at,
                comments, rejection_reason,
                auto_approved, auto_approval_reason,
                metadata
            )
            VALUES (
                @RequestId, @ProposalId, @PromptId,
                @Status, @RiskLevel,
                @SubmittedBy, @SubmittedAt,
                @AssignedTo, @ReviewedBy, @ReviewedAt,
                @Comments, @RejectionReason,
                @AutoApproved, @AutoApprovalReason,
                @Metadata::jsonb
            )
            ON CONFLICT (request_id)
            DO UPDATE SET
                status = EXCLUDED.status,
                assigned_to = EXCLUDED.assigned_to,
                reviewed_by = EXCLUDED.reviewed_by,
                reviewed_at = EXCLUDED.reviewed_at,
                comments = EXCLUDED.comments,
                rejection_reason = EXCLUDED.rejection_reason,
                metadata = EXCLUDED.metadata";

        await connection.ExecuteAsync(sql, new
        {
            request.RequestId,
            request.ProposalId,
            request.PromptId,
            Status = request.Status.ToString(),
            RiskLevel = request.RiskLevel.ToString(),
            request.SubmittedBy,
            request.SubmittedAt,
            request.AssignedTo,
            request.ReviewedBy,
            request.ReviewedAt,
            request.Comments,
            request.RejectionReason,
            request.AutoApproved,
            request.AutoApprovalReason,
            Metadata = JsonSerializer.Serialize(request.Metadata)
        });
    }

    public async Task<ApprovalRequest?> GetRequestAsync(
        string requestId,
        CancellationToken cancellationToken = default)
    {
        using var connection = await GetConnectionAsync();

        const string sql = @"
            SELECT *
            FROM approval_requests
            WHERE request_id = @RequestId";

        var result = await connection.QueryFirstOrDefaultAsync<dynamic>(sql, new { RequestId = requestId });

        if (result == null)
        {
            return null;
        }

        return MapToApprovalRequest(result);
    }

    public async Task<ApprovalRequest?> GetRequestByProposalAsync(
        string proposalId,
        CancellationToken cancellationToken = default)
    {
        using var connection = await GetConnectionAsync();

        const string sql = @"
            SELECT *
            FROM approval_requests
            WHERE proposal_id = @ProposalId
            ORDER BY submitted_at DESC
            LIMIT 1";

        var result = await connection.QueryFirstOrDefaultAsync<dynamic>(sql, new { ProposalId = proposalId });

        if (result == null)
        {
            return null;
        }

        return MapToApprovalRequest(result);
    }

    public async Task<List<ApprovalRequest>> GetPendingRequestsAsync(
        string? approverId = null,
        CancellationToken cancellationToken = default)
    {
        using var connection = await GetConnectionAsync();

        string sql;
        object parameters;

        if (!string.IsNullOrWhiteSpace(approverId))
        {
            sql = @"
                SELECT *
                FROM approval_requests
                WHERE status = 'Pending'
                AND (assigned_to = @ApproverId OR assigned_to IS NULL)
                ORDER BY submitted_at ASC";
            parameters = new { ApproverId = approverId };
        }
        else
        {
            sql = @"
                SELECT *
                FROM approval_requests
                WHERE status = 'Pending'
                ORDER BY submitted_at ASC";
            parameters = new { };
        }

        var results = await connection.QueryAsync<dynamic>(sql, parameters);

        return results.Select(r => MapToApprovalRequest((dynamic)r)).ToList();
    }

    public async Task<List<ApprovalRequest>> GetRequestsForPromptAsync(
        string promptId,
        CancellationToken cancellationToken = default)
    {
        using var connection = await GetConnectionAsync();

        const string sql = @"
            SELECT *
            FROM approval_requests
            WHERE prompt_id = @PromptId
            ORDER BY submitted_at DESC";

        var results = await connection.QueryAsync<dynamic>(sql, new { PromptId = promptId });

        return results.Select(r => MapToApprovalRequest((dynamic)r)).ToList();
    }

    public async Task<int> DeleteOldRequestsAsync(
        DateTime olderThan,
        CancellationToken cancellationToken = default)
    {
        using var connection = await GetConnectionAsync();

        const string sql = @"
            DELETE FROM approval_requests
            WHERE submitted_at < @OlderThan";

        return await connection.ExecuteAsync(sql, new { OlderThan = olderThan });
    }

    public async Task<ApprovalConfig?> GetConfigAsync(
        string promptId,
        CancellationToken cancellationToken = default)
    {
        using var connection = await GetConnectionAsync();

        const string sql = @"
            SELECT *
            FROM approval_configs
            WHERE prompt_id = @PromptId";

        var result = await connection.QueryFirstOrDefaultAsync<dynamic>(sql, new { PromptId = promptId });

        if (result == null)
        {
            return null;
        }

        return MapToApprovalConfig(result);
    }

    public async Task SaveConfigAsync(
        ApprovalConfig config,
        CancellationToken cancellationToken = default)
    {
        using var connection = await GetConnectionAsync();

        const string sql = @"
            INSERT INTO approval_configs (
                prompt_id, require_approval, allow_auto_approval,
                auto_approval_max_semantic_change, auto_approval_min_performance,
                approver_user_ids, approver_roles,
                required_approvals, approval_timeout,
                notify_on_submission, notification_channels,
                metadata
            )
            VALUES (
                @PromptId, @RequireApproval, @AllowAutoApproval,
                @AutoApprovalMaxSemanticChange, @AutoApprovalMinPerformance,
                @ApproverUserIds::jsonb, @ApproverRoles::jsonb,
                @RequiredApprovals, @ApprovalTimeout,
                @NotifyOnSubmission, @NotificationChannels::jsonb,
                @Metadata::jsonb
            )
            ON CONFLICT (prompt_id)
            DO UPDATE SET
                require_approval = EXCLUDED.require_approval,
                allow_auto_approval = EXCLUDED.allow_auto_approval,
                auto_approval_max_semantic_change = EXCLUDED.auto_approval_max_semantic_change,
                auto_approval_min_performance = EXCLUDED.auto_approval_min_performance,
                approver_user_ids = EXCLUDED.approver_user_ids,
                approver_roles = EXCLUDED.approver_roles,
                required_approvals = EXCLUDED.required_approvals,
                approval_timeout = EXCLUDED.approval_timeout,
                notify_on_submission = EXCLUDED.notify_on_submission,
                notification_channels = EXCLUDED.notification_channels,
                metadata = EXCLUDED.metadata";

        await connection.ExecuteAsync(sql, new
        {
            config.PromptId,
            config.RequireApproval,
            config.AllowAutoApproval,
            config.AutoApprovalMaxSemanticChange,
            config.AutoApprovalMinPerformance,
            ApproverUserIds = JsonSerializer.Serialize(config.ApproverUserIds),
            ApproverRoles = JsonSerializer.Serialize(config.ApproverRoles),
            config.RequiredApprovals,
            config.ApprovalTimeout,
            config.NotifyOnSubmission,
            NotificationChannels = JsonSerializer.Serialize(config.NotificationChannels),
            Metadata = JsonSerializer.Serialize(config.Metadata)
        });
    }

    public async Task DeleteConfigAsync(
        string promptId,
        CancellationToken cancellationToken = default)
    {
        using var connection = await GetConnectionAsync();

        const string sql = "DELETE FROM approval_configs WHERE prompt_id = @PromptId";

        await connection.ExecuteAsync(sql, new { PromptId = promptId });
    }

    private ApprovalRequest MapToApprovalRequest(dynamic record)
    {
        var request = new ApprovalRequest
        {
            RequestId = record.request_id,
            ProposalId = record.proposal_id,
            PromptId = record.prompt_id,
            Status = Enum.Parse<ApprovalStatus>(record.status),
            RiskLevel = Enum.Parse<RiskLevel>(record.risk_level),
            SubmittedBy = record.submitted_by,
            SubmittedAt = record.submitted_at,
            AssignedTo = record.assigned_to,
            ReviewedBy = record.reviewed_by,
            ReviewedAt = record.reviewed_at,
            Comments = record.comments,
            RejectionReason = record.rejection_reason,
            AutoApproved = record.auto_approved,
            AutoApprovalReason = record.auto_approval_reason
        };

        if (record.metadata != null)
        {
            try
            {
                request.Metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(
                    record.metadata.ToString()
                ) ?? new Dictionary<string, object>();
            }
            catch
            {
                request.Metadata = new Dictionary<string, object>();
            }
        }

        return request;
    }

    private ApprovalConfig MapToApprovalConfig(dynamic record)
    {
        var config = new ApprovalConfig
        {
            PromptId = record.prompt_id,
            RequireApproval = record.require_approval,
            AllowAutoApproval = record.allow_auto_approval,
            AutoApprovalMaxSemanticChange = record.auto_approval_max_semantic_change,
            AutoApprovalMinPerformance = record.auto_approval_min_performance,
            RequiredApprovals = record.required_approvals,
            ApprovalTimeout = (TimeSpan)record.approval_timeout,
            NotifyOnSubmission = record.notify_on_submission
        };

        if (record.approver_user_ids != null)
        {
            try
            {
                config.ApproverUserIds = JsonSerializer.Deserialize<List<string>>(
                    record.approver_user_ids.ToString()
                ) ?? new List<string>();
            }
            catch
            {
                config.ApproverUserIds = new List<string>();
            }
        }

        if (record.approver_roles != null)
        {
            try
            {
                config.ApproverRoles = JsonSerializer.Deserialize<List<string>>(
                    record.approver_roles.ToString()
                ) ?? new List<string>();
            }
            catch
            {
                config.ApproverRoles = new List<string>();
            }
        }

        if (record.notification_channels != null)
        {
            try
            {
                config.NotificationChannels = JsonSerializer.Deserialize<List<string>>(
                    record.notification_channels.ToString()
                ) ?? new List<string>();
            }
            catch
            {
                config.NotificationChannels = new List<string>();
            }
        }

        if (record.metadata != null)
        {
            try
            {
                config.Metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(
                    record.metadata.ToString()
                ) ?? new Dictionary<string, object>();
            }
            catch
            {
                config.Metadata = new Dictionary<string, object>();
            }
        }

        return config;
    }
}
