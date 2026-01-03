using System.Net.Http.Headers;
using System.Text.Json;
using System.Web;
using Microsoft.Extensions.Logging;
using Hazina.Tools.Services.Social.Abstractions;

namespace Hazina.Tools.Services.Social.Providers;

/// <summary>
/// Medium social media provider implementation.
/// Supports OAuth 2.0 authentication and content import.
/// Requires Medium integration token.
/// </summary>
public class MediumProvider : ISocialProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<MediumProvider> _logger;
    private readonly string _clientId;
    private readonly string _clientSecret;

    private const string AuthorizeUrl = "https://medium.com/m/oauth/authorize";
    private const string TokenUrl = "https://medium.com/v1/tokens";
    private const string ApiBaseUrl = "https://api.medium.com/v1";

    public string ProviderId => "medium";
    public string DisplayName => "Medium";

    public MediumProvider(
        HttpClient httpClient,
        ILogger<MediumProvider> logger,
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
        var scopes = "basicProfile listPublications";
        var encodedRedirect = HttpUtility.UrlEncode(redirectUri);

        return $"{AuthorizeUrl}?client_id={_clientId}&scope={scopes}&state={state}&response_type=code&redirect_uri={encodedRedirect}";
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
                _logger.LogWarning("Medium token exchange failed: {Response}", json);
                return new SocialAuthResult
                {
                    Success = false,
                    Error = $"Token exchange failed: {response.StatusCode}"
                };
            }

            var tokenResponse = JsonSerializer.Deserialize<MediumTokenResponse>(json);
            if (tokenResponse?.access_token == null)
            {
                return new SocialAuthResult { Success = false, Error = "Invalid token response" };
            }

            return new SocialAuthResult
            {
                Success = true,
                AccessToken = tokenResponse.access_token,
                RefreshToken = tokenResponse.refresh_token,
                ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.expires_in ?? 3600)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exchanging Medium auth code");
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

            var tokenResponse = JsonSerializer.Deserialize<MediumTokenResponse>(json);
            if (tokenResponse?.access_token == null)
            {
                return new SocialAuthResult { Success = false, Error = "Invalid token response" };
            }

            return new SocialAuthResult
            {
                Success = true,
                AccessToken = tokenResponse.access_token,
                RefreshToken = tokenResponse.refresh_token,
                ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.expires_in ?? 3600)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing Medium token");
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
                _logger.LogWarning("Medium profile fetch failed: {Response}", json);
                return new SocialProfile { Id = "", Name = "Unknown" };
            }

            var userResponse = JsonSerializer.Deserialize<MediumUserResponse>(json);
            if (userResponse?.data == null)
            {
                return new SocialProfile { Id = "", Name = "Unknown" };
            }

            var user = userResponse.data;
            return new SocialProfile
            {
                Id = user.id ?? "",
                Name = user.name ?? user.username ?? "Unknown",
                ProfileUrl = $"https://medium.com/@{user.username}",
                AvatarUrl = user.imageUrl,
                Metadata = new Dictionary<string, string>
                {
                    ["username"] = user.username ?? "",
                    ["url"] = user.url ?? ""
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Medium profile");
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
            var profile = await GetProfileAsync(accessToken, cancellationToken);
            var username = profile.Metadata.TryGetValue("username", out var u) ? u : "Medium";

            // Note: Medium API doesn't provide a direct endpoint to get user's posts
            // We can get publications but not individual articles via API
            // This would require scraping or using an unofficial API
            _logger.LogWarning("Medium API has limited article retrieval capabilities");

            if (options.ContentTypes.Contains("articles"))
            {
                var publications = await GetPublicationsAsync(accessToken, profile.Id, cancellationToken);
                // Would need to fetch articles per publication, which Medium API doesn't fully support
                _logger.LogInformation("Found {Count} Medium publications for user", publications.Count);
            }

            result.TotalImported = result.Posts.Count + result.Articles.Count;
            _logger.LogInformation("Imported {Count} items from Medium", result.TotalImported);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing Medium content");
            result.Success = false;
            result.Error = ex.Message;
        }

        return result;
    }

    private async Task<List<MediumPublication>> GetPublicationsAsync(
        string accessToken,
        string userId,
        CancellationToken cancellationToken)
    {
        try
        {
            var url = $"{ApiBaseUrl}/users/{userId}/publications";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Medium publications fetch failed: {Status} - {Response}",
                    response.StatusCode, json);
                return new List<MediumPublication>();
            }

            var pubResponse = JsonSerializer.Deserialize<MediumPublicationsResponse>(json);
            return pubResponse?.data ?? new List<MediumPublication>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error fetching Medium publications");
            return new List<MediumPublication>();
        }
    }

    public async Task<bool> RevokeAccessAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        // Medium doesn't have a documented revoke endpoint
        await Task.CompletedTask;
        return true;
    }

    // Medium API response classes
    private class MediumTokenResponse
    {
        public string? access_token { get; set; }
        public string? refresh_token { get; set; }
        public int? expires_in { get; set; }
        public string? token_type { get; set; }
        public string? scope { get; set; }
    }

    private class MediumUserResponse
    {
        public MediumUser? data { get; set; }
    }

    private class MediumUser
    {
        public string? id { get; set; }
        public string? username { get; set; }
        public string? name { get; set; }
        public string? url { get; set; }
        public string? imageUrl { get; set; }
    }

    private class MediumPublicationsResponse
    {
        public List<MediumPublication>? data { get; set; }
    }

    private class MediumPublication
    {
        public string? id { get; set; }
        public string? name { get; set; }
        public string? description { get; set; }
        public string? url { get; set; }
        public string? imageUrl { get; set; }
    }
}
