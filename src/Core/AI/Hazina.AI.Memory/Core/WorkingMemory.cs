namespace Hazina.AI.Memory.Core;

/// <summary>
/// Token-bounded working memory for active agent context.
/// Automatically manages capacity and provides FIFO/priority-based eviction.
/// </summary>
public class WorkingMemory
{
    private readonly int _maxTokens;
    private readonly List<MemoryItem> _items = new();
    private int _currentTokenCount = 0;

    public int MaxTokens => _maxTokens;
    public int CurrentTokenCount => _currentTokenCount;
    public int AvailableTokens => _maxTokens - _currentTokenCount;
    public IReadOnlyList<MemoryItem> Items => _items.AsReadOnly();

    public WorkingMemory(int maxTokens = 8000)
    {
        if (maxTokens <= 0)
            throw new ArgumentException("Max tokens must be positive", nameof(maxTokens));

        _maxTokens = maxTokens;
    }

    /// <summary>
    /// Add item to working memory, evicting oldest if necessary
    /// </summary>
    public void Add(MemoryItem item)
    {
        while (_currentTokenCount + item.TokenCount > _maxTokens && _items.Count > 0)
        {
            EvictOldest();
        }

        if (item.TokenCount > _maxTokens)
        {
            throw new InvalidOperationException(
                $"Item token count ({item.TokenCount}) exceeds working memory capacity ({_maxTokens})");
        }

        _items.Add(item);
        _currentTokenCount += item.TokenCount;
    }

    /// <summary>
    /// Add text content with estimated token count
    /// </summary>
    public void AddText(string content, MemoryItemType type, int? priority = null)
    {
        var tokenCount = EstimateTokenCount(content);
        var item = new MemoryItem
        {
            ItemId = Guid.NewGuid().ToString(),
            Content = content,
            Type = type,
            TokenCount = tokenCount,
            Timestamp = DateTime.UtcNow,
            Priority = priority
        };

        Add(item);
    }

    /// <summary>
    /// Get all items of a specific type
    /// </summary>
    public List<MemoryItem> GetByType(MemoryItemType type)
    {
        return _items.Where(i => i.Type == type).ToList();
    }

    /// <summary>
    /// Get most recent N items
    /// </summary>
    public List<MemoryItem> GetRecent(int count)
    {
        return _items.OrderByDescending(i => i.Timestamp).Take(count).ToList();
    }

    /// <summary>
    /// Clear all items
    /// </summary>
    public void Clear()
    {
        _items.Clear();
        _currentTokenCount = 0;
    }

    /// <summary>
    /// Remove specific item
    /// </summary>
    public bool Remove(string itemId)
    {
        var item = _items.FirstOrDefault(i => i.ItemId == itemId);
        if (item != null)
        {
            _items.Remove(item);
            _currentTokenCount -= item.TokenCount;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Consolidate working memory into a single text representation
    /// </summary>
    public string ConsolidateToText()
    {
        return string.Join("\n\n", _items.Select(i => i.Content));
    }

    /// <summary>
    /// Evict oldest item
    /// </summary>
    private void EvictOldest()
    {
        if (_items.Count == 0)
            return;

        var oldest = _items.OrderBy(i => i.Priority ?? 0).ThenBy(i => i.Timestamp).First();
        _items.Remove(oldest);
        _currentTokenCount -= oldest.TokenCount;
    }

    /// <summary>
    /// Estimate token count for text (rough approximation: ~4 chars per token)
    /// </summary>
    private static int EstimateTokenCount(string text)
    {
        return text.Length / 4;
    }
}

/// <summary>
/// Item stored in working memory
/// </summary>
public class MemoryItem
{
    public required string ItemId { get; init; }
    public required string Content { get; init; }
    public required MemoryItemType Type { get; init; }
    public required int TokenCount { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public int? Priority { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }
}

/// <summary>
/// Type of memory item
/// </summary>
public enum MemoryItemType
{
    Instruction,
    Observation,
    ToolResult,
    Decision,
    Context
}
