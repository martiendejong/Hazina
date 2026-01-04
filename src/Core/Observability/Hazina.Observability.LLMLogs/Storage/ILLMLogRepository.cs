using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hazina.Observability.LLMLogs.Storage.Models;

namespace Hazina.Observability.LLMLogs.Storage
{
    /// <summary>
    /// Repository for storing and retrieving LLM call logs.
    /// </summary>
    public interface ILLMLogRepository
    {
        /// <summary>
        /// Initializes the database schema if needed.
        /// </summary>
        Task InitializeAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Logs an LLM call to the database.
        /// </summary>
        Task LogCallAsync(LLMCallLog log, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves logs matching the specified filters.
        /// </summary>
        Task<IReadOnlyList<LLMCallLog>> GetLogsAsync(
            DateTime? startDate = null,
            DateTime? endDate = null,
            string? username = null,
            string? feature = null,
            string? provider = null,
            int limit = 100,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the total cost for a specific time period and optional filters.
        /// </summary>
        Task<decimal> GetTotalCostAsync(
            DateTime? startDate = null,
            DateTime? endDate = null,
            string? username = null,
            string? feature = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the total token usage for a specific time period and optional filters.
        /// </summary>
        Task<(long inputTokens, long outputTokens, long totalTokens)> GetTokenUsageAsync(
            DateTime? startDate = null,
            DateTime? endDate = null,
            string? username = null,
            string? feature = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes logs older than the specified retention period.
        /// </summary>
        Task CleanupOldLogsAsync(int retentionDays, CancellationToken cancellationToken = default);
    }
}
