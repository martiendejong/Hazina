using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Hazina.AI.PromptManagement.Rewriter;
using Npgsql;

namespace Hazina.AI.PromptManagement.Storage.PostgreSQL;

/// <summary>
/// PostgreSQL implementation of proposal storage
/// </summary>
public class PostgresProposalStore : IProposalStore
{
    private readonly string _connectionString;

    public PostgresProposalStore(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    private async Task<IDbConnection> GetConnectionAsync()
    {
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        return connection;
    }

    public async Task SaveProposalAsync(
        PromptProposal proposal,
        CancellationToken cancellationToken = default)
    {
        using var connection = await GetConnectionAsync();

        const string sql = @"
            INSERT INTO prompt_proposals (
                proposal_id, prompt_id, current_version, proposed_template,
                proposed_version, changes, rationale, expected_improvement,
                status, created_at, created_by, reflection_report_id, metadata
            )
            VALUES (
                @ProposalId, @PromptId, @CurrentVersion, @ProposedTemplate,
                @ProposedVersion, @Changes::jsonb, @Rationale, @ExpectedImprovement,
                @Status, @CreatedAt, @CreatedBy, @ReflectionReportId, @Metadata::jsonb
            )
            ON CONFLICT (proposal_id)
            DO UPDATE SET
                status = EXCLUDED.status,
                changes = EXCLUDED.changes,
                rationale = EXCLUDED.rationale,
                expected_improvement = EXCLUDED.expected_improvement,
                metadata = EXCLUDED.metadata";

        await connection.ExecuteAsync(sql, new
        {
            proposal.ProposalId,
            proposal.PromptId,
            proposal.CurrentVersion,
            proposal.ProposedTemplate,
            ProposedVersion = proposal.ProposedVersionId,
            Changes = JsonSerializer.Serialize(proposal.Changes),
            proposal.Rationale,
            proposal.ExpectedImprovement,
            proposal.Status,
            proposal.CreatedAt,
            proposal.CreatedBy,
            proposal.ReflectionReportId,
            Metadata = JsonSerializer.Serialize(new Dictionary<string, object>(proposal.Metadata)
            {
                ["hypothesis_ids"] = proposal.HypothesisIds,
                ["semantic_similarity"] = proposal.SemanticSimilarity
            })
        });
    }

    public async Task<PromptProposal?> GetProposalAsync(
        string proposalId,
        CancellationToken cancellationToken = default)
    {
        using var connection = await GetConnectionAsync();

        const string sql = @"
            SELECT *
            FROM prompt_proposals
            WHERE proposal_id = @ProposalId";

        var result = await connection.QueryFirstOrDefaultAsync<dynamic>(sql, new { ProposalId = proposalId });

        if (result == null)
        {
            return null;
        }

        return MapToProposal(result);
    }

    public async Task<List<PromptProposal>> GetProposalsForPromptAsync(
        string promptId,
        string? status = null,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        using var connection = await GetConnectionAsync();

        var sql = "SELECT * FROM prompt_proposals WHERE prompt_id = @PromptId";
        var parameters = new DynamicParameters();
        parameters.Add("PromptId", promptId);

        if (!string.IsNullOrEmpty(status))
        {
            sql += " AND status = @Status";
            parameters.Add("Status", status);
        }

        sql += " ORDER BY created_at DESC LIMIT @Limit";
        parameters.Add("Limit", limit);

        var results = await connection.QueryAsync<dynamic>(sql, parameters);

        return results.Select(r => MapToProposal((dynamic)r)).ToList();
    }

    public async Task<List<PromptProposal>> GetPendingProposalsAsync(
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        using var connection = await GetConnectionAsync();

        const string sql = @"
            SELECT *
            FROM prompt_proposals
            WHERE status = 'pending'
            ORDER BY created_at DESC
            LIMIT @Limit";

        var results = await connection.QueryAsync<dynamic>(sql, new { Limit = limit });

        return results.Select(r => MapToProposal((dynamic)r)).ToList();
    }

    public async Task UpdateProposalStatusAsync(
        string proposalId,
        string newStatus,
        CancellationToken cancellationToken = default)
    {
        using var connection = await GetConnectionAsync();

        const string sql = @"
            UPDATE prompt_proposals
            SET status = @Status
            WHERE proposal_id = @ProposalId";

        await connection.ExecuteAsync(sql, new
        {
            ProposalId = proposalId,
            Status = newStatus
        });
    }

    public async Task<int> DeleteOldProposalsAsync(
        DateTime olderThan,
        List<string>? excludeStatuses = null,
        CancellationToken cancellationToken = default)
    {
        using var connection = await GetConnectionAsync();

        var sql = "DELETE FROM prompt_proposals WHERE created_at < @OlderThan";
        var parameters = new DynamicParameters();
        parameters.Add("OlderThan", olderThan);

        if (excludeStatuses != null && excludeStatuses.Any())
        {
            sql += " AND status NOT IN @ExcludeStatuses";
            parameters.Add("ExcludeStatuses", excludeStatuses);
        }

        return await connection.ExecuteAsync(sql, parameters);
    }

    // Helper method to map database record to PromptProposal
    private PromptProposal MapToProposal(dynamic record)
    {
        var proposal = new PromptProposal
        {
            ProposalId = record.proposal_id,
            PromptId = record.prompt_id,
            CurrentVersion = record.current_version,
            ProposedTemplate = record.proposed_template,
            ProposedVersionId = record.proposed_version,
            Rationale = record.rationale,
            ExpectedImprovement = record.expected_improvement,
            Status = record.status,
            CreatedAt = record.created_at,
            CreatedBy = record.created_by ?? "system:rewriter",
            ReflectionReportId = record.reflection_report_id
        };

        // Parse changes
        if (record.changes != null)
        {
            try
            {
                proposal.Changes = JsonSerializer.Deserialize<List<PromptChange>>(
                    record.changes.ToString()
                ) ?? new List<PromptChange>();
            }
            catch (JsonException)
            {
                proposal.Changes = new List<PromptChange>();
            }
        }

        // Parse metadata
        if (record.metadata != null)
        {
            try
            {
                var metadata = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                    record.metadata.ToString()
                ) ?? new Dictionary<string, JsonElement>();

                proposal.Metadata = metadata.ToDictionary(
                    kv => kv.Key,
                    kv => (object)kv.Value
                );

                // Extract hypothesis_ids
                if (metadata.ContainsKey("hypothesis_ids"))
                {
                    proposal.HypothesisIds = JsonSerializer.Deserialize<List<string>>(
                        metadata["hypothesis_ids"].GetRawText()
                    ) ?? new List<string>();
                }

                // Extract semantic_similarity
                if (metadata.ContainsKey("semantic_similarity"))
                {
                    proposal.SemanticSimilarity = metadata["semantic_similarity"].GetDouble();
                }
            }
            catch (JsonException)
            {
                proposal.Metadata = new Dictionary<string, object>();
            }
        }

        return proposal;
    }
}
