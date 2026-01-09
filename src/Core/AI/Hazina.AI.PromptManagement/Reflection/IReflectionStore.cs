using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Hazina.AI.PromptManagement.Reflection;

/// <summary>
/// Storage interface for reflection reports
/// </summary>
public interface IReflectionStore
{
    /// <summary>
    /// Save a reflection report
    /// </summary>
    /// <param name="report">Reflection report to save</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SaveReportAsync(
        ReflectionReport report,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a reflection report by ID
    /// </summary>
    /// <param name="reportId">Report ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Reflection report or null if not found</returns>
    Task<ReflectionReport?> GetReportAsync(
        string reportId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get reflection report history for a prompt
    /// </summary>
    /// <param name="promptId">Prompt ID</param>
    /// <param name="limit">Maximum number of reports to retrieve</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of reflection reports ordered by creation date descending</returns>
    Task<List<ReflectionReport>> GetReportHistoryAsync(
        string promptId,
        int limit = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the most recent reflection report for a prompt
    /// </summary>
    /// <param name="promptId">Prompt ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Most recent reflection report or null</returns>
    Task<ReflectionReport?> GetLatestReportAsync(
        string promptId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete old reflection reports (cleanup)
    /// </summary>
    /// <param name="olderThan">Delete reports older than this date</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of reports deleted</returns>
    Task<int> DeleteOldReportsAsync(
        DateTime olderThan,
        CancellationToken cancellationToken = default);
}
