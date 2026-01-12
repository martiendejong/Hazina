using Hazina.AI.RAG.Core;
using Hazina.LongContext.Interfaces;
using Hazina.LongContext.Models;

namespace Hazina.LongContext.Providers;

/// <summary>
/// Bridges to Hazina.AI.RAG for shard retrieval.
/// Converts RAG search results to ContextShard objects.
/// </summary>
public class ContextEngineeringShardProvider : IContextShardProvider
{
    private readonly RAGEngine _ragEngine;

    public ContextEngineeringShardProvider(RAGEngine ragEngine)
    {
        _ragEngine = ragEngine ?? throw new ArgumentNullException(nameof(ragEngine));
    }

    public async Task<IReadOnlyList<ContextShard>> FindRelevantShardsAsync(
        string query,
        LongContextSessionId? sessionId = null,
        int maxShards = 20,
        CancellationToken ct = default)
    {
        // Search using RAG engine
        var searchResults = await _ragEngine.SearchAsync(
            query,
            topK: maxShards,
            minSimilarity: 0.0, // Let caller filter if needed
            cancellationToken: ct);

        // Convert to shards
        var shards = new List<ContextShard>();

        foreach (var result in searchResults)
        {
            var shard = new ContextShard
            {
                Id = ContextShardId.From(result.Id),
                Content = result.Content,
                Relevance = result.Similarity,
                Metadata = new Dictionary<string, object>(result.Metadata)
                {
                    ["originalSimilarity"] = result.Similarity
                }
            };

            shards.Add(shard);
        }

        return shards;
    }
}
