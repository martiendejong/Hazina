using Hazina.LongContext.Models;

namespace Hazina.LongContext.Interfaces;

/// <summary>
/// Provides relevant context shards for a given query.
/// Bridges to existing Hazina retrieval layers (RAG, ContextEngineering).
/// </summary>
public interface IContextShardProvider
{
    /// <summary>
    /// Find relevant context shards for a query
    /// </summary>
    /// <param name="query">The search query</param>
    /// <param name="sessionId">Optional session ID for context</param>
    /// <param name="maxShards">Maximum number of shards to return</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of relevant shards ordered by relevance</returns>
    Task<IReadOnlyList<ContextShard>> FindRelevantShardsAsync(
        string query,
        LongContextSessionId? sessionId = null,
        int maxShards = 20,
        CancellationToken ct = default);
}
