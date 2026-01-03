using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Hazina.Tools.Services.Social.Abstractions;

namespace Hazina.Tools.Services.Social.Providers;

/// <summary>
/// Bluesky social media provider implementation.
/// Supports AT Protocol authentication and content import.
/// Bluesky uses a different auth model (app passwords) instead of traditional OAuth.
/// </summary>
public class BlueskyProvider : ISocialProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BlueskyProvider> _logger;

    private const string ApiBaseUrl = "https://bsky.social/xrpc";

    public string ProviderId => "bluesky";
    public string DisplayName => "Bluesky";

    public BlueskyProvider(
        HttpClient httpClient,
        ILogger<BlueskyProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public string GetAuthorizationUrl(string redirectUri, string state)
    {
        // Bluesky doesn't use traditional OAuth - uses app passwords
        // Return a custom URL that indicates manual auth is needed
        return $"{redirectUri}?state={state}&provider=bluesky&manual_auth=true";
    }

    public async Task<SocialAuthResult> ExchangeCodeAsync(
        string code,
        string redirectUri,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Code format: "identifier:app_password"
            var parts = code.Split(':');
            if (parts.Length != 2)
            {
                return new SocialAuthResult { Success = false, Error = "Invalid credentials format. Expected 'handle:app_password'" };
            }

            var identifier = parts[0];
            var password = parts[1];

            var requestBody = new
            {
                identifier = identifier,
                password = password
            };

            var content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync($"{ApiBaseUrl}/com.atproto.server.createSession", content, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Bluesky session creation failed: {Response}", json);
                return new SocialAuthResult
                {
                    Success = false,
                    Error = $"Authentication failed: {response.StatusCode}"
                };
            }

            var sessionResponse = JsonSerializer.Deserialize<BlueskySessionResponse>(json);
            if (sessionResponse?.accessJwt == null)
            {
                return new SocialAuthResult { Success = false, Error = "Invalid session response" };
            }

            return new SocialAuthResult
            {
                Success = true,
                AccessToken = sessionResponse.accessJwt,
                RefreshToken = sessionResponse.refreshJwt,
                ExpiresAt = DateTime.UtcNow.AddDays(90) // Bluesky tokens are long-lived
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Bluesky session");
            return new SocialAuthResult { Success = false, Error = ex.Message };
        }
    }

    public async Task<SocialAuthResult> RefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"{ApiBaseUrl}/com.atproto.server.refreshSession");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshToken);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new SocialAuthResult
                {
                    Success = false,
                    Error = $"Token refresh failed: {response.StatusCode}"
                };
            }

            var sessionResponse = JsonSerializer.Deserialize<BlueskySessionResponse>(json);
            if (sessionResponse?.accessJwt == null)
            {
                return new SocialAuthResult { Success = false, Error = "Invalid session response" };
            }

            return new SocialAuthResult
            {
                Success = true,
                AccessToken = sessionResponse.accessJwt,
                RefreshToken = sessionResponse.refreshJwt,
                ExpiresAt = DateTime.UtcNow.AddDays(90)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing Bluesky session");
            return new SocialAuthResult { Success = false, Error = ex.Message };
        }
    }

    public async Task<SocialProfile> GetProfileAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiBaseUrl}/com.atproto.server.getSession");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Bluesky profile fetch failed: {Response}", json);
                return new SocialProfile { Id = "", Name = "Unknown" };
            }

            var sessionResponse = JsonSerializer.Deserialize<BlueskySessionResponse>(json);
            if (sessionResponse == null)
            {
                return new SocialProfile { Id = "", Name = "Unknown" };
            }

            return new SocialProfile
            {
                Id = sessionResponse.did ?? "",
                Name = sessionResponse.handle ?? "Unknown",
                ProfileUrl = $"https://bsky.app/profile/{sessionResponse.handle}",
                Metadata = new Dictionary<string, string>
                {
                    ["handle"] = sessionResponse.handle ?? "",
                    ["did"] = sessionResponse.did ?? ""
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Bluesky profile");
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
            var handle = profile.Metadata.TryGetValue("handle", out var h) ? h : "";

            if (options.ContentTypes.Contains("posts") && !string.IsNullOrEmpty(handle))
            {
                var posts = await ImportPostsAsync(accessToken, handle, options, cancellationToken);
                result.Posts.AddRange(posts);
            }

            result.TotalImported = result.Posts.Count + result.Articles.Count;
            _logger.LogInformation("Imported {Count} items from Bluesky", result.TotalImported);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing Bluesky content");
            result.Success = false;
            result.Error = ex.Message;
        }

        return result;
    }

    private async Task<List<SocialPost>> ImportPostsAsync(
        string accessToken,
        string handle,
        SocialImportOptions options,
        CancellationToken cancellationToken)
    {
        var posts = new List<SocialPost>();

        try
        {
            var url = $"{ApiBaseUrl}/app.bsky.feed.getAuthorFeed?actor={handle}&limit={Math.Min(options.MaxItems, 100)}";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Bluesky posts fetch failed: {Status} - {Response}",
                    response.StatusCode, json);
                return posts;
            }

            var feedResponse = JsonSerializer.Deserialize<BlueskyFeedResponse>(json);
            if (feedResponse?.feed == null)
            {
                return posts;
            }

            foreach (var item in feedResponse.feed)
            {
                var post = item.post;
                if (post?.record == null)
                    continue;

                var createdAt = DateTime.Parse(post.record.createdAt ?? DateTime.UtcNow.ToString());
                if (options.Since.HasValue && createdAt < options.Since.Value)
                    continue;

                var socialPost = new SocialPost
                {
                    Id = post.uri ?? "",
                    AccountId = handle,
                    Content = post.record.text ?? "",
                    CreatedAt = createdAt,
                    Url = $"https://bsky.app/profile/{handle}/post/{post.uri?.Split('/').Last()}",
                    Metadata = new Dictionary<string, string>
                    {
                        ["source"] = handle,
                        ["likes"] = post.likeCount?.ToString() ?? "0",
                        ["reposts"] = post.repostCount?.ToString() ?? "0",
                        ["replies"] = post.replyCount?.ToString() ?? "0"
                    }
                };

                posts.Add(socialPost);
            }

            _logger.LogInformation("Imported {Count} posts from Bluesky @{Handle}", posts.Count, handle);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error fetching Bluesky posts for @{Handle}", handle);
        }

        return posts;
    }

    public async Task<bool> RevokeAccessAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"{ApiBaseUrl}/com.atproto.server.deleteSession");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking Bluesky session");
            return false;
        }
    }

    // Bluesky API response classes
    private class BlueskySessionResponse
    {
        public string? did { get; set; }
        public string? handle { get; set; }
        public string? email { get; set; }
        public string? accessJwt { get; set; }
        public string? refreshJwt { get; set; }
    }

    private class BlueskyFeedResponse
    {
        public List<BlueskyFeedItem>? feed { get; set; }
        public string? cursor { get; set; }
    }

    private class BlueskyFeedItem
    {
        public BlueskyPost? post { get; set; }
    }

    private class BlueskyPost
    {
        public string? uri { get; set; }
        public string? cid { get; set; }
        public BlueskyPostRecord? record { get; set; }
        public int? replyCount { get; set; }
        public int? repostCount { get; set; }
        public int? likeCount { get; set; }
    }

    private class BlueskyPostRecord
    {
        public string? text { get; set; }
        public string? createdAt { get; set; }
    }
}
