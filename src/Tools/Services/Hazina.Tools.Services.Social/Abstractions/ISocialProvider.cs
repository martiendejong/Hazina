namespace Hazina.Tools.Services.Social.Abstractions;

/// <summary>
/// Base interface for social media providers.
/// Each provider implements content import and account management.
/// </summary>
public interface ISocialProvider
{
    /// <summary>
    /// Provider identifier (e.g., "linkedin", "facebook", "twitter").
    /// </summary>
    string ProviderId { get; }

    /// <summary>
    /// Human-readable provider name.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// OAuth authorization URL for connecting accounts.
    /// </summary>
    string GetAuthorizationUrl(string redirectUri, string state);

    /// <summary>
    /// Exchanges an authorization code for access tokens.
    /// </summary>
    Task<SocialAuthResult> ExchangeCodeAsync(
        string code,
        string redirectUri,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Refreshes an access token using a refresh token.
    /// </summary>
    Task<SocialAuthResult> RefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the connected user's profile.
    /// </summary>
    Task<SocialProfile> GetProfileAsync(
        string accessToken,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Imports content from the connected account.
    /// </summary>
    Task<SocialImportResult> ImportContentAsync(
        string accessToken,
        SocialImportOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes access for a connected account.
    /// </summary>
    Task<bool> RevokeAccessAsync(
        string accessToken,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of OAuth authentication.
/// </summary>
public class SocialAuthResult
{
    public bool Success { get; set; }
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? Error { get; set; }
    public string? UserId { get; set; }
}

/// <summary>
/// Social media user profile.
/// </summary>
public class SocialProfile
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Email { get; set; }
    public string? ProfileUrl { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Headline { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>
/// Options for content import.
/// </summary>
public class SocialImportOptions
{
    /// <summary>
    /// Maximum number of items to import.
    /// </summary>
    public int MaxItems { get; set; } = 100;

    /// <summary>
    /// Import items from after this date.
    /// </summary>
    public DateTime? Since { get; set; }

    /// <summary>
    /// Types of content to import.
    /// </summary>
    public HashSet<string> ContentTypes { get; set; } = new() { "posts", "articles" };

    /// <summary>
    /// Whether to include comments/reactions.
    /// </summary>
    public bool IncludeEngagement { get; set; } = true;
}

/// <summary>
/// Result of a content import operation.
/// </summary>
public class SocialImportResult
{
    public bool Success { get; set; }
    public List<SocialPost> Posts { get; set; } = new();
    public List<SocialArticle> Articles { get; set; } = new();
    public int TotalImported { get; set; }
    public string? Error { get; set; }
    public string? ContinuationToken { get; set; }
}

/// <summary>
/// Represents a social media post.
/// </summary>
public class SocialPost
{
    public string Id { get; set; } = "";
    public string AccountId { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public string? Url { get; set; }
    public List<string> MediaUrls { get; set; } = new();
    public int LikeCount { get; set; }
    public int CommentCount { get; set; }
    public int ShareCount { get; set; }
    public List<SocialComment> Comments { get; set; } = new();
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>
/// Represents a social media article (LinkedIn articles, Medium posts, etc.)
/// </summary>
public class SocialArticle
{
    public string Id { get; set; } = "";
    public string AccountId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public string? Summary { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? Url { get; set; }
    public string? CoverImageUrl { get; set; }
    public List<string> Tags { get; set; } = new();
    public int ViewCount { get; set; }
    public int LikeCount { get; set; }
    public int CommentCount { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>
/// Represents a comment on social content.
/// </summary>
public class SocialComment
{
    public string Id { get; set; } = "";
    public string AuthorName { get; set; } = "";
    public string? AuthorId { get; set; }
    public string Content { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public int LikeCount { get; set; }
}
