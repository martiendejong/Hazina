using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Hazina.AI.PromptManagement.Safety;

/// <summary>
/// Storage interface for sandbox test results
/// </summary>
public interface ISandboxTestStore
{
    /// <summary>
    /// Save sandbox test result
    /// </summary>
    /// <param name="result">Test result</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SaveTestResultAsync(
        SandboxTestResult result,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get sandbox test result by ID
    /// </summary>
    /// <param name="testId">Test ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Test result or null if not found</returns>
    Task<SandboxTestResult?> GetTestResultAsync(
        string testId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get sandbox test results for a proposal
    /// </summary>
    /// <param name="proposalId">Proposal ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of test results</returns>
    Task<List<SandboxTestResult>> GetTestResultsForProposalAsync(
        string proposalId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete old sandbox test results
    /// </summary>
    /// <param name="olderThan">Delete results older than this date</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of results deleted</returns>
    Task<int> DeleteOldTestResultsAsync(
        DateTime olderThan,
        CancellationToken cancellationToken = default);
}
