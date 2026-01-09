using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hazina.AI.PromptManagement.Rewriter;

namespace Hazina.AI.PromptManagement.Approval;

/// <summary>
/// Approval workflow for prompt change proposals
/// </summary>
public interface IApprovalWorkflow
{
    /// <summary>
    /// Submit a proposal for approval
    /// </summary>
    /// <param name="proposal">Prompt proposal</param>
    /// <param name="submittedBy">User submitting the proposal</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Approval request</returns>
    Task<ApprovalRequest> SubmitProposalAsync(
        PromptProposal proposal,
        string submittedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get approval request by ID
    /// </summary>
    /// <param name="requestId">Request ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Approval request or null if not found</returns>
    Task<ApprovalRequest?> GetRequestAsync(
        string requestId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get pending approval requests for a user
    /// </summary>
    /// <param name="approverId">Approver user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of pending requests</returns>
    Task<List<ApprovalRequest>> GetPendingRequestsAsync(
        string approverId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Approve a proposal
    /// </summary>
    /// <param name="requestId">Request ID</param>
    /// <param name="approvedBy">User approving</param>
    /// <param name="comments">Optional approval comments</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated approval request</returns>
    Task<ApprovalRequest> ApproveAsync(
        string requestId,
        string approvedBy,
        string? comments = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reject a proposal
    /// </summary>
    /// <param name="requestId">Request ID</param>
    /// <param name="rejectedBy">User rejecting</param>
    /// <param name="reason">Rejection reason</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated approval request</returns>
    Task<ApprovalRequest> RejectAsync(
        string requestId,
        string rejectedBy,
        string reason,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancel a pending approval request
    /// </summary>
    /// <param name="requestId">Request ID</param>
    /// <param name="cancelledBy">User cancelling</param>
    /// <param name="reason">Cancellation reason</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated approval request</returns>
    Task<ApprovalRequest> CancelAsync(
        string requestId,
        string cancelledBy,
        string reason,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if a proposal can be auto-approved
    /// </summary>
    /// <param name="proposal">Prompt proposal</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Auto-approval decision</returns>
    Task<AutoApprovalDecision> CheckAutoApprovalAsync(
        PromptProposal proposal,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get approval configuration for a prompt
    /// </summary>
    /// <param name="promptId">Prompt ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Approval configuration</returns>
    Task<ApprovalConfig> GetConfigAsync(
        string promptId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Save approval configuration
    /// </summary>
    /// <param name="config">Approval configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SaveConfigAsync(
        ApprovalConfig config,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Approval request for a prompt change proposal
/// </summary>
public class ApprovalRequest
{
    public string RequestId { get; set; } = string.Empty;
    public string ProposalId { get; set; } = string.Empty;
    public string PromptId { get; set; } = string.Empty;
    public ApprovalStatus Status { get; set; }
    public RiskLevel RiskLevel { get; set; }
    public string SubmittedBy { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; }
    public string? AssignedTo { get; set; }
    public string? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? Comments { get; set; }
    public string? RejectionReason { get; set; }
    public bool AutoApproved { get; set; }
    public string? AutoApprovalReason { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Approval status
/// </summary>
public enum ApprovalStatus
{
    Pending,
    Approved,
    Rejected,
    Cancelled,
    Expired
}

/// <summary>
/// Risk level for approval decisions
/// </summary>
public enum RiskLevel
{
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// Auto-approval decision
/// </summary>
public class AutoApprovalDecision
{
    public bool CanAutoApprove { get; set; }
    public string Reason { get; set; } = string.Empty;
    public RiskLevel RiskLevel { get; set; }
    public List<string> RiskFactors { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Approval configuration
/// </summary>
public class ApprovalConfig
{
    public string PromptId { get; set; } = string.Empty;
    public bool RequireApproval { get; set; } = true;
    public bool AllowAutoApproval { get; set; } = true;
    public double AutoApprovalMaxSemanticChange { get; set; } = 0.10; // Max 10% semantic change
    public double AutoApprovalMinPerformance { get; set; } = 1.05; // Min 5% improvement
    public List<string> ApproverUserIds { get; set; } = new();
    public List<string> ApproverRoles { get; set; } = new();
    public int RequiredApprovals { get; set; } = 1;
    public TimeSpan ApprovalTimeout { get; set; } = TimeSpan.FromDays(7);
    public bool NotifyOnSubmission { get; set; } = true;
    public List<string> NotificationChannels { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
}
