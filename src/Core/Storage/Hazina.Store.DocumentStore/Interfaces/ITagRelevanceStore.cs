using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Persistence layer for tag relevance indices.
/// Stores and retrieves tag scores for different query contexts.
/// </summary>
public interface ITagRelevanceStore
{
    /// <summary>
    /// Store a tag relevance index.
    /// </summary>
    Task StoreAsync(TagRelevanceIndex index, CancellationToken ct = default);

    /// <summary>
    /// Retrieve index by its ID.
    /// </summary>
    Task<TagRelevanceIndex?> GetByIdAsync(string indexId, CancellationToken ct = default);

    /// <summary>
    /// Retrieve index by query context checksum (for cache lookup).
    /// </summary>
    Task<TagRelevanceIndex?> GetByChecksumAsync(string checksum, CancellationToken ct = default);

    /// <summary>
    /// Get the most recent index (useful for default scoring).
    /// </summary>
    Task<TagRelevanceIndex?> GetLatestAsync(CancellationToken ct = default);

    /// <summary>
    /// List all stored indices.
    /// </summary>
    Task<List<TagRelevanceIndex>> ListAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Delete an index by ID.
    /// </summary>
    Task DeleteAsync(string indexId, CancellationToken ct = default);

    /// <summary>
    /// Delete indices older than the specified date.
    /// </summary>
    Task CleanupOlderThanAsync(DateTime cutoff, CancellationToken ct = default);
}
