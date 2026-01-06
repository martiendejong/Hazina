using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Hazina.AI.PromptManagement.Rewriter;

/// <summary>
/// Storage interface for prompt proposals
/// </summary>
public interface IProposalStore
{
    /// <summary>
    /// Save a prompt proposal
    /// </summary>
    /// <param name="proposal">Proposal to save</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SaveProposalAsync(
        PromptProposal proposal,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a proposal by ID
    /// </summary>
    /// <param name="proposalId">Proposal ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Prompt proposal or null if not found</returns>
    Task<PromptProposal?> GetProposalAsync(
        string proposalId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all proposals for a prompt
    /// </summary>
    /// <param name="promptId">Prompt ID</param>
    /// <param name="status">Filter by status (optional)</param>
    /// <param name="limit">Maximum number of proposals to return</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of proposals</returns>
    Task<List<PromptProposal>> GetProposalsForPromptAsync(
        string promptId,
        string? status = null,
        int limit = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get pending proposals (awaiting approval)
    /// </summary>
    /// <param name="limit">Maximum number to return</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of pending proposals</returns>
    Task<List<PromptProposal>> GetPendingProposalsAsync(
        int limit = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Update proposal status
    /// </summary>
    /// <param name="proposalId">Proposal ID</param>
    /// <param name="newStatus">New status</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task UpdateProposalStatusAsync(
        string proposalId,
        string newStatus,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete old proposals
    /// </summary>
    /// <param name="olderThan">Delete proposals older than this date</param>
    /// <param name="excludeStatuses">Don't delete proposals with these statuses</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of proposals deleted</returns>
    Task<int> DeleteOldProposalsAsync(
        DateTime olderThan,
        List<string>? excludeStatuses = null,
        CancellationToken cancellationToken = default);
}
