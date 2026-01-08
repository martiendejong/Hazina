using Hazina.Tools.Services.GoogleDrive.Models;

namespace Hazina.Tools.Services.GoogleDrive.Abstractions;

/// <summary>
/// Provider interface for Google Drive integration.
/// Follows the same pattern as ISocialProvider for consistency.
/// </summary>
public interface IGoogleDriveProvider
{
    /// <summary>
    /// Provider identifier: "googledrive"
    /// </summary>
    string ProviderId { get; }

    /// <summary>
    /// Display name: "Google Drive"
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Gets the OAuth authorization URL for connecting Google Drive.
    /// </summary>
    string GetAuthorizationUrl(string redirectUri, string state);

    /// <summary>
    /// Exchanges an authorization code for access tokens.
    /// </summary>
    Task<DriveAuthResult> ExchangeCodeAsync(
        string code,
        string redirectUri,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Refreshes an access token using a refresh token.
    /// </summary>
    Task<DriveAuthResult> RefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists files from Google Drive.
    /// </summary>
    Task<List<GoogleDriveFileDto>> ListFilesAsync(
        string accessToken,
        string? folderId = null,
        int maxResults = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets file metadata.
    /// </summary>
    Task<GoogleDriveFileDto> GetFileMetadataAsync(
        string accessToken,
        string driveFileId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a file from Drive.
    /// </summary>
    Task<Stream> DownloadFileAsync(
        string accessToken,
        string driveFileId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts text content from a file (including Google Docs export).
    /// </summary>
    Task<string> ExtractTextContentAsync(
        string accessToken,
        string driveFileId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of OAuth token exchange or refresh.
/// </summary>
public class DriveAuthResult
{
    public bool Success { get; set; }
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? Error { get; set; }
    public string? UserId { get; set; }
    public string? Email { get; set; }
}
