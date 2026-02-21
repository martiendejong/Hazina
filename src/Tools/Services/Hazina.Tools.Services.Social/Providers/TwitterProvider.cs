using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Web;
using Microsoft.Extensions.Logging;
using Hazina.Tools.Services.Social.Abstractions;

namespace Hazina.Tools.Services.Social.Providers;

/// <summary>
/// X (Twitter) social media provider implementation.
/// Supports OAuth 2.0 with PKCE authentication and content import.
/// Requires Twitter API v2 access.
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

    // Store code verifiers keyed by state to retrieve during token exchange.
    // This is a simple in-memory store; in production, use a distributed cache.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _codeVerifiers = new();

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
        // Twitter OAuth 2.0 requires PKCE with S256 method
        var codeVerifier = GenerateCodeVerifier();
        var codeChallenge = GenerateCodeChallenge(codeVerifier);

        // Store the verifier so we can retrieve it during token exchange
        _codeVerifiers[state] = codeVerifier;

        // Clean up old entries (keep max 100)
        if (_codeVerifiers.Count > 100)
        {
            var oldest = _codeVerifiers.Keys.Take(50).ToList();
            foreach (var key in oldest)
            {
                _codeVerifiers.TryRemove(key, out _);
            }
        }

        var scopes = "tweet.read tweet.write users.read offline.access";

        return $"{AuthorizeUrl}"
            + $"?response_type=code"
            + $"&client_id={HttpUtility.UrlEncode(_clientId)}"
            + $"&redirect_uri={HttpUtility.UrlEncode(redirectUri)}"
            + $"&scope={HttpUtility.UrlEncode(scopes)}"
            + $"&state={HttpUtility.UrlEncode(state)}"
            + $"&code_challenge={HttpUtility.UrlEncode(codeChallenge)}"
            + $"&code_challenge_method=S256";
    }

    public async Task<SocialAuthResult> ExchangeCodeAsync(
        string code,
        string redirectUri,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Try to retrieve the code verifier from stored state
            // The state parameter should be passed back — look it up
            string codeVerifier = "challenge"; // fallback
            foreach (var kvp in _codeVerifiers)
            {
                // Try each stored verifier — remove on use
                if (_codeVerifiers.TryRemove(kvp.Key, out var storedVerifier))
                {
                    codeVerifier = storedVerifier;
                    break; // Use the most recently stored one
                }
            }

            _logger.LogInformation("[Twitter] Exchanging code for token, verifier length: {Length}", codeVerifier.Length);

            var requestBody = new Dictionary<string, string>
            {
                ["code"] = code,
                ["grant_type"] = "authorization_code",
                ["client_id"] = _clientId,
                ["redirect_uri"] = redirectUri,
                ["code_verifier"] = codeVerifier
            };

            var content = new FormUrlEncodedContent(requestBody);

            // Twitter requires Basic Auth with client credentials
            using var request = new HttpRequestMessage(HttpMethod.Post, TokenUrl);
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_clientId}:{_clientSecret}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            request.Content = content;

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[Twitter] Token exchange failed: {Status} - {Response}",
                    response.StatusCode, json);
                return new SocialAuthResult
                {
                    Success = false,
                    Error = $"Token exchange failed: {response.StatusCode} - {json}"
                };
            }

            var tokenResponse = JsonSerializer.Deserialize<TwitterTokenResponse>(json);
            if (tokenResponse?.access_token == null)
            {
                return new SocialAuthResult { Success = false, Error = "Invalid token response" };
            }

            _logger.LogInformation("[Twitter] Token exchange successful");

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
            _logger.LogError(ex, "[Twitter] Error exchanging auth code");
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

            using var request = new HttpRequestMessage(HttpMethod.Post, TokenUrl);
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_clientId}:{_clientSecret}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            request.Content = content;

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[Twitter] Token refresh failed: {Status} - {Response}",
                    response.StatusCode, json);
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
            _logger.LogError(ex, "[Twitter] Error refreshing token");
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

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[Twitter] Profile fetch failed: {Status} - {Response}",
                    response.StatusCode, json);
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
                ProfileUrl = user.username != null ? $"https://x.com/{user.username}" : "",
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
            _logger.LogError(ex, "[Twitter] Error getting profile");
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
            _logger.LogInformation("[Twitter] Imported {Count} items", result.TotalImported);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Twitter] Error importing content");
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
            var url = $"{ApiBaseUrl}/users/{userId}/tweets?tweet.fields=created_at,public_metrics,entities&max_results={Math.Min(options.MaxItems, 100)}";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[Twitter] Tweets fetch failed: {Status} - {Response}",
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
                    Url = $"https://x.com/{username}/status/{tweet.id}",
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

            _logger.LogInformation("[Twitter] Imported {Count} tweets", posts.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Twitter] Error fetching tweets");
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

            using var request = new HttpRequestMessage(HttpMethod.Post, RevokeUrl);
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_clientId}:{_clientSecret}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            request.Content = content;

            var response = await _httpClient.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Twitter] Error revoking access");
            return false;
        }
    }

    public Task<List<SocialComment>> FetchCommentsAsync(
        string accessToken,
        string contentId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new List<SocialComment>());
    }

    public Task<SocialEngagement> FetchEngagementAsync(
        string accessToken,
        string contentId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new SocialEngagement());
    }

    // PKCE helpers
    private static string GenerateCodeVerifier()
    {
        // Generate a random 43-128 character string using unreserved characters
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string GenerateCodeChallenge(string codeVerifier)
    {
        // S256: BASE64URL(SHA256(code_verifier))
        using var sha256 = SHA256.Create();
        var challengeBytes = sha256.ComputeHash(Encoding.ASCII.GetBytes(codeVerifier));
        return Convert.ToBase64String(challengeBytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
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
