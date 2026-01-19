namespace Hazina.Tools.Services.Social.Models;

/// <summary>
/// Unified content model that works for ALL sources:
/// WordPress posts/pages, LinkedIn posts, Facebook posts, Instagram posts, etc.
/// </summary>
public class UnifiedContent
{
    // ===== Identity =====
    /// <summary>
    /// Unique ID: {source}-{sourceId} e.g., "wordpress-123", "linkedin-789"
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    /// Project this content belongs to
    /// </summary>
    public string ProjectId { get; set; } = "";

    /// <summary>
    /// Connected account that imported this
    /// </summary>
    public string AccountId { get; set; } = "";

    // ===== Source Information =====
    /// <summary>
    /// Source platform: "wordpress", "linkedin", "facebook", "instagram", "twitter"
    /// </summary>
    public string SourceType { get; set; } = "";

    /// <summary>
    /// Original ID from source platform
    /// </summary>
    public string SourceId { get; set; } = "";

    /// <summary>
    /// Original URL on source platform
    /// </summary>
    public string? SourceUrl { get; set; }

    /// <summary>
    /// Content type: "post", "page", "product", "story", "reel", "video", "article"
    /// </summary>
    public string ContentType { get; set; } = "";

    // ===== Core Content =====
    /// <summary>
    /// Title (may be null for posts without titles, e.g., Instagram captions)
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Main content/body text
    /// </summary>
    public string Content { get; set; } = "";

    /// <summary>
    /// Summary/excerpt (auto-generated if not provided)
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>
    /// HTML version (for rich text platforms)
    /// </summary>
    public string? ContentHtml { get; set; }

    /// <summary>
    /// Plain text version (for search/analysis)
    /// </summary>
    public string? ContentPlainText { get; set; }

    // ===== Media =====
    /// <summary>
    /// Featured/main image URL
    /// </summary>
    public string? FeaturedImageUrl { get; set; }

    /// <summary>
    /// Additional media (images, videos)
    /// </summary>
    public List<ContentMedia> Media { get; set; } = new();

    // ===== Author =====
    /// <summary>
    /// Author display name
    /// </summary>
    public string? AuthorName { get; set; }

    /// <summary>
    /// Author ID on source platform
    /// </summary>
    public string? AuthorId { get; set; }

    // ===== Timestamps =====
    /// <summary>
    /// When content was originally published
    /// </summary>
    public DateTime PublishedAt { get; set; }

    /// <summary>
    /// When content was last modified (may be null)
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// When we imported this content
    /// </summary>
    public DateTime ImportedAt { get; set; }

    /// <summary>
    /// Last time we synced/refreshed this content
    /// </summary>
    public DateTime? LastSyncedAt { get; set; }

    // ===== Engagement Metrics =====
    /// <summary>
    /// Number of likes/reactions
    /// </summary>
    public int LikeCount { get; set; }

    /// <summary>
    /// Number of comments
    /// </summary>
    public int CommentCount { get; set; }

    /// <summary>
    /// Number of shares/retweets
    /// </summary>
    public int ShareCount { get; set; }

    /// <summary>
    /// Number of views (if available)
    /// </summary>
    public int ViewCount { get; set; }

    /// <summary>
    /// Calculated engagement rate
    /// </summary>
    public double EngagementRate { get; set; }

    // ===== Comments =====
    /// <summary>
    /// Comments on this content
    /// </summary>
    public List<ContentComment> Comments { get; set; } = new();

    // ===== Categorization =====
    /// <summary>
    /// Tags/hashtags
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Categories (WordPress categories, topics, etc.)
    /// </summary>
    public List<string> Categories { get; set; } = new();

    /// <summary>
    /// Language code (e.g., "en", "nl", "de")
    /// </summary>
    public string? Language { get; set; }

    // ===== AI Analysis (Filled after import) =====
    /// <summary>
    /// AI-detected topics
    /// </summary>
    public List<string> DetectedTopics { get; set; } = new();

    /// <summary>
    /// Sentiment: "positive", "neutral", "negative"
    /// </summary>
    public string? Sentiment { get; set; }

    /// <summary>
    /// Tone: "professional", "casual", "technical", "friendly"
    /// </summary>
    public string? Tone { get; set; }

    /// <summary>
    /// Full analysis result
    /// </summary>
    public ContentAnalysis? Analysis { get; set; }

    // ===== Status & Flags =====
    /// <summary>
    /// Status: "published", "draft", "archived", "deleted"
    /// </summary>
    public string Status { get; set; } = "published";

    /// <summary>
    /// Is this historical (imported past content) or future (scheduled)?
    /// </summary>
    public bool IsHistorical { get; set; } = true;

    /// <summary>
    /// Should this appear on calendar?
    /// </summary>
    public bool DisplayOnCalendar { get; set; } = true;

    /// <summary>
    /// Soft delete flag
    /// </summary>
    public bool IsDeleted { get; set; }

    // ===== Document Store Integration =====
    /// <summary>
    /// Document store ID for RAG/semantic search
    /// </summary>
    public string? DocumentStoreId { get; set; }

    /// <summary>
    /// Vector embedding ID
    /// </summary>
    public string? EmbeddingId { get; set; }

    // ===== Platform-Specific Metadata =====
    /// <summary>
    /// Platform-specific fields (JSON serialized)
    /// </summary>
    public Dictionary<string, object> PlatformMetadata { get; set; } = new();
}
