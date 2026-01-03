using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Web;
using Microsoft.Extensions.Logging;
using Hazina.Tools.Services.Social.Abstractions;

namespace Hazina.Tools.Services.Social.Providers;

/// <summary>
/// Reddit social media provider implementation.
/// Supports OAuth 2.0 authentication and content import.
/// Requires Reddit App with API access.
/// </summary>
public class RedditProvider : ISocialProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RedditProvider> _logger;
    private readonly string _clientId;
    private readonly string _clientSecret;

    private const string AuthorizeUrl = "https://www.reddit.com/api/v1/authorize";
    private const string TokenUrl = "https://www.reddit.com/api/v1/access_token";
    private const string RevokeUrl = "https://www.reddit.com/api/v1/revoke_token";
    private const string ApiBaseUrl = "https://oauth.reddit.com";

    public string ProviderId => "reddit";
    public string DisplayName => "Reddit";

    public RedditProvider(
        HttpClient httpClient,
        ILogger<RedditProvider> logger,
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
        var scopes = "identity read history mysubreddits";
        var encodedRedirect = HttpUtility.UrlEncode(redirectUri);

        return $"{AuthorizeUrl}?client_id={_clientId}&response_type=code&state={state}&redirect_uri={encodedRedirect}&duration=permanent&scope={scopes}";
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
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = redirectUri
            });

            // Reddit requires Basic Auth with client credentials
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_clientId}:{_clientSecret}"));
            var request = new HttpRequestMessage(HttpMethod.Post, TokenUrl) { Content = content };
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            request.Headers.UserAgent.ParseAdd("Brand2Boost/1.0");

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Reddit token exchange failed: {Response}", json);
                return new SocialAuthResult
                {
                    Success = false,
                    Error = $"Token exchange failed: {response.StatusCode}"
                };
            }

            var tokenResponse = JsonSerializer.Deserialize<RedditTokenResponse>(json);
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
            _logger.LogError(ex, "Error exchanging Reddit auth code");
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
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken
            });

            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_clientId}:{_clientSecret}"));
            var request = new HttpRequestMessage(HttpMethod.Post, TokenUrl) { Content = content };
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            request.Headers.UserAgent.ParseAdd("Brand2Boost/1.0");

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

            var tokenResponse = JsonSerializer.Deserialize<RedditTokenResponse>(json);
            if (tokenResponse?.access_token == null)
            {
                return new SocialAuthResult { Success = false, Error = "Invalid token response" };
            }

            return new SocialAuthResult
            {
                Success = true,
                AccessToken = tokenResponse.access_token,
                RefreshToken = refreshToken, // Reddit doesn't return new refresh token
                ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.expires_in)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing Reddit token");
            return new SocialAuthResult { Success = false, Error = ex.Message };
        }
    }

    public async Task<SocialProfile> GetProfileAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{ApiBaseUrl}/api/v1/me";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.UserAgent.ParseAdd("Brand2Boost/1.0");

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Reddit profile fetch failed: {Response}", json);
                return new SocialProfile { Id = "", Name = "Unknown" };
            }

            var userResponse = JsonSerializer.Deserialize<RedditUserResponse>(json);
            if (userResponse == null)
            {
                return new SocialProfile { Id = "", Name = "Unknown" };
            }

            return new SocialProfile
            {
                Id = userResponse.id ?? "",
                Name = userResponse.name ?? "Unknown",
                ProfileUrl = $"https://www.reddit.com/user/{userResponse.name}",
                AvatarUrl = userResponse.icon_img,
                Metadata = new Dictionary<string, string>
                {
                    ["link_karma"] = userResponse.link_karma?.ToString() ?? "0",
                    ["comment_karma"] = userResponse.comment_karma?.ToString() ?? "0",
                    ["total_karma"] = userResponse.total_karma?.ToString() ?? "0",
                    ["created_utc"] = userResponse.created_utc?.ToString() ?? "0"
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Reddit profile");
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
            var username = profile.Name;

            if (options.ContentTypes.Contains("posts"))
            {
                var submissions = await ImportSubmissionsAsync(accessToken, username, options, cancellationToken);
                result.Posts.AddRange(submissions);
            }

            result.TotalImported = result.Posts.Count + result.Articles.Count;
            _logger.LogInformation("Imported {Count} items from Reddit user {Username}", result.TotalImported, username);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing Reddit content");
            result.Success = false;
            result.Error = ex.Message;
        }

        return result;
    }

    private async Task<List<SocialPost>> ImportSubmissionsAsync(
        string accessToken,
        string username,
        SocialImportOptions options,
        CancellationToken cancellationToken)
    {
        var posts = new List<SocialPost>();

        try
        {
            var url = $"{ApiBaseUrl}/user/{username}/submitted?limit={Math.Min(options.MaxItems, 100)}";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.UserAgent.ParseAdd("Brand2Boost/1.0");

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Reddit submissions fetch failed: {Status} - {Response}",
                    response.StatusCode, json);
                return posts;
            }

            var listingResponse = JsonSerializer.Deserialize<RedditListingResponse>(json);
            if (listingResponse?.data?.children == null)
            {
                return posts;
            }

            foreach (var child in listingResponse.data.children)
            {
                var submission = child.data;
                if (submission == null)
                    continue;

                var createdAt = DateTimeOffset.FromUnixTimeSeconds((long)(submission.created_utc ?? 0)).UtcDateTime;
                if (options.Since.HasValue && createdAt < options.Since.Value)
                    continue;

                var post = new SocialPost
                {
                    Id = submission.id ?? "",
                    AccountId = username,
                    Content = submission.selftext ?? submission.title ?? "",
                    CreatedAt = createdAt,
                    Url = $"https://www.reddit.com{submission.permalink}",
                    Metadata = new Dictionary<string, string>
                    {
                        ["source"] = $"r/{submission.subreddit}",
                        ["title"] = submission.title ?? "",
                        ["subreddit"] = submission.subreddit ?? "",
                        ["post_type"] = submission.post_hint ?? "text",
                        ["upvotes"] = submission.ups?.ToString() ?? "0",
                        ["downvotes"] = submission.downs?.ToString() ?? "0",
                        ["score"] = submission.score?.ToString() ?? "0",
                        ["comments"] = submission.num_comments?.ToString() ?? "0",
                        ["url"] = submission.url ?? "",
                        ["is_self"] = submission.is_self.ToString()
                    }
                };

                posts.Add(post);
            }

            _logger.LogInformation("Imported {Count} Reddit submissions from u/{Username}",
                posts.Count, username);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error fetching Reddit submissions for u/{Username}", username);
        }

        return posts;
    }

    public async Task<bool> RevokeAccessAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["token"] = accessToken,
                ["token_type_hint"] = "access_token"
            });

            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_clientId}:{_clientSecret}"));
            var request = new HttpRequestMessage(HttpMethod.Post, RevokeUrl) { Content = content };
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            request.Headers.UserAgent.ParseAdd("Brand2Boost/1.0");

            var response = await _httpClient.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking Reddit access");
            return false;
        }
    }

    // Reddit API response classes
    private class RedditTokenResponse
    {
        public string? access_token { get; set; }
        public string? token_type { get; set; }
        public int expires_in { get; set; }
        public string? refresh_token { get; set; }
        public string? scope { get; set; }
    }

    private class RedditUserResponse
    {
        public string? id { get; set; }
        public string? name { get; set; }
        public string? icon_img { get; set; }
        public int? link_karma { get; set; }
        public int? comment_karma { get; set; }
        public int? total_karma { get; set; }
        public double? created_utc { get; set; }
    }

    private class RedditListingResponse
    {
        public RedditListingData? data { get; set; }
    }

    private class RedditListingData
    {
        public List<RedditChild>? children { get; set; }
        public string? after { get; set; }
        public string? before { get; set; }
    }

    private class RedditChild
    {
        public RedditSubmission? data { get; set; }
    }

    private class RedditSubmission
    {
        public string? id { get; set; }
        public string? name { get; set; }
        public string? title { get; set; }
        public string? selftext { get; set; }
        public string? author { get; set; }
        public string? subreddit { get; set; }
        public string? permalink { get; set; }
        public string? url { get; set; }
        public double? created_utc { get; set; }
        public int? score { get; set; }
        public int? ups { get; set; }
        public int? downs { get; set; }
        public int? num_comments { get; set; }
        public bool is_self { get; set; }
        public string? post_hint { get; set; }
        public string? thumbnail { get; set; }
    }
}
