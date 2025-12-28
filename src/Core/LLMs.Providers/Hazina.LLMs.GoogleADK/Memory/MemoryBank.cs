using Hazina.LLMs.GoogleADK.Memory.Models;
using Hazina.LLMs.GoogleADK.Memory.Storage;
using Microsoft.Extensions.Logging;

namespace Hazina.LLMs.GoogleADK.Memory;

/// <summary>
/// Memory bank for managing long-term agent memories
/// </summary>
public class MemoryBank : IAsyncDisposable
{
    private readonly IMemoryStorage _storage;
    private readonly ILogger? _logger;
    private readonly Timer? _consolidationTimer;
    private bool _disposed;

    public MemoryBank(IMemoryStorage storage, ILogger? logger = null)
    {
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _logger = logger;

        // Start consolidation timer (runs every hour)
        _consolidationTimer = new Timer(
            async _ => await ConsolidateMemoriesAsync(),
            null,
            TimeSpan.FromHours(1),
            TimeSpan.FromHours(1)
        );

        _logger?.LogInformation("MemoryBank initialized");
    }

    /// <summary>
    /// Store a new memory
    /// </summary>
    public async Task<MemoryItem> StoreMemoryAsync(
        string content,
        MemoryType type,
        string? agentName = null,
        string? userId = null,
        string? sessionId = null,
        double importance = 0.5,
        float[]? embedding = null,
        Dictionary<string, object>? metadata = null,
        List<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        var memory = new MemoryItem
        {
            Content = content,
            Type = type,
            AgentName = agentName,
            UserId = userId,
            SessionId = sessionId,
            Importance = importance,
            Embedding = embedding,
            Metadata = metadata ?? new Dictionary<string, object>(),
            Tags = tags ?? new List<string>()
        };

        await _storage.SaveMemoryAsync(memory, cancellationToken);

        _logger?.LogInformation(
            "Stored {Type} memory {MemoryId} for agent {AgentName}",
            type, memory.MemoryId, agentName);

        return memory;
    }

    /// <summary>
    /// Retrieve a specific memory
    /// </summary>
    public async Task<MemoryItem?> RetrieveMemoryAsync(
        string memoryId,
        CancellationToken cancellationToken = default)
    {
        var memory = await _storage.GetMemoryAsync(memoryId, cancellationToken);

        if (memory != null)
        {
            await _storage.UpdateMemoryAccessAsync(memoryId, cancellationToken);
            _logger?.LogDebug("Retrieved memory {MemoryId}", memoryId);
        }

        return memory;
    }

    /// <summary>
    /// Search memories with query
    /// </summary>
    public async Task<List<MemorySearchResult>> SearchAsync(
        MemoryQuery query,
        CancellationToken cancellationToken = default)
    {
        var results = await _storage.SearchMemoriesAsync(query, cancellationToken);

        // Update access for retrieved memories
        foreach (var result in results)
        {
            await _storage.UpdateMemoryAccessAsync(result.Memory.MemoryId, cancellationToken);
        }

        _logger?.LogDebug("Search returned {Count} memories", results.Count);
        return results;
    }

    /// <summary>
    /// Search memories by text
    /// </summary>
    public async Task<List<MemorySearchResult>> SearchByTextAsync(
        string queryText,
        MemoryType? type = null,
        string? agentName = null,
        string? userId = null,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var query = new MemoryQuery
        {
            QueryText = queryText,
            Type = type,
            AgentName = agentName,
            UserId = userId,
            Limit = limit
        };

        return await SearchAsync(query, cancellationToken);
    }

    /// <summary>
    /// Search memories by embedding (semantic search)
    /// </summary>
    public async Task<List<MemorySearchResult>> SearchByEmbeddingAsync(
        float[] embedding,
        MemoryType? type = null,
        string? agentName = null,
        string? userId = null,
        int limit = 10,
        double minSimilarity = 0.7,
        CancellationToken cancellationToken = default)
    {
        var query = new MemoryQuery
        {
            QueryEmbedding = embedding,
            Type = type,
            AgentName = agentName,
            UserId = userId,
            Limit = limit,
            MinSimilarity = minSimilarity
        };

        return await SearchAsync(query, cancellationToken);
    }

    /// <summary>
    /// Get recent memories
    /// </summary>
    public async Task<List<MemoryItem>> GetRecentMemoriesAsync(
        string? agentName = null,
        string? userId = null,
        MemoryType? type = null,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var memories = await _storage.ListMemoriesAsync(agentName, userId, type, cancellationToken);

        return memories
            .OrderByDescending(m => m.CreatedAt)
            .Take(limit)
            .ToList();
    }

    /// <summary>
    /// Get important memories
    /// </summary>
    public async Task<List<MemoryItem>> GetImportantMemoriesAsync(
        double minImportance = 0.8,
        string? agentName = null,
        string? userId = null,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var query = new MemoryQuery
        {
            AgentName = agentName,
            UserId = userId,
            MinImportance = minImportance,
            Limit = limit
        };

        var results = await SearchAsync(query, cancellationToken);
        return results.Select(r => r.Memory).ToList();
    }

    /// <summary>
    /// Update memory importance
    /// </summary>
    public async Task UpdateImportanceAsync(
        string memoryId,
        double importance,
        CancellationToken cancellationToken = default)
    {
        var memory = await _storage.GetMemoryAsync(memoryId, cancellationToken);
        if (memory != null)
        {
            memory.Importance = Math.Clamp(importance, 0.0, 1.0);
            await _storage.SaveMemoryAsync(memory, cancellationToken);
            _logger?.LogDebug("Updated importance for memory {MemoryId} to {Importance}", memoryId, importance);
        }
    }

    /// <summary>
    /// Add tags to a memory
    /// </summary>
    public async Task AddTagsAsync(
        string memoryId,
        params string[] tags)
    {
        var memory = await _storage.GetMemoryAsync(memoryId);
        if (memory != null)
        {
            foreach (var tag in tags)
            {
                if (!memory.Tags.Contains(tag))
                {
                    memory.Tags.Add(tag);
                }
            }
            await _storage.SaveMemoryAsync(memory);
        }
    }

    /// <summary>
    /// Link related memories
    /// </summary>
    public async Task LinkMemoriesAsync(
        string memoryId,
        params string[] relatedMemoryIds)
    {
        var memory = await _storage.GetMemoryAsync(memoryId);
        if (memory != null)
        {
            foreach (var relatedId in relatedMemoryIds)
            {
                if (!memory.RelatedMemories.Contains(relatedId))
                {
                    memory.RelatedMemories.Add(relatedId);
                }
            }
            await _storage.SaveMemoryAsync(memory);
        }
    }

    /// <summary>
    /// Delete a memory
    /// </summary>
    public async Task DeleteMemoryAsync(
        string memoryId,
        CancellationToken cancellationToken = default)
    {
        await _storage.DeleteMemoryAsync(memoryId, cancellationToken);
        _logger?.LogInformation("Deleted memory {MemoryId}", memoryId);
    }

    /// <summary>
    /// Consolidate memories (remove weak ones)
    /// </summary>
    public async Task<int> ConsolidateMemoriesAsync(
        double strengthThreshold = 0.1,
        CancellationToken cancellationToken = default)
    {
        var removed = await _storage.ConsolidateMemoriesAsync(strengthThreshold, cancellationToken);
        _logger?.LogInformation("Consolidated {Count} memories", removed);
        return removed;
    }

    /// <summary>
    /// Get memory statistics
    /// </summary>
    public async Task<MemoryStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var allMemories = await _storage.ListMemoriesAsync(cancellationToken: cancellationToken);

        return new MemoryStatistics
        {
            TotalMemories = allMemories.Count,
            MemoriesByType = allMemories.GroupBy(m => m.Type).ToDictionary(g => g.Key, g => g.Count()),
            AverageImportance = allMemories.Any() ? allMemories.Average(m => m.Importance) : 0,
            AverageStrength = allMemories.Any() ? allMemories.Average(m => m.CalculateStrength()) : 0,
            OldestMemory = allMemories.OrderBy(m => m.CreatedAt).FirstOrDefault()?.CreatedAt,
            NewestMemory = allMemories.OrderByDescending(m => m.CreatedAt).FirstOrDefault()?.CreatedAt
        };
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        if (_consolidationTimer != null)
        {
            await _consolidationTimer.DisposeAsync();
        }

        _logger?.LogInformation("MemoryBank disposed");
    }
}

/// <summary>
/// Memory statistics
/// </summary>
public class MemoryStatistics
{
    public int TotalMemories { get; set; }
    public Dictionary<MemoryType, int> MemoriesByType { get; set; } = new();
    public double AverageImportance { get; set; }
    public double AverageStrength { get; set; }
    public DateTime? OldestMemory { get; set; }
    public DateTime? NewestMemory { get; set; }
}
