using System.Net.Http.Headers;
using System.Text.Json;
using System.Web;
using Microsoft.Extensions.Logging;
using Hazina.Tools.Services.Social.Abstractions;

namespace Hazina.Tools.Services.Social.Providers;

/// <summary>
/// Threads (by Meta/Instagram) social media provider implementation.
/// Supports OAuth 2.0 authentication and content import.
/// Uses Instagram Graph API with Threads permissions.
/// </summary>
public class ThreadsProvider : ISocialProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ThreadsProvider> _logger;
    private readonly string _appId;
    private readonly string _appSecret;

    private const string AuthorizeUrl = "https://www.facebook.com/v18.0/dialog/oauth";
    private const string TokenUrl = "https://graph.facebook.com/v18.0/oauth/access_token";
    private const string GraphApiUrl = "https://graph.threads.net/v1.0";

    public string ProviderId => "threads";
    public string DisplayName => "Threads";

    public ThreadsProvider(
        HttpClient httpClient,
        ILogger<ThreadsProvider> logger,
        string appId,
        string appSecret)
    {
        _httpClient = httpClient;
        _logger = logger;
        _appId = appId;
        _appSecret = appSecret;
    }

    public string GetAuthorizationUrl(string redirectUri, string state)
    {
        // Threads uses Facebook OAuth with specific permissions
        var scopes = "threads_basic,threads_content_publish,threads_manage_insights,threads_manage_replies,threads_read_replies";
        var encodedRedirect = HttpUtility.UrlEncode(redirectUri);
        var encodedScopes = HttpUtility.UrlEncode(scopes);

        return $"{AuthorizeUrl}?client_id={_appId}&redirect_uri={encodedRedirect}&state={state}&scope={encodedScopes}&response_type=code";
    }

    public async Task<SocialAuthResult> ExchangeCodeAsync(
        string code,
        string redirectUri,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{TokenUrl}?client_id={_appId}&client_secret={_appSecret}&code={code}&redirect_uri={HttpUtility.UrlEncode(redirectUri)}";

            var response = await _httpClient.GetAsync(url, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Threads token exchange failed: {Response}", json);
                return new SocialAuthResult
                {
                    Success = false,
                    Error = $"Token exchange failed: {response.StatusCode}"
                };
            }

            var tokenResponse = JsonSerializer.Deserialize<ThreadsTokenResponse>(json);
            if (tokenResponse?.access_token == null)
            {
                return new SocialAuthResult { Success = false, Error = "Invalid token response" };
            }

            // Exchange for long-lived token
            var longLivedToken = await ExchangeForLongLivedTokenAsync(tokenResponse.access_token, cancellationToken);
            if (longLivedToken == null)
            {
                return new SocialAuthResult { Success = false, Error = "Failed to get long-lived token" };
            }

            return new SocialAuthResult
            {
                Success = true,
                AccessToken = longLivedToken.access_token,
                ExpiresAt = DateTime.UtcNow.AddSeconds(longLivedToken.expires_in)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exchanging Threads auth code");
            return new SocialAuthResult { Success = false, Error = ex.Message };
        }
    }

    private async Task<ThreadsLongLivedTokenResponse?> ExchangeForLongLivedTokenAsync(
        string shortLivedToken,
        CancellationToken cancellationToken)
    {
        try
        {
            var url = $"https://graph.facebook.com/v18.0/oauth/access_token?grant_type=fb_exchange_token&client_id={_appId}&client_secret={_appSecret}&fb_exchange_token={shortLivedToken}";

            var response = await _httpClient.GetAsync(url, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Long-lived token exchange failed: {Response}", json);
                return null;
            }

            return JsonSerializer.Deserialize<ThreadsLongLivedTokenResponse>(json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error exchanging for long-lived token");
            return null;
        }
    }

    public async Task<SocialAuthResult> RefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"https://graph.facebook.com/v18.0/oauth/access_token?grant_type=fb_exchange_token&client_id={_appId}&client_secret={_appSecret}&fb_exchange_token={refreshToken}";

            var response = await _httpClient.GetAsync(url, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new SocialAuthResult
                {
                    Success = false,
                    Error = $"Token refresh failed: {response.StatusCode}"
                };
            }

            var tokenResponse = JsonSerializer.Deserialize<ThreadsLongLivedTokenResponse>(json);
            if (tokenResponse?.access_token == null)
            {
                return new SocialAuthResult { Success = false, Error = "Invalid token response" };
            }

            return new SocialAuthResult
            {
                Success = true,
                AccessToken = tokenResponse.access_token,
                ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.expires_in)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing Threads token");
            return new SocialAuthResult { Success = false, Error = ex.Message };
        }
    }

    public async Task<SocialProfile> GetProfileAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{GraphApiUrl}/me?fields=id,username,threads_profile_picture_url,threads_biography&access_token={accessToken}";

            var response = await _httpClient.GetAsync(url, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Threads profile fetch failed: {Response}", json);
                return new SocialProfile { Id = "", Name = "Unknown" };
            }

            var userResponse = JsonSerializer.Deserialize<ThreadsUserResponse>(json);
            if (userResponse == null)
            {
                return new SocialProfile { Id = "", Name = "Unknown" };
            }

            return new SocialProfile
            {
                Id = userResponse.id ?? "",
                Name = userResponse.username ?? "Unknown",
                ProfileUrl = $"https://www.threads.net/@{userResponse.username}",
                AvatarUrl = userResponse.threads_profile_picture_url,
                Metadata = new Dictionary<string, string>
                {
                    ["username"] = userResponse.username ?? "",
                    ["biography"] = userResponse.threads_biography ?? ""
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Threads profile");
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
            var username = profile.Metadata.TryGetValue("username", out var u) ? u : "Threads";

            if (options.ContentTypes.Contains("posts"))
            {
                var threads = await ImportThreadsAsync(accessToken, profile.Id, username, options, cancellationToken);
                result.Posts.AddRange(threads);
            }

            result.TotalImported = result.Posts.Count + result.Articles.Count;
            _logger.LogInformation("Imported {Count} items from Threads", result.TotalImported);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing Threads content");
            result.Success = false;
            result.Error = ex.Message;
        }

        return result;
    }

    private async Task<List<SocialPost>> ImportThreadsAsync(
        string accessToken,
        string userId,
        string username,
        SocialImportOptions options,
        CancellationToken cancellationToken)
    {
        var posts = new List<SocialPost>();

        try
        {
            var url = $"{GraphApiUrl}/{userId}/threads?fields=id,media_type,media_url,permalink,text,timestamp,shortcode,thumbnail_url,is_quote_post&limit={Math.Min(options.MaxItems, 100)}&access_token={accessToken}";

            var response = await _httpClient.GetAsync(url, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Threads fetch failed: {Status} - {Response}",
                    response.StatusCode, json);
                return posts;
            }

            var threadsResponse = JsonSerializer.Deserialize<ThreadsMediaResponse>(json);
            if (threadsResponse?.data == null)
            {
                return posts;
            }

            foreach (var thread in threadsResponse.data)
            {
                var createdAt = DateTime.Parse(thread.timestamp ?? DateTime.UtcNow.ToString());
                if (options.Since.HasValue && createdAt < options.Since.Value)
                    continue;

                var post = new SocialPost
                {
                    Id = thread.id ?? "",
                    AccountId = userId,
                    Content = thread.text ?? "",
                    CreatedAt = createdAt,
                    Url = thread.permalink ?? $"https://www.threads.net/@{username}/post/{thread.shortcode}",
                    Metadata = new Dictionary<string, string>
                    {
                        ["source"] = username,
                        ["media_type"] = thread.media_type ?? "TEXT",
                        ["media_url"] = thread.media_url ?? "",
                        ["thumbnail_url"] = thread.thumbnail_url ?? "",
                        ["is_quote_post"] = thread.is_quote_post?.ToString() ?? "false"
                    }
                };

                posts.Add(post);
            }

            _logger.LogInformation("Imported {Count} threads from @{Username}", posts.Count, username);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error fetching Threads for @{Username}", username);
        }

        return posts;
    }

    public async Task<bool> RevokeAccessAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"https://graph.facebook.com/v18.0/me/permissions?access_token={accessToken}";
            var response = await _httpClient.DeleteAsync(url, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking Threads access");
            return false;
        }
    }

    // Threads API response classes
    private class ThreadsTokenResponse
    {
        public string? access_token { get; set; }
        public string? token_type { get; set; }
    }

    private class ThreadsLongLivedTokenResponse
    {
        public string access_token { get; set; } = "";
        public string? token_type { get; set; }
        public int expires_in { get; set; }
    }

    private class ThreadsUserResponse
    {
        public string? id { get; set; }
        public string? username { get; set; }
        public string? threads_profile_picture_url { get; set; }
        public string? threads_biography { get; set; }
    }

    private class ThreadsMediaResponse
    {
        public List<ThreadsMedia>? data { get; set; }
        public ThreadsPaging? paging { get; set; }
    }

    private class ThreadsMedia
    {
        public string? id { get; set; }
        public string? media_type { get; set; }
        public string? media_url { get; set; }
        public string? permalink { get; set; }
        public string? text { get; set; }
        public string? timestamp { get; set; }
        public string? shortcode { get; set; }
        public string? thumbnail_url { get; set; }
        public bool? is_quote_post { get; set; }
    }

    private class ThreadsPaging
    {
        public string? next { get; set; }
        public string? previous { get; set; }
    }
}
