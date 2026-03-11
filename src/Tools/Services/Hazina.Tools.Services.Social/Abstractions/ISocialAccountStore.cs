namespace Hazina.Tools.Services.Social.Abstractions;

/// <summary>
/// Storage interface for connected social accounts.
/// Manages OAuth tokens and account metadata.
/// </summary>
public interface ISocialAccountStore
{
    /// <summary>
    /// Saves a connected account.
    /// </summary>
    Task SaveAccountAsync(
        string projectId,
        ConnectedAccount account,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all connected accounts for a project.
    /// </summary>
    Task<List<ConnectedAccount>> GetAccountsAsync(
        string projectId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific connected account.
    /// </summary>
    Task<ConnectedAccount?> GetAccountAsync(
        string projectId,
        string accountId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a connected account and optionally its data.
    /// </summary>
    Task<bool> RemoveAccountAsync(
        string projectId,
        string accountId,
        bool deleteData = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates tokens for an account.
    /// </summary>
    Task UpdateTokensAsync(
        string projectId,
        string accountId,
        string accessToken,
        string? refreshToken,
        DateTime? expiresAt,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a connected social media account.
/// </summary>
public class ConnectedAccount
{
    /// <summary>
    /// Unique identifier for this connected account.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Project this account belongs to.
    /// </summary>
    public string ProjectId { get; set; } = "";

    /// <summary>
    /// Provider identifier (linkedin, facebook, etc.).
    /// </summary>
    public string ProviderId { get; set; } = "";

    /// <summary>
    /// User ID from the provider.
    /// </summary>
    public string ProviderUserId { get; set; } = "";

    /// <summary>
    /// Display name of the connected account.
    /// </summary>
    public string DisplayName { get; set; } = "";

    /// <summary>
    /// Profile URL on the platform.
    /// </summary>
    public string? ProfileUrl { get; set; }

    /// <summary>
    /// Avatar/profile image URL.
    /// </summary>
    public string? AvatarUrl { get; set; }

    /// <summary>
    /// OAuth access token (encrypted at rest).
    /// </summary>
    public string AccessToken { get; set; } = "";

    /// <summary>
    /// OAuth refresh token if available (encrypted at rest).
    /// </summary>
    public string? RefreshToken { get; set; }

    /// <summary>
    /// When the access token expires.
    /// </summary>
    public DateTime? TokenExpiresAt { get; set; }

    /// <summary>
    /// When this account was connected.
    /// </summary>
    public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When content was last imported.
    /// </summary>
    public DateTime? LastImportAt { get; set; }

    /// <summary>
    /// Number of items imported.
    /// </summary>
    public int ImportedItemCount { get; set; }

    /// <summary>
    /// Account status.
    /// </summary>
    public AccountStatus Status { get; set; } = AccountStatus.Active;

    /// <summary>
    /// Additional metadata.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>
    /// Authentication type for this connection.
    /// Default: OAuth (for backward compatibility with existing accounts).
    /// </summary>
    public AuthType AuthType { get; set; } = AuthType.OAuth;

    /// <summary>
    /// What operations are allowed for this account.
    /// Default: ImportPosts | ImportImages (read-only with images).
    /// </summary>
    public AccountPermissions Permissions { get; set; } =
        AccountPermissions.ImportPosts | AccountPermissions.ImportImages;

    /// <summary>
    /// Provider category (social-network, website, etc.).
    /// Default: social-network (for backward compatibility).
    /// </summary>
    public string ProviderCategory { get; set; } = "social-network";

    /// <summary>
    /// Base URL for website providers (e.g., "https://example.com").
    /// Only applicable for website-type providers.
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// Credentials storage (encrypted at rest).
    /// For WordPress: { "username": "admin", "appPassword": "xxxx xxxx xxxx" }
    /// For ApiKey: { "apiKey": "secret" }
    /// For NoAuth: empty
    /// </summary>
    public Dictionary<string, string> Credentials { get; set; } = new();

    // Helper properties for permission checks
    /// <summary>
    /// Can import posts/articles from this account.
    /// </summary>
    public bool CanImportPosts => Permissions.HasFlag(AccountPermissions.ImportPosts);

    /// <summary>
    /// Can publish new posts to this platform.
    /// </summary>
    public bool CanPublishPosts => Permissions.HasFlag(AccountPermissions.PublishPosts);

    /// <summary>
    /// Can import pages (website-specific: About, Contact, etc.).
    /// </summary>
    public bool CanImportPages => Permissions.HasFlag(AccountPermissions.ImportPages);

    /// <summary>
    /// Whether to download images during import.
    /// </summary>
    public bool CanImportImages => Permissions.HasFlag(AccountPermissions.ImportImages);

    /// <summary>
    /// Whether this is a website provider (vs social network).
    /// </summary>
    public bool IsWebsite => ProviderCategory == "website";

    /// <summary>
    /// Whether this is a social network provider.
    /// </summary>
    public bool IsSocialNetwork => ProviderCategory == "social-network";
}

/// <summary>
/// Authentication method used for connecting an account.
/// </summary>
public enum AuthType
{
    /// <summary>
    /// No authentication - public scraping only.
    /// Suitable for importing public content from websites.
    /// </summary>
    NoAuth = 0,

    /// <summary>
    /// OAuth 2.0 flow (LinkedIn, Facebook, Instagram, etc.).
    /// Standard OAuth authorization with access/refresh tokens.
    /// </summary>
    OAuth = 1,

    /// <summary>
    /// WordPress Application Password.
    /// Uses WordPress REST API with Basic authentication.
    /// </summary>
    WordPressAppPassword = 2,

    /// <summary>
    /// Generic API key authentication.
    /// For platforms that use API keys (Twitter, YouTube, etc.).
    /// </summary>
    ApiKey = 3
}

/// <summary>
/// Permissions flags for what an account can do.
/// Uses [Flags] to allow combining multiple permissions.
/// </summary>
[Flags]
public enum AccountPermissions
{
    /// <summary>
    /// No permissions.
    /// </summary>
    None = 0,

    /// <summary>
    /// Can import posts/articles from the platform.
    /// Read access to blog posts, social posts, articles.
    /// </summary>
    ImportPosts = 1,

    /// <summary>
    /// Can publish new posts to the platform.
    /// Write access - ability to create content on the platform.
    /// </summary>
    PublishPosts = 2,

    /// <summary>
    /// Can import pages (WordPress pages, static website pages).
    /// Website-specific: About, Contact, Services pages, etc.
    /// </summary>
    ImportPages = 4,

    /// <summary>
    /// Download and store images during import.
    /// When disabled, only image URLs are stored (not downloaded).
    /// </summary>
    ImportImages = 8,

    /// <summary>
    /// Full read access (posts + pages + images).
    /// Convenience flag for common read-only scenarios.
    /// </summary>
    FullRead = ImportPosts | ImportPages | ImportImages,

    /// <summary>
    /// Full control (read + write).
    /// All permissions enabled.
    /// </summary>
    FullControl = FullRead | PublishPosts
}

/// <summary>
/// Status of a connected account.
/// </summary>
public enum AccountStatus
{
    /// <summary>
    /// Account is active and can import data.
    /// </summary>
    Active,

    /// <summary>
    /// Token needs refresh.
    /// </summary>
    TokenExpired,

    /// <summary>
    /// User revoked access.
    /// </summary>
    Revoked,

    /// <summary>
    /// Account encountered an error.
    /// </summary>
    Error
}
