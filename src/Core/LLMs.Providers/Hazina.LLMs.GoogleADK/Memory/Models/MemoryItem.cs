namespace Hazina.LLMs.GoogleADK.Memory.Models;

/// <summary>
/// Represents a memory item stored in the memory bank
/// </summary>
public class MemoryItem
{
    /// <summary>
    /// Unique memory identifier
    /// </summary>
    public string MemoryId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Type of memory
    /// </summary>
    public MemoryType Type { get; set; }

    /// <summary>
    /// Memory content
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Embedding vector for semantic search (optional)
    /// </summary>
    public float[]? Embedding { get; set; }

    /// <summary>
    /// When the memory was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last time the memory was accessed
    /// </summary>
    public DateTime LastAccessedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Number of times this memory has been accessed
    /// </summary>
    public int AccessCount { get; set; } = 0;

    /// <summary>
    /// Importance score (0.0 to 1.0)
    /// </summary>
    public double Importance { get; set; } = 0.5;

    /// <summary>
    /// Session ID this memory originated from
    /// </summary>
    public string? SessionId { get; set; }

    /// <summary>
    /// Agent name that created this memory
    /// </summary>
    public string? AgentName { get; set; }

    /// <summary>
    /// User ID associated with this memory
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Tags for categorization
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Additional metadata
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Related memory IDs
    /// </summary>
    public List<string> RelatedMemories { get; set; } = new();

    /// <summary>
    /// Calculate memory strength based on recency and access
    /// </summary>
    public double CalculateStrength()
    {
        var recencyScore = CalculateRecencyScore();
        var frequencyScore = Math.Min(AccessCount / 10.0, 1.0); // Cap at 10 accesses

        return (recencyScore * 0.6) + (frequencyScore * 0.3) + (Importance * 0.1);
    }

    /// <summary>
    /// Calculate recency score (exponential decay)
    /// </summary>
    private double CalculateRecencyScore()
    {
        var hoursSinceAccess = (DateTime.UtcNow - LastAccessedAt).TotalHours;
        return Math.Exp(-hoursSinceAccess / 168.0); // Half-life of 1 week
    }

    /// <summary>
    /// Mark memory as accessed
    /// </summary>
    public void MarkAccessed()
    {
        LastAccessedAt = DateTime.UtcNow;
        AccessCount++;
    }
}

/// <summary>
/// Types of memory
/// </summary>
public enum MemoryType
{
    /// <summary>
    /// Episodic memory - specific events or experiences
    /// </summary>
    Episodic,

    /// <summary>
    /// Semantic memory - facts and general knowledge
    /// </summary>
    Semantic,

    /// <summary>
    /// Procedural memory - how to do things
    /// </summary>
    Procedural,

    /// <summary>
    /// Working memory - temporary, short-term
    /// </summary>
    Working
}

/// <summary>
/// Query for searching memories
/// </summary>
public class MemoryQuery
{
    /// <summary>
    /// Text query for semantic search
    /// </summary>
    public string? QueryText { get; set; }

    /// <summary>
    /// Query embedding vector
    /// </summary>
    public float[]? QueryEmbedding { get; set; }

    /// <summary>
    /// Filter by memory type
    /// </summary>
    public MemoryType? Type { get; set; }

    /// <summary>
    /// Filter by agent name
    /// </summary>
    public string? AgentName { get; set; }

    /// <summary>
    /// Filter by user ID
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Filter by session ID
    /// </summary>
    public string? SessionId { get; set; }

    /// <summary>
    /// Filter by tags (any match)
    /// </summary>
    public List<string>? Tags { get; set; }

    /// <summary>
    /// Minimum importance score
    /// </summary>
    public double? MinImportance { get; set; }

    /// <summary>
    /// Maximum number of results
    /// </summary>
    public int Limit { get; set; } = 10;

    /// <summary>
    /// Minimum similarity threshold (0.0 to 1.0)
    /// </summary>
    public double MinSimilarity { get; set; } = 0.0;

    /// <summary>
    /// Date range start
    /// </summary>
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// Date range end
    /// </summary>
    public DateTime? EndDate { get; set; }
}

/// <summary>
/// Result from memory search
/// </summary>
public class MemorySearchResult
{
    public MemoryItem Memory { get; set; } = new();
    public double SimilarityScore { get; set; }
    public double RelevanceScore { get; set; }
}
