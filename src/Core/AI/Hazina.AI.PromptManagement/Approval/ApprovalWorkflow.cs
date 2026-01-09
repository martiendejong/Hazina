using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hazina.AI.PromptManagement.Rewriter;
using Hazina.AI.PromptManagement.Safety;

namespace Hazina.AI.PromptManagement.Approval;

/// <summary>
/// Approval workflow implementation
/// </summary>
public class ApprovalWorkflow : IApprovalWorkflow
{
    private readonly IApprovalStore _store;
    private readonly IPromptRewriter _rewriter;
    private readonly ISafetyCoordinator _safetyCoordinator;

    public ApprovalWorkflow(
        IApprovalStore store,
        IPromptRewriter rewriter,
        ISafetyCoordinator safetyCoordinator)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _rewriter = rewriter ?? throw new ArgumentNullException(nameof(rewriter));
        _safetyCoordinator = safetyCoordinator ?? throw new ArgumentNullException(nameof(safetyCoordinator));
    }

    public async Task<ApprovalRequest> SubmitProposalAsync(
        PromptProposal proposal,
        string submittedBy,
        CancellationToken cancellationToken = default)
    {
        if (proposal == null)
            throw new ArgumentNullException(nameof(proposal));
        if (string.IsNullOrWhiteSpace(submittedBy))
            throw new ArgumentException("Submitted by user is required", nameof(submittedBy));

        // Get approval configuration
        var config = await GetConfigAsync(proposal.PromptId, cancellationToken);

        // Check if approval is required
        if (!config.RequireApproval)
        {
            // Create auto-approved request
            var autoRequest = new ApprovalRequest
            {
                RequestId = Guid.NewGuid().ToString(),
                ProposalId = proposal.ProposalId,
                PromptId = proposal.PromptId,
                Status = ApprovalStatus.Approved,
                RiskLevel = RiskLevel.Low,
                SubmittedBy = submittedBy,
                SubmittedAt = DateTime.UtcNow,
                ReviewedBy = "system",
                ReviewedAt = DateTime.UtcNow,
                AutoApproved = true,
                AutoApprovalReason = "Approval not required for this prompt"
            };

            await _store.SaveRequestAsync(autoRequest, cancellationToken);
            return autoRequest;
        }

        // Check auto-approval eligibility
        var autoApprovalDecision = await CheckAutoApprovalAsync(proposal, cancellationToken);

        var request = new ApprovalRequest
        {
            RequestId = Guid.NewGuid().ToString(),
            ProposalId = proposal.ProposalId,
            PromptId = proposal.PromptId,
            RiskLevel = autoApprovalDecision.RiskLevel,
            SubmittedBy = submittedBy,
            SubmittedAt = DateTime.UtcNow,
            Metadata = new Dictionary<string, object>
            {
                ["risk_factors"] = autoApprovalDecision.RiskFactors,
                ["semantic_similarity"] = proposal.SemanticSimilarity,
                ["rationale"] = proposal.Rationale
            }
        };

        if (autoApprovalDecision.CanAutoApprove && config.AllowAutoApproval)
        {
            request.Status = ApprovalStatus.Approved;
            request.AutoApproved = true;
            request.AutoApprovalReason = autoApprovalDecision.Reason;
            request.ReviewedBy = "system";
            request.ReviewedAt = DateTime.UtcNow;
        }
        else
        {
            request.Status = ApprovalStatus.Pending;

            // Assign to first available approver
            if (config.ApproverUserIds.Count > 0)
            {
                request.AssignedTo = config.ApproverUserIds[0];
            }

            request.Metadata["requires_human_approval"] = true;
            request.Metadata["auto_approval_blocked_reason"] = autoApprovalDecision.Reason;
        }

        await _store.SaveRequestAsync(request, cancellationToken);

        // TODO: Send notification if configured
        // if (config.NotifyOnSubmission && request.Status == ApprovalStatus.Pending)
        // {
        //     await SendNotificationAsync(request, config, cancellationToken);
        // }

        return request;
    }

    public async Task<ApprovalRequest?> GetRequestAsync(
        string requestId,
        CancellationToken cancellationToken = default)
    {
        return await _store.GetRequestAsync(requestId, cancellationToken);
    }

    public async Task<List<ApprovalRequest>> GetPendingRequestsAsync(
        string approverId,
        CancellationToken cancellationToken = default)
    {
        return await _store.GetPendingRequestsAsync(approverId, cancellationToken);
    }

    public async Task<ApprovalRequest> ApproveAsync(
        string requestId,
        string approvedBy,
        string? comments = null,
        CancellationToken cancellationToken = default)
    {
        var request = await _store.GetRequestAsync(requestId, cancellationToken);
        if (request == null)
            throw new InvalidOperationException($"Approval request {requestId} not found");

        if (request.Status != ApprovalStatus.Pending)
            throw new InvalidOperationException($"Cannot approve request in status {request.Status}");

        request.Status = ApprovalStatus.Approved;
        request.ReviewedBy = approvedBy;
        request.ReviewedAt = DateTime.UtcNow;
        request.Comments = comments;

        await _store.SaveRequestAsync(request, cancellationToken);

        return request;
    }

    public async Task<ApprovalRequest> RejectAsync(
        string requestId,
        string rejectedBy,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var request = await _store.GetRequestAsync(requestId, cancellationToken);
        if (request == null)
            throw new InvalidOperationException($"Approval request {requestId} not found");

        if (request.Status != ApprovalStatus.Pending)
            throw new InvalidOperationException($"Cannot reject request in status {request.Status}");

        request.Status = ApprovalStatus.Rejected;
        request.ReviewedBy = rejectedBy;
        request.ReviewedAt = DateTime.UtcNow;
        request.RejectionReason = reason;

        await _store.SaveRequestAsync(request, cancellationToken);

        return request;
    }

    public async Task<ApprovalRequest> CancelAsync(
        string requestId,
        string cancelledBy,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var request = await _store.GetRequestAsync(requestId, cancellationToken);
        if (request == null)
            throw new InvalidOperationException($"Approval request {requestId} not found");

        if (request.Status != ApprovalStatus.Pending)
            throw new InvalidOperationException($"Cannot cancel request in status {request.Status}");

        request.Status = ApprovalStatus.Cancelled;
        request.ReviewedBy = cancelledBy;
        request.ReviewedAt = DateTime.UtcNow;
        request.RejectionReason = reason;

        await _store.SaveRequestAsync(request, cancellationToken);

        return request;
    }

    public async Task<AutoApprovalDecision> CheckAutoApprovalAsync(
        PromptProposal proposal,
        CancellationToken cancellationToken = default)
    {
        var config = await GetConfigAsync(proposal.PromptId, cancellationToken);
        var riskFactors = new List<string>();
        var riskLevel = RiskLevel.Low;

        // Check semantic change threshold
        var semanticChange = 1.0 - proposal.SemanticSimilarity;
        if (semanticChange > config.AutoApprovalMaxSemanticChange)
        {
            riskFactors.Add($"Semantic change {semanticChange:P1} exceeds threshold {config.AutoApprovalMaxSemanticChange:P1}");
            riskLevel = RiskLevel.Medium;
        }

        // Check performance improvement
        var hasPerformanceData = proposal.Metadata.ContainsKey("performance_improvement");
        if (hasPerformanceData)
        {
            var perfImprovement = Convert.ToDouble(proposal.Metadata["performance_improvement"]);
            if (perfImprovement < config.AutoApprovalMinPerformance)
            {
                riskFactors.Add($"Performance improvement {perfImprovement:P1} below threshold {config.AutoApprovalMinPerformance:P1}");
                riskLevel = RiskLevel.Medium;
            }

            // Negative performance is high risk
            if (perfImprovement < 1.0)
            {
                riskFactors.Add($"Performance regression detected: {perfImprovement:P1}");
                riskLevel = RiskLevel.High;
            }
        }

        // Check if safety validation passed
        var hasSafetyValidation = proposal.Metadata.ContainsKey("safety_validation_passed");
        if (hasSafetyValidation)
        {
            var safetyPassed = Convert.ToBoolean(proposal.Metadata["safety_validation_passed"]);
            if (!safetyPassed)
            {
                riskFactors.Add("Safety validation failed");
                riskLevel = RiskLevel.High;
            }
        }
        else
        {
            riskFactors.Add("No safety validation performed");
            riskLevel = RiskLevel.Medium;
        }

        // Check template length change
        var lengthChange = Math.Abs(proposal.ProposedTemplate.Length - proposal.OriginalTemplate.Length);
        var lengthChangePercent = (double)lengthChange / proposal.OriginalTemplate.Length;
        if (lengthChangePercent > 0.30) // More than 30% length change
        {
            riskFactors.Add($"Template length changed by {lengthChangePercent:P1}");
            if (riskLevel < RiskLevel.Medium)
                riskLevel = RiskLevel.Medium;
        }

        // Determine if can auto-approve
        var canAutoApprove = riskFactors.Count == 0 && riskLevel == RiskLevel.Low;

        var decision = new AutoApprovalDecision
        {
            CanAutoApprove = canAutoApprove,
            RiskLevel = riskLevel,
            RiskFactors = riskFactors,
            Reason = canAutoApprove
                ? "Low risk change with good performance and semantic similarity"
                : $"Cannot auto-approve: {string.Join("; ", riskFactors)}",
            Metadata = new Dictionary<string, object>
            {
                ["semantic_change"] = semanticChange,
                ["semantic_similarity"] = proposal.SemanticSimilarity,
                ["length_change_percent"] = lengthChangePercent
            }
        };

        return decision;
    }

    public async Task<ApprovalConfig> GetConfigAsync(
        string promptId,
        CancellationToken cancellationToken = default)
    {
        var config = await _store.GetConfigAsync(promptId, cancellationToken);

        // Return default config if not found
        if (config == null)
        {
            config = new ApprovalConfig
            {
                PromptId = promptId,
                RequireApproval = true,
                AllowAutoApproval = true,
                AutoApprovalMaxSemanticChange = 0.10,
                AutoApprovalMinPerformance = 1.05,
                RequiredApprovals = 1,
                ApprovalTimeout = TimeSpan.FromDays(7),
                NotifyOnSubmission = true
            };
        }

        return config;
    }

    public async Task SaveConfigAsync(
        ApprovalConfig config,
        CancellationToken cancellationToken = default)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));

        await _store.SaveConfigAsync(config, cancellationToken);
    }
}
