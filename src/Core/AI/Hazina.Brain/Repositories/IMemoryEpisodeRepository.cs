using Hazina.Brain.Domain;

namespace Hazina.Brain.Repositories;

/// <summary>
/// Repository for persistent storage and retrieval of episodic memories.
/// </summary>
public interface IMemoryEpisodeRepository
{
    /// <summary>
    /// Store a new episode
    /// </summary>
    Task StoreAsync(MemoryEpisode episode, CancellationToken ct = default);

    /// <summary>
    /// Query episodes by similarity to a query embedding
    /// </summary>
    /// <param name="storeId">Store filter</param>
    /// <param name="agentId">Optional agent filter</param>
    /// <param name="userId">Optional user filter</param>
    /// <param name="queryEmbedding">Query embedding vector</param>
    /// <param name="topK">Number of results to return</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Top-K most similar episodes, ordered by similarity (highest first)</returns>
    Task<IReadOnlyList<MemoryEpisode>> QueryAsync(
        string storeId,
        string? agentId,
        string? userId,
        float[] queryEmbedding,
        int topK,
        CancellationToken ct = default);

    /// <summary>
    /// Update last accessed timestamps for retrieved episodes
    /// </summary>
    Task UpdateAccessAsync(IEnumerable<Guid> ids, CancellationToken ct = default);

    /// <summary>
    /// Get count of episodes for a store
    /// </summary>
    Task<int> GetCountAsync(string storeId, CancellationToken ct = default);

    /// <summary>
    /// Prune lowest-weighted episodes when count exceeds max
    /// </summary>
    /// <param name="storeId">Store to prune</param>
    /// <param name="maxEpisodes">Maximum allowed episodes</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Number of episodes deleted</returns>
    Task<int> PruneAsync(string storeId, int maxEpisodes, CancellationToken ct = default);
}
