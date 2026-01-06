using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hazina.AI.PromptManagement.Core.Models;

namespace Hazina.AI.PromptManagement.Core;

/// <summary>
/// Interface for prompt template storage and versioning
/// </summary>
public interface IPromptStore
{
    // ========================================================================
    // Template Management
    // ========================================================================

    /// <summary>
    /// Get a prompt template by ID, optionally specifying a version
    /// </summary>
    /// <param name="promptId">Prompt ID</param>
    /// <param name="version">Optional version ID (defaults to current version)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Prompt template with the specified version</returns>
    Task<PromptTemplate?> GetAsync(
        string promptId,
        string? version = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all prompt templates
    /// </summary>
    /// <param name="category">Optional category filter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of all prompt templates</returns>
    Task<List<PromptTemplate>> GetAllAsync(
        string? category = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a new prompt template
    /// </summary>
    /// <param name="request">Template creation request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Version ID of the created template</returns>
    Task<string> CreateAsync(
        PromptTemplateRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Update an existing prompt template (creates a new version)
    /// </summary>
    /// <param name="request">Template update request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Version ID of the new version</returns>
    Task<string> UpdateAsync(
        PromptTemplateRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a prompt template and all its versions
    /// </summary>
    /// <param name="promptId">Prompt ID to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteAsync(
        string promptId,
        CancellationToken cancellationToken = default);

    // ========================================================================
    // Version Management
    // ========================================================================

    /// <summary>
    /// Get version history for a prompt
    /// </summary>
    /// <param name="promptId">Prompt ID</param>
    /// <param name="limit">Maximum number of versions to return</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of versions, newest first</returns>
    Task<List<PromptVersion>> GetVersionHistoryAsync(
        string promptId,
        int limit = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a specific version by version ID
    /// </summary>
    /// <param name="versionId">Version ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Prompt version</returns>
    Task<PromptVersion?> GetVersionAsync(
        string versionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rollback a prompt to a previous version
    /// </summary>
    /// <param name="request">Rollback request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Version ID after rollback</returns>
    Task<string> RollbackAsync(
        RollbackRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get rollback history for a prompt
    /// </summary>
    /// <param name="promptId">Prompt ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of rollback history entries</returns>
    Task<List<RollbackHistory>> GetRollbackHistoryAsync(
        string promptId,
        CancellationToken cancellationToken = default);

    // ========================================================================
    // Metrics
    // ========================================================================

    /// <summary>
    /// Get performance metrics for a prompt version
    /// </summary>
    /// <param name="promptId">Prompt ID</param>
    /// <param name="versionId">Version ID (optional, defaults to current)</param>
    /// <param name="startDate">Start date for metrics aggregation</param>
    /// <param name="endDate">End date for metrics aggregation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Aggregated metrics</returns>
    Task<PromptMetrics?> GetMetricsAsync(
        string promptId,
        string? versionId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Record usage of a prompt (for metrics tracking)
    /// </summary>
    /// <param name="promptId">Prompt ID</param>
    /// <param name="versionId">Version ID</param>
    /// <param name="success">Whether the execution was successful</param>
    /// <param name="userRating">Optional user rating (0.0 to 1.0)</param>
    /// <param name="confidence">Optional confidence score (0.0 to 1.0)</param>
    /// <param name="latencyMs">Optional latency in milliseconds</param>
    /// <param name="tokensUsed">Optional number of tokens used</param>
    /// <param name="costUsd">Optional cost in USD</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task RecordUsageAsync(
        string promptId,
        string versionId,
        bool success,
        double? userRating = null,
        double? confidence = null,
        double? latencyMs = null,
        long? tokensUsed = null,
        decimal? costUsd = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Update evaluation metrics for a version
    /// </summary>
    /// <param name="promptId">Prompt ID</param>
    /// <param name="versionId">Version ID</param>
    /// <param name="evalMetrics">Evaluation metrics (MRR, NDCG, etc.)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task UpdateEvalMetricsAsync(
        string promptId,
        string versionId,
        Dictionary<string, double> evalMetrics,
        CancellationToken cancellationToken = default);

    // ========================================================================
    // Template Rendering
    // ========================================================================

    /// <summary>
    /// Render a template with given variables
    /// </summary>
    /// <param name="promptId">Prompt ID</param>
    /// <param name="variables">Variables to substitute</param>
    /// <param name="version">Optional version ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Rendered template string</returns>
    Task<string> RenderAsync(
        string promptId,
        Dictionary<string, object> variables,
        string? version = null,
        CancellationToken cancellationToken = default);
}
