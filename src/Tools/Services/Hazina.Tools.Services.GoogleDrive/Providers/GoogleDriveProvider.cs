using System.Text;
using System.Text.Json;

using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;

using Hazina.Tools.Services.GoogleDrive.Abstractions;
using Hazina.Tools.Services.GoogleDrive.Models;

using Microsoft.Extensions.Logging;

namespace Hazina.Tools.Services.GoogleDrive.Providers;

/// <summary>
/// Google Drive provider implementation.
/// Handles OAuth and file operations via Google Drive API v3.
/// </summary>
public class GoogleDriveProvider : IGoogleDriveProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GoogleDriveProvider> _logger;
    private readonly string _clientId;
    private readonly string _clientSecret;

    public string ProviderId
    {
        get
        {
            return "googledrive";
        }
    }

    public string DisplayName
    {
        get
        {
            return "Google Drive";
        }
    }

    public GoogleDriveProvider(
        HttpClient httpClient,
        ILogger<GoogleDriveProvider> logger,
        string clientId,
        string clientSecret)
    {
        _httpClient = httpClient;
        _logger = logger;
        _clientId = clientId;
        _clientSecret = clientSecret;
    }

    public string GetAuthorizationUrl(string redirectUri, string state)
    {
        var scopes = new[]
        {
            "https://www.googleapis.com/auth/drive.readonly",
            "https://www.googleapis.com/auth/drive.metadata.readonly",
            "openid",
            "email",
            "profile"
        };

        var authUrl = $"https://accounts.google.com/o/oauth2/v2/auth?" +
            $"client_id={_clientId}&" +
            $"redirect_uri={Uri.EscapeDataString(redirectUri)}&" +
            $"response_type=code&" +
            $"scope={Uri.EscapeDataString(string.Join(" ", scopes))}&" +
            $"access_type=offline&" +
            $"prompt=consent&" +
            $"state={state}";

        return authUrl;
    }

    public async Task<DriveAuthResult> ExchangeCodeAsync(
        string code,
        string redirectUri,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["code"] = code,
                ["client_id"] = _clientId,
                ["client_secret"] = _clientSecret,
                ["redirect_uri"] = redirectUri,
                ["grant_type"] = "authorization_code"
            });

            var response = await _httpClient.PostAsync(
                "https://oauth2.googleapis.com/token",
                content,
                cancellationToken);

            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Google Drive token exchange failed: {Response}", json);
                return new DriveAuthResult
                {
                    Success = false,
                    Error = $"Token exchange failed: {response.StatusCode}"
                };
            }

            var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (tokenResponse == null)
            {
                return new DriveAuthResult { Success = false, Error = "Invalid token response" };
            }

            // Get user info
            var userInfo = await GetUserInfoAsync(tokenResponse.AccessToken, cancellationToken);

            return new DriveAuthResult
            {
                Success = true,
                AccessToken = tokenResponse.AccessToken,
                RefreshToken = tokenResponse.RefreshToken,
                ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn),
                UserId = userInfo?.Id,
                Email = userInfo?.Email
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exchanging Google Drive auth code");
            return new DriveAuthResult { Success = false, Error = ex.Message };
        }
    }

    public async Task<DriveAuthResult> RefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["refresh_token"] = refreshToken,
                ["client_id"] = _clientId,
                ["client_secret"] = _clientSecret,
                ["grant_type"] = "refresh_token"
            });

            var response = await _httpClient.PostAsync(
                "https://oauth2.googleapis.com/token",
                content,
                cancellationToken);

            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Google Drive token refresh failed: {Response}", json);
                return new DriveAuthResult
                {
                    Success = false,
                    Error = $"Token refresh failed: {response.StatusCode}"
                };
            }

            var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (tokenResponse == null)
            {
                return new DriveAuthResult { Success = false, Error = "Invalid token response" };
            }

            return new DriveAuthResult
            {
                Success = true,
                AccessToken = tokenResponse.AccessToken,
                RefreshToken = refreshToken, // Refresh token stays the same
                ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing Google Drive token");
            return new DriveAuthResult { Success = false, Error = ex.Message };
        }
    }

    public async Task<List<GoogleDriveFileDto>> ListFilesAsync(
        string accessToken,
        string? folderId = null,
        int maxResults = 100,
        CancellationToken cancellationToken = default)
    {
        var service = CreateDriveService(accessToken);

        var request = service.Files.List();
        request.PageSize = Math.Min(maxResults, 1000);
        request.Fields = "files(id,name,mimeType,size,modifiedTime,webViewLink)";

        if (!string.IsNullOrEmpty(folderId))
        {
            request.Q = $"'{folderId}' in parents and trashed = false";
        }
        else
        {
            request.Q = "trashed = false";
        }

        var result = await request.ExecuteAsync(cancellationToken);

        return result.Files.Select(f => new GoogleDriveFileDto
        {
            Id = f.Id,
            Name = f.Name,
            MimeType = f.MimeType,
            Size = f.Size ?? 0,
            ModifiedTime = f.ModifiedTime ?? DateTime.UtcNow,
            WebViewLink = f.WebViewLink,
            IsSynced = false,
            IsEmbedded = false
        }).ToList();
    }

    public async Task<GoogleDriveFileDto> GetFileMetadataAsync(
        string accessToken,
        string driveFileId,
        CancellationToken cancellationToken = default)
    {
        var service = CreateDriveService(accessToken);

        var request = service.Files.Get(driveFileId);
        request.Fields = "id,name,mimeType,size,modifiedTime,webViewLink";

        var file = await request.ExecuteAsync(cancellationToken);

        return new GoogleDriveFileDto
        {
            Id = file.Id,
            Name = file.Name,
            MimeType = file.MimeType,
            Size = file.Size ?? 0,
            ModifiedTime = file.ModifiedTime ?? DateTime.UtcNow,
            WebViewLink = file.WebViewLink,
            IsSynced = false,
            IsEmbedded = false
        };
    }

    public async Task<Stream> DownloadFileAsync(
        string accessToken,
        string driveFileId,
        CancellationToken cancellationToken = default)
    {
        var service = CreateDriveService(accessToken);

        var stream = new MemoryStream();
        await service.Files.Get(driveFileId).DownloadAsync(stream, cancellationToken);
        stream.Position = 0;

        return stream;
    }

    public async Task<string> ExtractTextContentAsync(
        string accessToken,
        string driveFileId,
        CancellationToken cancellationToken = default)
    {
        var metadata = await GetFileMetadataAsync(accessToken, driveFileId, cancellationToken);

        // Google Docs: export as plain text
        if (metadata.MimeType == "application/vnd.google-apps.document")
        {
            return await ExportGoogleDocAsTextAsync(accessToken, driveFileId, cancellationToken);
        }

        // Other files: download and extract (simplified)
        using var stream = await DownloadFileAsync(accessToken, driveFileId, cancellationToken);
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    private async Task<string> ExportGoogleDocAsTextAsync(
        string accessToken,
        string fileId,
        CancellationToken cancellationToken)
    {
        var service = CreateDriveService(accessToken);

        var stream = new MemoryStream();
        await service.Files.Export(fileId, "text/plain").DownloadAsync(stream, cancellationToken);

        stream.Position = 0;
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    private async Task<UserInfo?> GetUserInfoAsync(string accessToken, CancellationToken cancellationToken)
    {
        try
        {
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.GetAsync(
                "https://www.googleapis.com/oauth2/v2/userinfo",
                cancellationToken);

            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("User info fetch failed: {Response}", json);
                return null;
            }

            return JsonSerializer.Deserialize<UserInfo>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user info");
            return null;
        }
    }

    private DriveService CreateDriveService(string accessToken)
    {
        var credential = GoogleCredential.FromAccessToken(accessToken);
        return new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "Hazina GoogleDrive Integration"
        });
    }

    #region Helper Classes

    private class TokenResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string? RefreshToken { get; set; }
        public int ExpiresIn { get; set; }
    }

    private class UserInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    #endregion
}
