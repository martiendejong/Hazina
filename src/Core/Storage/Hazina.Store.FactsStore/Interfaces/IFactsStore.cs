using Hazina.Store.FactsStore.Models;

namespace Hazina.Store.FactsStore.Interfaces;

/// <summary>
/// Storage interface for compact, relevant facts used in context engineering.
/// Supports both semantic search (via embeddings) and metadata-based filtering.
/// </summary>
public interface IFactsStore
{
    /// <summary>
    /// Add a single fact to the store
    /// </summary>
    Task<string> AddAsync(Fact fact, CancellationToken cancellationToken = default);

    /// <summary>
    /// Add multiple facts in batch
    /// </summary>
    Task<List<string>> AddBatchAsync(List<Fact> facts, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update an existing fact
    /// </summary>
    Task<bool> UpdateAsync(Fact fact, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a fact by ID
    /// </summary>
    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a fact by ID
    /// </summary>
    Task<Fact?> GetByIdAsync(string id, bool includeEmbedding = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Query facts with flexible filtering and ranking
    /// Supports semantic search (if embeddings provided) and metadata filtering
    /// </summary>
    Task<List<Fact>> QueryAsync(FactQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all facts matching specific tags
    /// </summary>
    Task<List<Fact>> GetByTagsAsync(List<string> tags, int maxResults = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all facts of a specific type
    /// </summary>
    Task<List<Fact>> GetByTypeAsync(string type, int maxResults = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// Count total facts in store
    /// </summary>
    Task<long> CountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Clear all facts from the store (use with caution!)
    /// </summary>
    Task ClearAsync(CancellationToken cancellationToken = default);
}
