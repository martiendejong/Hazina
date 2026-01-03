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
