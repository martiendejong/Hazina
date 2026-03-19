namespace WebSearch.Core;

/// <summary>
/// Represents a single search result from a provider
/// </summary>
public class SearchResult
{
    /// <summary>
    /// Title of the search result
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// URL of the search result
    /// </summary>
    public required string Url { get; init; }

    /// <summary>
    /// Snippet/description of the search result
    /// </summary>
    public string? Snippet { get; init; }

    /// <summary>
    /// Provider that generated this result
    /// </summary>
    public required string Source { get; init; }

    /// <summary>
    /// Position/rank in search results (1-based)
    /// </summary>
    public int Rank { get; init; }

    /// <summary>
    /// Additional metadata specific to the provider
    /// </summary>
    public Dictionary<string, object>? Metadata { get; init; }

    /// <summary>
    /// Timestamp when this result was fetched
    /// </summary>
    public DateTime FetchedAt { get; init; } = DateTime.UtcNow;
}
