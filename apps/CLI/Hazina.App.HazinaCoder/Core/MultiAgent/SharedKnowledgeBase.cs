using System;
using System.Collections.Generic;
using System.Linq;

namespace Hazina.App.HazinaCoder.Core.MultiAgent;

/// <summary>
/// Shared knowledge base across all agents.
/// Uses Qdrant vector database for distributed learning.
/// </summary>
/// <remarks>
/// Improvement #39: Shared Knowledge Base
///
/// Features:
/// - All agents contribute learnings
/// - All agents benefit from collective experience
/// - Vector similarity search for retrieval
/// - Automatic knowledge consolidation
/// </remarks>
public class SharedKnowledgeBase
{
    private readonly string _qdrantEndpoint;
    private readonly string _collectionName;
    private readonly List<KnowledgeEntry> _localCache = new();

    public SharedKnowledgeBase(string qdrantEndpoint = "http://localhost:6333", string collectionName = "agent_knowledge")
    {
        _qdrantEndpoint = qdrantEndpoint;
        _collectionName = collectionName;
    }

    /// <summary>
    /// Store a knowledge entry
    /// </summary>
    public void Store(string agentId, string category, string content, Dictionary<string, string>? metadata = null)
    {
        var entry = new KnowledgeEntry
        {
            Id = Guid.NewGuid(),
            AgentId = agentId,
            Category = category,
            Content = content,
            Metadata = metadata ?? new(),
            Timestamp = DateTime.UtcNow
        };

        _localCache.Add(entry);

        // TODO: Generate embedding and store in Qdrant
        Console.WriteLine($"[Knowledge] Stored: {category} from {agentId}");
    }

    /// <summary>
    /// Search knowledge base
    /// </summary>
    public IEnumerable<KnowledgeEntry> Search(string query, int maxResults = 10)
    {
        // TODO: Generate query embedding and search Qdrant
        // For now, simple keyword search in local cache
        var keywords = query.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return _localCache
            .Where(e => keywords.Any(k =>
                e.Category.Contains(k, StringComparison.OrdinalIgnoreCase) ||
                e.Content.Contains(k, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(e => e.Timestamp)
            .Take(maxResults);
    }

    /// <summary>
    /// Get knowledge contributed by specific agent
    /// </summary>
    public IEnumerable<KnowledgeEntry> GetByAgent(string agentId)
    {
        return _localCache
            .Where(e => e.AgentId == agentId)
            .OrderByDescending(e => e.Timestamp);
    }

    /// <summary>
    /// Get knowledge by category
    /// </summary>
    public IEnumerable<KnowledgeEntry> GetByCategory(string category)
    {
        return _localCache
            .Where(e => e.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(e => e.Timestamp);
    }

    /// <summary>
    /// Consolidate similar knowledge entries
    /// </summary>
    public void Consolidate()
    {
        // TODO: Use LLM to merge similar entries
        // TODO: Remove duplicates
        // TODO: Update confidence scores
        Console.WriteLine("[Knowledge] Consolidation complete (placeholder)");
    }

    /// <summary>
    /// Get statistics
    /// </summary>
    public KnowledgeStats GetStats()
    {
        return new KnowledgeStats
        {
            TotalEntries = _localCache.Count,
            UniqueAgents = _localCache.Select(e => e.AgentId).Distinct().Count(),
            Categories = _localCache.Select(e => e.Category).Distinct().Count(),
            OldestEntry = _localCache.Count > 0 ? _localCache.Min(e => e.Timestamp) : DateTime.MinValue,
            NewestEntry = _localCache.Count > 0 ? _localCache.Max(e => e.Timestamp) : DateTime.MinValue
        };
    }
}

/// <summary>
/// Single knowledge entry
/// </summary>
public class KnowledgeEntry
{
    public Guid Id { get; set; }
    public string AgentId { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = new();
    public DateTime Timestamp { get; set; }
    public double Confidence { get; set; } = 1.0;
}

public class KnowledgeStats
{
    public int TotalEntries { get; set; }
    public int UniqueAgents { get; set; }
    public int Categories { get; set; }
    public DateTime OldestEntry { get; set; }
    public DateTime NewestEntry { get; set; }
}
