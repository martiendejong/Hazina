namespace Hazina.Tools.Services.Social.Abstractions;

/// <summary>
/// Storage interface for imported social content.
/// Uses SQLite for structured storage with embeddings support.
/// </summary>
public interface ISocialContentStore
{
    /// <summary>
    /// Saves imported posts.
    /// </summary>
    Task SavePostsAsync(
        string projectId,
        string accountId,
        IEnumerable<SocialPost> posts,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves imported articles.
    /// </summary>
    Task SaveArticlesAsync(
        string projectId,
        string accountId,
        IEnumerable<SocialArticle> articles,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets posts for an account with optional filtering.
    /// </summary>
    Task<List<SocialPost>> GetPostsAsync(
        string projectId,
        string? accountId = null,
        DateTime? since = null,
        int limit = 100,
        int offset = 0,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets articles for an account with optional filtering.
    /// </summary>
    Task<List<SocialArticle>> GetArticlesAsync(
        string projectId,
        string? accountId = null,
        DateTime? since = null,
        int limit = 100,
        int offset = 0,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches content by text.
    /// </summary>
    Task<List<SocialContentSearchResult>> SearchAsync(
        string projectId,
        string query,
        SearchOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all content for an account.
    /// </summary>
    Task DeleteAccountContentAsync(
        string projectId,
        string accountId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets content statistics for a project.
    /// </summary>
    Task<ContentStats> GetStatsAsync(
        string projectId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Options for content search.
/// </summary>
public class SearchOptions
{
    /// <summary>
    /// Types of content to search (posts, articles).
    /// </summary>
    public HashSet<string> ContentTypes { get; set; } = new() { "posts", "articles" };

    /// <summary>
    /// Account IDs to filter by.
    /// </summary>
    public List<string>? AccountIds { get; set; }

    /// <summary>
    /// Date range start.
    /// </summary>
    public DateTime? Since { get; set; }

    /// <summary>
    /// Date range end.
    /// </summary>
    public DateTime? Until { get; set; }

    /// <summary>
    /// Maximum results.
    /// </summary>
    public int Limit { get; set; } = 20;

    /// <summary>
    /// Use semantic search (embeddings).
    /// </summary>
    public bool SemanticSearch { get; set; } = true;
}

/// <summary>
/// Result of a content search.
/// </summary>
public class SocialContentSearchResult
{
    /// <summary>
    /// Content type (post, article).
    /// </summary>
    public string ContentType { get; set; } = "";

    /// <summary>
    /// Content ID.
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    /// Account that owns this content.
    /// </summary>
    public string AccountId { get; set; } = "";

    /// <summary>
    /// Content text (title for articles, content for posts).
    /// </summary>
    public string Text { get; set; } = "";

    /// <summary>
    /// Content snippet with match highlights.
    /// </summary>
    public string Snippet { get; set; } = "";

    /// <summary>
    /// When the content was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// URL to the original content.
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// Relevance score (0-1).
    /// </summary>
    public float Score { get; set; }
}

/// <summary>
/// Content statistics for a project.
/// </summary>
public class ContentStats
{
    /// <summary>
    /// Total number of posts.
    /// </summary>
    public int TotalPosts { get; set; }

    /// <summary>
    /// Total number of articles.
    /// </summary>
    public int TotalArticles { get; set; }

    /// <summary>
    /// Total number of comments.
    /// </summary>
    public int TotalComments { get; set; }

    /// <summary>
    /// Posts per account.
    /// </summary>
    public Dictionary<string, int> PostsByAccount { get; set; } = new();

    /// <summary>
    /// Date range of content.
    /// </summary>
    public DateTime? OldestContent { get; set; }
    public DateTime? NewestContent { get; set; }
}
