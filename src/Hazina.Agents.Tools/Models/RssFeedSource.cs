namespace Hazina.Agents.Tools.Models;

/// <summary>
/// Configuration for an RSS feed source
/// </summary>
public class RssFeedSource
{
    /// <summary>
    /// Publisher name
    /// </summary>
    public required string Publisher { get; set; }

    /// <summary>
    /// RSS feed URL
    /// </summary>
    public required string FeedUrl { get; set; }

    /// <summary>
    /// Geographic region
    /// </summary>
    public required string Region { get; set; }

    /// <summary>
    /// Language ISO code
    /// </summary>
    public required string Language { get; set; }

    /// <summary>
    /// Whether this feed is currently active
    /// </summary>
    public bool IsActive { get; set; } = true;
}
