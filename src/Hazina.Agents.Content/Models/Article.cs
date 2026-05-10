namespace Hazina.Agents.Content.Models;

/// <summary>
/// Represents an article to be embedded and clustered.
/// </summary>
public class Article
{
    /// <summary>
    /// Unique identifier for the article.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Article title.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Full article content/body text.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Source or publication name.
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// Geographic region the article covers.
    /// </summary>
    public string Region { get; set; } = string.Empty;

    /// <summary>
    /// When the article was published.
    /// </summary>
    public DateTime PublishedAt { get; set; }

    /// <summary>
    /// Builds the text representation used for embedding generation.
    /// Combines title and content for richer semantic representation.
    /// </summary>
    public string ToEmbeddingText() => $"{Title}\n\n{Content}";
}
