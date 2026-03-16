namespace WebSearch.Core;

/// <summary>
/// Options for configuring a search request
/// </summary>
public class SearchOptions
{
    /// <summary>
    /// Maximum number of results to return (default: 10)
    /// </summary>
    public int MaxResults { get; init; } = 10;

    /// <summary>
    /// Language code (e.g., "en", "nl", "de")
    /// </summary>
    public string? Language { get; init; }

    /// <summary>
    /// Region/country code (e.g., "US", "NL", "DE")
    /// </summary>
    public string? Region { get; init; }

    /// <summary>
    /// Enable safe search filtering
    /// </summary>
    public bool SafeSearch { get; init; } = true;

    /// <summary>
    /// Time range for results (e.g., "d" = past day, "w" = past week)
    /// </summary>
    public string? TimeRange { get; init; }

    /// <summary>
    /// Custom parameters for provider-specific features
    /// </summary>
    public Dictionary<string, string>? CustomParameters { get; init; }

    /// <summary>
    /// Page number for paginated results (default: 0)
    /// </summary>
    public int Page { get; init; } = 0;

    /// <summary>
    /// User agent string to use (if not specified, library will rotate agents)
    /// </summary>
    public string? UserAgent { get; init; }
}
