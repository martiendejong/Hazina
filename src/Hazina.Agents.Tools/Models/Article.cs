using System;

namespace Hazina.Agents.Tools.Models;

/// <summary>
/// Represents a news article collected from RSS feeds
/// </summary>
public class Article
{
    /// <summary>
    /// Unique identifier for the article
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Article title
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Full article content (may be summary from RSS)
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Original article URL
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Publisher/source name (e.g., "Reuters", "BBC", "Al Jazeera")
    /// </summary>
    public string Publisher { get; set; } = string.Empty;

    /// <summary>
    /// Geographic region (e.g., "Western", "Russian", "Chinese", "Middle East", "Africa", "Latin America", "Southeast Asia")
    /// </summary>
    public string Region { get; set; } = string.Empty;

    /// <summary>
    /// Article language (ISO 639-1 code, e.g., "en", "ru", "zh", "ar", "es")
    /// </summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>
    /// Article publication timestamp
    /// </summary>
    public DateTime PublishTime { get; set; }

    /// <summary>
    /// Collection timestamp
    /// </summary>
    public DateTime CollectedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Optional: Author name(s)
    /// </summary>
    public string? Author { get; set; }

    /// <summary>
    /// Optional: Article categories/tags
    /// </summary>
    public List<string> Categories { get; set; } = new();

    /// <summary>
    /// Hash of URL for duplicate detection
    /// </summary>
    public string UrlHash { get; set; } = string.Empty;
}
