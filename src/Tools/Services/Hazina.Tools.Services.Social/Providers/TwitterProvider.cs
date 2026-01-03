using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Web;
using Microsoft.Extensions.Logging;
using Hazina.Tools.Services.Social.Abstractions;

namespace Hazina.Tools.Services.Social.Providers;

/// <summary>
/// X (Twitter) social media provider implementation.
/// Supports OAuth 2.0 authentication and content import.
/// Requires Twitter API v2 access and elevated permissions.
/// </summary>
public class TwitterProvider : ISocialProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TwitterProvider> _logger;
    private readonly string _clientId;
    private readonly string _clientSecret;

    private const string AuthorizeUrl = "https://twitter.com/i/oauth2/authorize";
    private const string TokenUrl = "https://api.twitter.com/2/oauth2/token";
    private const string RevokeUrl = "https://api.twitter.com/2/oauth2/revoke";
    private const string ApiBaseUrl = "https://api.twitter.com/2";

    public string ProviderId => "twitter";
    public string DisplayName => "X (Twitter)";

    public TwitterProvider(
        HttpClient httpClient,
        ILogger<TwitterProvider> logger,
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
        // Twitter OAuth 2.0 with PKCE
        var scopes = "tweet.read users.read offline.access";
        var encodedRedirect = HttpUtility.UrlEncode(redirectUri);
        var encodedScopes = HttpUtility.UrlEncode(scopes);

        // Generate code_challenge for PKCE (in production, this should be properly generated and stored)
        var codeChallenge = "challenge"; // Placeholder - should be SHA256 hash of code_verifier

        return $"{AuthorizeUrl}?response_type=code&client_id={_clientId}&redirect_uri={encodedRedirect}&scope={encodedScopes}&state={state}&code_challenge={codeChallenge}&code_challenge_method=plain";
    }

    public async Task<SocialAuthResult> ExchangeCodeAsync(
        string code,
        string redirectUri,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var requestBody = new Dictionary<string, string>
            {
                ["code"] = code,
                ["grant_type"] = "authorization_code",
                ["client_id"] = _clientId,
                ["redirect_uri"] = redirectUri,
                ["code_verifier"] = "challenge" // Should match the one used in GetAuthorizationUrl
            };

            var content = new FormUrlEncodedContent(requestBody);

            // Twitter requires Basic Auth with client credentials
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_clientId}:{_clientSecret}"));
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);

            var response = await _httpClient.PostAsync(TokenUrl, content, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Twitter token exchange failed: {Response}", json);
                return new SocialAuthResult
                {
                    Success = false,
                    Error = $"Token exchange failed: {response.StatusCode}"
                };
            }

            var tokenResponse = JsonSerializer.Deserialize<TwitterTokenResponse>(json);
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
            _logger.LogError(ex, "Error exchanging Twitter auth code");
            return new SocialAuthResult { Success = false, Error = ex.Message };
        }
    }

    public async Task<SocialAuthResult> RefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var requestBody = new Dictionary<string, string>
            {
                ["refresh_token"] = refreshToken,
                ["grant_type"] = "refresh_token",
                ["client_id"] = _clientId
            };

            var content = new FormUrlEncodedContent(requestBody);

            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_clientId}:{_clientSecret}"));
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);

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

            var tokenResponse = JsonSerializer.Deserialize<TwitterTokenResponse>(json);
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
            _logger.LogError(ex, "Error refreshing Twitter token");
            return new SocialAuthResult { Success = false, Error = ex.Message };
        }
    }

    public async Task<SocialProfile> GetProfileAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{ApiBaseUrl}/users/me?user.fields=id,name,username,profile_image_url,description";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Twitter profile fetch failed: {Response}", json);
                return new SocialProfile { Id = "", Name = "Unknown" };
            }

            var profileResponse = JsonSerializer.Deserialize<TwitterUserResponse>(json);
            if (profileResponse?.data == null)
            {
                return new SocialProfile { Id = "", Name = "Unknown" };
            }

            var user = profileResponse.data;
            return new SocialProfile
            {
                Id = user.id ?? "",
                Name = user.name ?? user.username ?? "Unknown",
                ProfileUrl = user.username != null ? $"https://twitter.com/{user.username}" : "",
                AvatarUrl = user.profile_image_url,
                Metadata = new Dictionary<string, string>
                {
                    ["username"] = user.username ?? "",
                    ["description"] = user.description ?? ""
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Twitter profile");
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
            var username = profile.Metadata.TryGetValue("username", out var u) ? u : "Twitter";

            if (options.ContentTypes.Contains("posts"))
            {
                var tweets = await ImportTweetsAsync(accessToken, profile.Id, username, options, cancellationToken);
                result.Posts.AddRange(tweets);
            }

            result.TotalImported = result.Posts.Count + result.Articles.Count;
            _logger.LogInformation("Imported {Count} items from Twitter", result.TotalImported);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing Twitter content");
            result.Success = false;
            result.Error = ex.Message;
        }

        return result;
    }

    private async Task<List<SocialPost>> ImportTweetsAsync(
        string accessToken,
        string userId,
        string username,
        SocialImportOptions options,
        CancellationToken cancellationToken)
    {
        var posts = new List<SocialPost>();

        try
        {
            // Get user timeline tweets
            var url = $"{ApiBaseUrl}/users/{userId}/tweets?tweet.fields=created_at,public_metrics,entities&max_results={Math.Min(options.MaxItems, 100)}";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Twitter tweets fetch failed: {Status} - {Response}",
                    response.StatusCode, json);
                return posts;
            }

            var tweetsResponse = JsonSerializer.Deserialize<TwitterTweetsResponse>(json);
            if (tweetsResponse?.data == null)
            {
                return posts;
            }

            foreach (var tweet in tweetsResponse.data)
            {
                var post = new SocialPost
                {
                    Id = tweet.id ?? "",
                    AccountId = userId,
                    Content = tweet.text ?? "",
                    CreatedAt = DateTime.Parse(tweet.created_at ?? DateTime.UtcNow.ToString()),
                    Url = $"https://twitter.com/{username}/status/{tweet.id}",
                    Metadata = new Dictionary<string, string>
                    {
                        ["source"] = username,
                        ["likes"] = tweet.public_metrics?.like_count.ToString() ?? "0",
                        ["retweets"] = tweet.public_metrics?.retweet_count.ToString() ?? "0",
                        ["replies"] = tweet.public_metrics?.reply_count.ToString() ?? "0",
                        ["quotes"] = tweet.public_metrics?.quote_count.ToString() ?? "0"
                    }
                };

                if (options.Since.HasValue && post.CreatedAt < options.Since.Value)
                    continue;

                posts.Add(post);
            }

            _logger.LogInformation("Imported {Count} tweets from Twitter", posts.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error fetching Twitter tweets");
        }

        return posts;
    }

    public async Task<bool> RevokeAccessAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var requestBody = new Dictionary<string, string>
            {
                ["token"] = accessToken,
                ["client_id"] = _clientId
            };

            var content = new FormUrlEncodedContent(requestBody);

            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_clientId}:{_clientSecret}"));
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);

            var response = await _httpClient.PostAsync(RevokeUrl, content, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking Twitter access");
            return false;
        }
    }

    // Twitter API response classes
    private class TwitterTokenResponse
    {
        public string? access_token { get; set; }
        public string? token_type { get; set; }
        public int expires_in { get; set; }
        public string? refresh_token { get; set; }
        public string? scope { get; set; }
    }

    private class TwitterUserResponse
    {
        public TwitterUser? data { get; set; }
    }

    private class TwitterUser
    {
        public string? id { get; set; }
        public string? name { get; set; }
        public string? username { get; set; }
        public string? profile_image_url { get; set; }
        public string? description { get; set; }
    }

    private class TwitterTweetsResponse
    {
        public List<TwitterTweet>? data { get; set; }
        public TwitterMeta? meta { get; set; }
    }

    private class TwitterTweet
    {
        public string? id { get; set; }
        public string? text { get; set; }
        public string? created_at { get; set; }
        public TwitterPublicMetrics? public_metrics { get; set; }
    }

    private class TwitterPublicMetrics
    {
        public int retweet_count { get; set; }
        public int reply_count { get; set; }
        public int like_count { get; set; }
        public int quote_count { get; set; }
    }

    private class TwitterMeta
    {
        public int result_count { get; set; }
        public string? next_token { get; set; }
    }
}
