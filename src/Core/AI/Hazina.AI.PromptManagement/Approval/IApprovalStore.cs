using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Hazina.AI.PromptManagement.Approval;

/// <summary>
/// Storage interface for approval requests
/// </summary>
public interface IApprovalStore
{
    /// <summary>
    /// Save approval request
    /// </summary>
    /// <param name="request">Approval request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SaveRequestAsync(
        ApprovalRequest request,
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
    /// Get approval request by proposal ID
    /// </summary>
    /// <param name="proposalId">Proposal ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Approval request or null if not found</returns>
    Task<ApprovalRequest?> GetRequestByProposalAsync(
        string proposalId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get pending approval requests
    /// </summary>
    /// <param name="approverId">Optional filter by approver ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of pending requests</returns>
    Task<List<ApprovalRequest>> GetPendingRequestsAsync(
        string? approverId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get approval requests for a prompt
    /// </summary>
    /// <param name="promptId">Prompt ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of approval requests</returns>
    Task<List<ApprovalRequest>> GetRequestsForPromptAsync(
        string promptId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete old approval requests
    /// </summary>
    /// <param name="olderThan">Delete requests older than this date</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of requests deleted</returns>
    Task<int> DeleteOldRequestsAsync(
        DateTime olderThan,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get approval configuration
    /// </summary>
    /// <param name="promptId">Prompt ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Approval configuration or null if not found</returns>
    Task<ApprovalConfig?> GetConfigAsync(
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

    /// <summary>
    /// Delete approval configuration
    /// </summary>
    /// <param name="promptId">Prompt ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteConfigAsync(
        string promptId,
        CancellationToken cancellationToken = default);
}
