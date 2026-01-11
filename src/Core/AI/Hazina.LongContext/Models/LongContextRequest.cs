namespace Hazina.LongContext.Models;

/// <summary>
/// Request for a long-context query
/// </summary>
public class LongContextRequest
{
    /// <summary>
    /// The user's query
    /// </summary>
    public string Query { get; init; } = string.Empty;

    /// <summary>
    /// Optional session ID for multi-turn conversations
    /// </summary>
    public LongContextSessionId? SessionId { get; init; }

    /// <summary>
    /// Maximum recursion depth (default: 3)
    /// </summary>
    public int MaxDepth { get; init; } = 3;

    /// <summary>
    /// Maximum total tokens to use across all recursive calls (default: 100,000)
    /// </summary>
    public int MaxTotalTokens { get; init; } = 100_000;

    /// <summary>
    /// Maximum number of sub-questions per decomposition node (default: 5)
    /// </summary>
    public int MaxBranchingFactor { get; init; } = 5;

    /// <summary>
    /// Optional: target a specific store/domain
    /// </summary>
    public string? StoreId { get; init; }

    /// <summary>
    /// Optional: tags to filter relevant content
    /// </summary>
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Optional: metadata filters
    /// </summary>
    public Dictionary<string, string> MetadataFilters { get; init; } = new();

    /// <summary>
    /// Whether to use recursive strategy (false = single-shot for backwards compatibility)
    /// </summary>
    public bool EnableRecursion { get; init; } = false;
}
