using Hazina.Brain.Domain;

namespace Hazina.Brain.Repositories;

/// <summary>
/// Repository for persistent storage and retrieval of distilled memory facts.
/// </summary>
public interface IMemoryFactRepository
{
    /// <summary>
    /// Add facts (bulk insert)
    /// </summary>
    Task AddFactsAsync(IEnumerable<MemoryFact> facts, CancellationToken ct = default);

    /// <summary>
    /// Query facts by similarity to a query embedding
    /// </summary>
    /// <param name="storeId">Store filter</param>
    /// <param name="agentId">Optional agent filter</param>
    /// <param name="userId">Optional user filter</param>
    /// <param name="queryEmbedding">Query embedding vector</param>
    /// <param name="topK">Number of results to return</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Top-K most similar facts, ordered by weight * similarity (highest first)</returns>
    Task<IReadOnlyList<MemoryFact>> QueryAsync(
        string storeId,
        string? agentId,
        string? userId,
        float[] queryEmbedding,
        int topK,
        CancellationToken ct = default);

    /// <summary>
    /// Update weights for retrieved facts (usage boost)
    /// </summary>
    Task UpdateWeightsAsync(IReadOnlyDictionary<Guid, double> weightDeltas, CancellationToken ct = default);

    /// <summary>
    /// Apply decay to all facts (exponential decay based on last accessed time)
    /// </summary>
    Task ApplyDecayAsync(TimeSpan halfLife, CancellationToken ct = default);

    /// <summary>
    /// Get count of facts for a store
    /// </summary>
    Task<int> GetCountAsync(string storeId, CancellationToken ct = default);

    /// <summary>
    /// Prune lowest-weighted facts when count exceeds max
    /// </summary>
    Task<int> PruneAsync(string storeId, int maxFacts, CancellationToken ct = default);

    /// <summary>
    /// Check if a fact with similar text already exists (deduplication)
    /// </summary>
    Task<bool> ExistsSimilarAsync(string storeId, string text, double similarityThreshold = 0.95, CancellationToken ct = default);
}
