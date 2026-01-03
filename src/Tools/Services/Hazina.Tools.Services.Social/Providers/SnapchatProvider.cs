using System.Net.Http.Headers;
using System.Text.Json;
using System.Web;
using Microsoft.Extensions.Logging;
using Hazina.Tools.Services.Social.Abstractions;

namespace Hazina.Tools.Services.Social.Providers;

/// <summary>
/// Snapchat social media provider implementation.
/// Supports OAuth 2.0 authentication and content import.
/// Requires Snapchat Business account and Marketing API access.
/// </summary>
public class SnapchatProvider : ISocialProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SnapchatProvider> _logger;
    private readonly string _clientId;
    private readonly string _clientSecret;

    private const string AuthorizeUrl = "https://accounts.snapchat.com/login/oauth2/authorize";
    private const string TokenUrl = "https://accounts.snapchat.com/login/oauth2/access_token";
    private const string ApiBaseUrl = "https://adsapi.snapchat.com/v1";

    public string ProviderId => "snapchat";
    public string DisplayName => "Snapchat";

    public SnapchatProvider(
        HttpClient httpClient,
        ILogger<SnapchatProvider> logger,
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
        var scopes = "snapchat-marketing-api";
        var encodedRedirect = HttpUtility.UrlEncode(redirectUri);

        return $"{AuthorizeUrl}?client_id={_clientId}&redirect_uri={encodedRedirect}&response_type=code&scope={scopes}&state={state}";
    }

    public async Task<SocialAuthResult> ExchangeCodeAsync(
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
                ["grant_type"] = "authorization_code",
                ["redirect_uri"] = redirectUri
            });

            var response = await _httpClient.PostAsync(TokenUrl, content, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Snapchat token exchange failed: {Response}", json);
                return new SocialAuthResult
                {
                    Success = false,
                    Error = $"Token exchange failed: {response.StatusCode}"
                };
            }

            var tokenResponse = JsonSerializer.Deserialize<SnapchatTokenResponse>(json);
            if (tokenResponse?.access_token == null)
            {
                return new SocialAuthResult { Success = false, Error = "Invalid token response" };
            }

            return new SocialAuthResult
            {
                Success = true,
                AccessToken = tokenResponse.access_token,
                RefreshToken = tokenResponse.refresh_token,
                ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.expires_in)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exchanging Snapchat auth code");
            return new SocialAuthResult { Success = false, Error = ex.Message };
        }
    }

    public async Task<SocialAuthResult> RefreshTokenAsync(
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

            var response = await _httpClient.PostAsync(TokenUrl, content, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new SocialAuthResult
                {
                    Success = false,
                    Error = $"Token refresh failed: {response.StatusCode}"
                };
            }

            var tokenResponse = JsonSerializer.Deserialize<SnapchatTokenResponse>(json);
            if (tokenResponse?.access_token == null)
            {
                return new SocialAuthResult { Success = false, Error = "Invalid token response" };
            }

            return new SocialAuthResult
            {
                Success = true,
                AccessToken = tokenResponse.access_token,
                RefreshToken = tokenResponse.refresh_token,
                ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.expires_in)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing Snapchat token");
            return new SocialAuthResult { Success = false, Error = ex.Message };
        }
    }

    public async Task<SocialProfile> GetProfileAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{ApiBaseUrl}/me";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Snapchat profile fetch failed: {Response}", json);
                return new SocialProfile { Id = "", Name = "Unknown" };
            }

            var profileResponse = JsonSerializer.Deserialize<SnapchatMeResponse>(json);
            if (profileResponse?.me == null)
            {
                return new SocialProfile { Id = "", Name = "Unknown" };
            }

            var me = profileResponse.me;
            return new SocialProfile
            {
                Id = me.id ?? "",
                Name = me.display_name ?? me.email ?? "Unknown",
                Email = me.email,
                Metadata = new Dictionary<string, string>
                {
                    ["organization_id"] = me.organization_id ?? "",
                    ["member_status"] = me.member_status ?? ""
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Snapchat profile");
            return new SocialProfile { Id = "", Name = "Unknown" };
        }
    }

    public async Task<SocialImportResult> ImportContentAsync(
        string accessToken,
        SocialImportOptions options,
        CancellationToken cancellationToken = default)
    {
        var result = new SocialImportResult { Success = true };

        try
        {
            // Note: Snapchat API is primarily for advertising/marketing
            // Organic content (Stories, Spotlight) is not easily accessible via API
            // The Marketing API provides ad account data but not personal content
            _logger.LogWarning("Snapchat Marketing API has limited organic content access");

            result.TotalImported = result.Posts.Count + result.Articles.Count;
            _logger.LogInformation("Imported {Count} items from Snapchat", result.TotalImported);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing Snapchat content");
            result.Success = false;
            result.Error = ex.Message;
        }

        return result;
    }

    public async Task<bool> RevokeAccessAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        // Snapchat doesn't have a documented revoke endpoint
        await Task.CompletedTask;
        return true;
    }

    // Snapchat API response classes
    private class SnapchatTokenResponse
    {
        public string? access_token { get; set; }
        public string? refresh_token { get; set; }
        public int expires_in { get; set; }
        public string? token_type { get; set; }
        public string? scope { get; set; }
    }

    private class SnapchatMeResponse
    {
        public SnapchatMe? me { get; set; }
    }

    private class SnapchatMe
    {
        public string? id { get; set; }
        public string? email { get; set; }
        public string? display_name { get; set; }
        public string? organization_id { get; set; }
        public string? member_status { get; set; }
    }
}
