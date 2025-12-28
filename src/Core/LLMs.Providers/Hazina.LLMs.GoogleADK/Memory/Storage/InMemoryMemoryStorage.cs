using System.Collections.Concurrent;
using Hazina.LLMs.GoogleADK.Memory.Models;
using Microsoft.Extensions.Logging;

namespace Hazina.LLMs.GoogleADK.Memory.Storage;

/// <summary>
/// In-memory storage for memory items (development/testing)
/// </summary>
public class InMemoryMemoryStorage : IMemoryStorage
{
    private readonly ConcurrentDictionary<string, MemoryItem> _memories = new();
    private readonly ILogger? _logger;

    public InMemoryMemoryStorage(ILogger? logger = null)
    {
        _logger = logger;
    }

    public Task SaveMemoryAsync(MemoryItem memory, CancellationToken cancellationToken = default)
    {
        _memories[memory.MemoryId] = memory;
        _logger?.LogDebug("Saved memory {MemoryId} of type {Type}", memory.MemoryId, memory.Type);
        return Task.CompletedTask;
    }

    public Task<MemoryItem?> GetMemoryAsync(string memoryId, CancellationToken cancellationToken = default)
    {
        if (_memories.TryGetValue(memoryId, out var memory))
        {
            memory.MarkAccessed();
            return Task.FromResult<MemoryItem?>(memory);
        }
        return Task.FromResult<MemoryItem?>(null);
    }

    public Task DeleteMemoryAsync(string memoryId, CancellationToken cancellationToken = default)
    {
        _memories.TryRemove(memoryId, out _);
        _logger?.LogDebug("Deleted memory {MemoryId}", memoryId);
        return Task.CompletedTask;
    }

    public Task<List<MemorySearchResult>> SearchMemoriesAsync(
        MemoryQuery query,
        CancellationToken cancellationToken = default)
    {
        var results = new List<MemorySearchResult>();
        var memories = _memories.Values.AsEnumerable();

        // Apply filters
        if (!string.IsNullOrEmpty(query.AgentName))
        {
            memories = memories.Where(m => m.AgentName == query.AgentName);
        }

        if (!string.IsNullOrEmpty(query.UserId))
        {
            memories = memories.Where(m => m.UserId == query.UserId);
        }

        if (!string.IsNullOrEmpty(query.SessionId))
        {
            memories = memories.Where(m => m.SessionId == query.SessionId);
        }

        if (query.Type.HasValue)
        {
            memories = memories.Where(m => m.Type == query.Type.Value);
        }

        if (query.MinImportance.HasValue)
        {
            memories = memories.Where(m => m.Importance >= query.MinImportance.Value);
        }

        if (query.StartDate.HasValue)
        {
            memories = memories.Where(m => m.CreatedAt >= query.StartDate.Value);
        }

        if (query.EndDate.HasValue)
        {
            memories = memories.Where(m => m.CreatedAt <= query.EndDate.Value);
        }

        if (query.Tags != null && query.Tags.Any())
        {
            memories = memories.Where(m => m.Tags.Any(t => query.Tags.Contains(t)));
        }

        // Text search (simple contains for now)
        if (!string.IsNullOrEmpty(query.QueryText))
        {
            var queryLower = query.QueryText.ToLowerInvariant();
            memories = memories.Where(m => m.Content.ToLowerInvariant().Contains(queryLower));
        }

        // Vector search if embeddings are available
        if (query.QueryEmbedding != null)
        {
            foreach (var memory in memories.Where(m => m.Embedding != null))
            {
                var similarity = CalculateCosineSimilarity(query.QueryEmbedding, memory.Embedding!);

                if (similarity >= query.MinSimilarity)
                {
                    var relevance = CalculateRelevance(memory, similarity);
                    results.Add(new MemorySearchResult
                    {
                        Memory = memory,
                        SimilarityScore = similarity,
                        RelevanceScore = relevance
                    });
                }
            }

            results = results.OrderByDescending(r => r.RelevanceScore).Take(query.Limit).ToList();
        }
        else
        {
            // No embeddings, sort by recency and strength
            results = memories
                .Select(m => new MemorySearchResult
                {
                    Memory = m,
                    SimilarityScore = 1.0,
                    RelevanceScore = m.CalculateStrength()
                })
                .OrderByDescending(r => r.RelevanceScore)
                .Take(query.Limit)
                .ToList();
        }

        _logger?.LogDebug("Found {Count} memories matching query", results.Count);
        return Task.FromResult(results);
    }

    public Task<List<MemoryItem>> ListMemoriesAsync(
        string? agentName = null,
        string? userId = null,
        MemoryType? type = null,
        CancellationToken cancellationToken = default)
    {
        var memories = _memories.Values.AsEnumerable();

        if (!string.IsNullOrEmpty(agentName))
        {
            memories = memories.Where(m => m.AgentName == agentName);
        }

        if (!string.IsNullOrEmpty(userId))
        {
            memories = memories.Where(m => m.UserId == userId);
        }

        if (type.HasValue)
        {
            memories = memories.Where(m => m.Type == type.Value);
        }

        return Task.FromResult(memories.ToList());
    }

    public Task<List<MemoryItem>> GetMemoriesByTagAsync(string tag, CancellationToken cancellationToken = default)
    {
        var memories = _memories.Values
            .Where(m => m.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
            .ToList();

        return Task.FromResult(memories);
    }

    public Task UpdateMemoryAccessAsync(string memoryId, CancellationToken cancellationToken = default)
    {
        if (_memories.TryGetValue(memoryId, out var memory))
        {
            memory.MarkAccessed();
        }
        return Task.CompletedTask;
    }

    public Task<int> GetMemoryCountAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_memories.Count);
    }

    public Task<int> ConsolidateMemoriesAsync(
        double strengthThreshold = 0.1,
        CancellationToken cancellationToken = default)
    {
        var weakMemories = _memories.Values
            .Where(m => m.CalculateStrength() < strengthThreshold)
            .Select(m => m.MemoryId)
            .ToList();

        foreach (var memoryId in weakMemories)
        {
            _memories.TryRemove(memoryId, out _);
        }

        _logger?.LogInformation("Consolidated {Count} weak memories", weakMemories.Count);
        return Task.FromResult(weakMemories.Count);
    }

    /// <summary>
    /// Calculate cosine similarity between two vectors
    /// </summary>
    private double CalculateCosineSimilarity(float[] vector1, float[] vector2)
    {
        if (vector1.Length != vector2.Length)
        {
            throw new ArgumentException("Vectors must have the same length");
        }

        double dotProduct = 0;
        double magnitude1 = 0;
        double magnitude2 = 0;

        for (int i = 0; i < vector1.Length; i++)
        {
            dotProduct += vector1[i] * vector2[i];
            magnitude1 += vector1[i] * vector1[i];
            magnitude2 += vector2[i] * vector2[i];
        }

        magnitude1 = Math.Sqrt(magnitude1);
        magnitude2 = Math.Sqrt(magnitude2);

        if (magnitude1 == 0 || magnitude2 == 0)
        {
            return 0;
        }

        return dotProduct / (magnitude1 * magnitude2);
    }

    /// <summary>
    /// Calculate overall relevance combining similarity and memory strength
    /// </summary>
    private double CalculateRelevance(MemoryItem memory, double similarity)
    {
        var strength = memory.CalculateStrength();
        return (similarity * 0.7) + (strength * 0.3);
    }

    /// <summary>
    /// Clear all memories (for testing)
    /// </summary>
    public void Clear()
    {
        _memories.Clear();
        _logger?.LogDebug("Cleared all memories");
    }
}
