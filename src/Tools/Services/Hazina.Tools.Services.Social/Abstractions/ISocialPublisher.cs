namespace Hazina.Tools.Services.Social.Abstractions;

/// <summary>
/// Interface for publishing content to social media platforms.
/// Separate from ISocialProvider (which handles import) to keep concerns separated.
/// </summary>
public interface ISocialPublisher
{
    /// <summary>
    /// Provider identifier (e.g., "linkedin", "facebook", "twitter").
    /// Must match the corresponding ISocialProvider.ProviderId.
    /// </summary>
    string ProviderId { get; }

    /// <summary>
    /// Human-readable provider name.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Publishes a text post to the platform.
    /// </summary>
    /// <param name="accessToken">OAuth access token for the user's account</param>
    /// <param name="request">Post content and options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing post URL and metadata</returns>
    Task<PublishResult> PublishPostAsync(
        string accessToken,
        PublishPostRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a post from the platform.
    /// </summary>
    /// <param name="accessToken">OAuth access token</param>
    /// <param name="externalPostId">Platform-specific post ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success status</returns>
    Task<bool> DeletePostAsync(
        string accessToken,
        string externalPostId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches engagement metrics for a published post.
    /// </summary>
    /// <param name="accessToken">OAuth access token</param>
    /// <param name="externalPostId">Platform-specific post ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Engagement metrics</returns>
    Task<PostMetrics> GetPostMetricsAsync(
        string accessToken,
        string externalPostId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the access token is valid and has publishing permissions.
    /// </summary>
    Task<bool> ValidateAccessAsync(
        string accessToken,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Request to publish a post to a social platform.
/// </summary>
public class PublishPostRequest
{
    /// <summary>
    /// The main content/caption of the post.
    /// </summary>
    public string Content { get; set; } = "";

    /// <summary>
    /// Optional media URLs (images, videos) to attach.
    /// </summary>
    public List<string> MediaUrls { get; set; } = new();

    /// <summary>
    /// Optional hashtags (platform will format appropriately).
    /// </summary>
    public List<string> Hashtags { get; set; } = new();

    /// <summary>
    /// Optional mentions/tags (platform-specific format).
    /// </summary>
    public List<string> Mentions { get; set; } = new();

    /// <summary>
    /// Platform-specific options (e.g., LinkedIn visibility, Twitter reply settings).
    /// </summary>
    public Dictionary<string, string> Options { get; set; } = new();

    /// <summary>
    /// Internal post ID from our database (for logging/tracking).
    /// </summary>
    public string? InternalPostId { get; set; }

    /// <summary>
    /// Raw image bytes for media upload (if available).
    /// </summary>
    public byte[]? ImageData { get; set; }

    /// <summary>
    /// Filename for the image (e.g., "image.jpg").
    /// </summary>
    public string? ImageFileName { get; set; }

    /// <summary>
    /// MIME type of the image (e.g., "image/jpeg").
    /// </summary>
    public string? ImageContentType { get; set; }
}

/// <summary>
/// Result of a post publishing operation.
/// </summary>
public class PublishResult
{
    /// <summary>
    /// Whether the publish operation succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Platform-specific post ID (e.g., LinkedIn URN, Twitter tweet ID).
    /// </summary>
    public string? ExternalPostId { get; set; }

    /// <summary>
    /// Public URL to view the published post.
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// Error message if publishing failed.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Error code for programmatic handling (e.g., "rate_limit", "invalid_token").
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// Additional metadata from the platform.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>
    /// When the post was published (from platform response).
    /// </summary>
    public DateTime? PublishedAt { get; set; }
}

/// <summary>
/// Engagement metrics for a published post.
/// </summary>
public class PostMetrics
{
    /// <summary>
    /// Platform-specific post ID.
    /// </summary>
    public string ExternalPostId { get; set; } = "";

    /// <summary>
    /// Number of likes/reactions.
    /// </summary>
    public int Likes { get; set; }

    /// <summary>
    /// Number of comments/replies.
    /// </summary>
    public int Comments { get; set; }

    /// <summary>
    /// Number of shares/retweets/reposts.
    /// </summary>
    public int Shares { get; set; }

    /// <summary>
    /// Number of views/impressions (if available).
    /// </summary>
    public int Views { get; set; }

    /// <summary>
    /// When these metrics were fetched.
    /// </summary>
    public DateTime FetchedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Platform-specific metrics not covered by standard fields.
    /// </summary>
    public Dictionary<string, int> AdditionalMetrics { get; set; } = new();

    /// <summary>
    /// Additional metadata from the platform (non-numeric data).
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();
}
