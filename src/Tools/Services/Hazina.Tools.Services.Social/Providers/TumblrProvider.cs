using System.Net.Http.Headers;
using System.Text.Json;
using System.Web;
using Microsoft.Extensions.Logging;
using Hazina.Tools.Services.Social.Abstractions;

namespace Hazina.Tools.Services.Social.Providers;

/// <summary>
/// Tumblr social media provider implementation.
/// Supports OAuth 2.0 authentication and content import.
/// Requires Tumblr App registration.
/// </summary>
public class TumblrProvider : ISocialProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TumblrProvider> _logger;
    private readonly string _clientId;
    private readonly string _clientSecret;

    private const string AuthorizeUrl = "https://www.tumblr.com/oauth2/authorize";
    private const string TokenUrl = "https://api.tumblr.com/v2/oauth2/token";
    private const string ApiBaseUrl = "https://api.tumblr.com/v2";

    public string ProviderId => "tumblr";
    public string DisplayName => "Tumblr";

    public TumblrProvider(
        HttpClient httpClient,
        ILogger<TumblrProvider> logger,
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
        var scopes = "basic write";
        var encodedRedirect = HttpUtility.UrlEncode(redirectUri);

        return $"{AuthorizeUrl}?client_id={_clientId}&response_type=code&scope={scopes}&state={state}&redirect_uri={encodedRedirect}";
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
                ["client_id"] = _clientId,
                ["client_secret"] = _clientSecret,
                ["redirect_uri"] = redirectUri
            });

            var response = await _httpClient.PostAsync(TokenUrl, content, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Tumblr token exchange failed: {Response}", json);
                return new SocialAuthResult
                {
                    Success = false,
                    Error = $"Token exchange failed: {response.StatusCode}"
                };
            }

            var tokenResponse = JsonSerializer.Deserialize<TumblrTokenResponse>(json);
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
            _logger.LogError(ex, "Error exchanging Tumblr auth code");
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
                ["refresh_token"] = refreshToken,
                ["client_id"] = _clientId,
                ["client_secret"] = _clientSecret
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

            var tokenResponse = JsonSerializer.Deserialize<TumblrTokenResponse>(json);
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
            _logger.LogError(ex, "Error refreshing Tumblr token");
            return new SocialAuthResult { Success = false, Error = ex.Message };
        }
    }

    public async Task<SocialProfile> GetProfileAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{ApiBaseUrl}/user/info";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Tumblr profile fetch failed: {Response}", json);
                return new SocialProfile { Id = "", Name = "Unknown" };
            }

            var userResponse = JsonSerializer.Deserialize<TumblrUserInfoResponse>(json);
            if (userResponse?.response?.user == null)
            {
                return new SocialProfile { Id = "", Name = "Unknown" };
            }

            var user = userResponse.response.user;
            var primaryBlog = user.blogs?.FirstOrDefault(b => b.primary == true) ?? user.blogs?.FirstOrDefault();

            return new SocialProfile
            {
                Id = user.name ?? "",
                Name = primaryBlog?.title ?? user.name ?? "Unknown",
                ProfileUrl = primaryBlog?.url ?? $"https://{user.name}.tumblr.com",
                Metadata = new Dictionary<string, string>
                {
                    ["name"] = user.name ?? "",
                    ["blogs_count"] = user.blogs?.Count.ToString() ?? "0",
                    ["following"] = user.following?.ToString() ?? "0"
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Tumblr profile");
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

            // Get user's blogs first
            var blogs = await GetUserBlogsAsync(accessToken, cancellationToken);

            if (options.ContentTypes.Contains("posts"))
            {
                foreach (var blog in blogs.Take(5)) // Limit to 5 blogs
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    var posts = await ImportBlogPostsAsync(
                        accessToken,
                        blog.name ?? "",
                        blog.title ?? blog.name ?? "",
                        options,
                        cancellationToken);
                    result.Posts.AddRange(posts);

                    if (result.Posts.Count >= options.MaxItems)
                        break;
                }
            }

            result.TotalImported = result.Posts.Count + result.Articles.Count;
            _logger.LogInformation("Imported {Count} items from {BlogCount} Tumblr blogs",
                result.TotalImported, blogs.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing Tumblr content");
            result.Success = false;
            result.Error = ex.Message;
        }

        return result;
    }

    private async Task<List<TumblrBlog>> GetUserBlogsAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        try
        {
            var url = $"{ApiBaseUrl}/user/info";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new List<TumblrBlog>();
            }

            var userResponse = JsonSerializer.Deserialize<TumblrUserInfoResponse>(json);
            return userResponse?.response?.user?.blogs ?? new List<TumblrBlog>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error fetching Tumblr blogs");
            return new List<TumblrBlog>();
        }
    }

    private async Task<List<SocialPost>> ImportBlogPostsAsync(
        string accessToken,
        string blogIdentifier,
        string blogTitle,
        SocialImportOptions options,
        CancellationToken cancellationToken)
    {
        var posts = new List<SocialPost>();

        try
        {
            var url = $"{ApiBaseUrl}/blog/{blogIdentifier}/posts?limit={Math.Min(options.MaxItems, 20)}";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Tumblr posts fetch failed for blog {Blog}: {Status} - {Response}",
                    blogTitle, response.StatusCode, json);
                return posts;
            }

            var postsResponse = JsonSerializer.Deserialize<TumblrPostsResponse>(json);
            if (postsResponse?.response?.posts == null)
            {
                return posts;
            }

            foreach (var tumblrPost in postsResponse.response.posts)
            {
                var createdAt = DateTimeOffset.FromUnixTimeSeconds(tumblrPost.timestamp).UtcDateTime;
                if (options.Since.HasValue && createdAt < options.Since.Value)
                    continue;

                var post = new SocialPost
                {
                    Id = tumblrPost.id?.ToString() ?? "",
                    AccountId = blogIdentifier,
                    Content = tumblrPost.summary ?? tumblrPost.caption ?? "",
                    CreatedAt = createdAt,
                    Url = tumblrPost.post_url ?? $"https://{blogIdentifier}.tumblr.com/post/{tumblrPost.id}",
                    Metadata = new Dictionary<string, string>
                    {
                        ["source"] = blogTitle,
                        ["blog_name"] = tumblrPost.blog_name ?? "",
                        ["type"] = tumblrPost.type ?? "text",
                        ["notes"] = tumblrPost.note_count?.ToString() ?? "0",
                        ["tags"] = string.Join(", ", tumblrPost.tags ?? new List<string>())
                    }
                };

                posts.Add(post);
            }

            _logger.LogInformation("Imported {Count} posts from Tumblr blog {Blog}",
                posts.Count, blogTitle);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error fetching Tumblr posts for blog {Blog}", blogTitle);
        }

        return posts;
    }

    public async Task<bool> RevokeAccessAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        // Tumblr doesn't have a documented revoke endpoint
        await Task.CompletedTask;
        return true;
    }

    // Tumblr API response classes
    private class TumblrTokenResponse
    {
        public string? access_token { get; set; }
        public string? refresh_token { get; set; }
        public int expires_in { get; set; }
        public string? token_type { get; set; }
        public string? scope { get; set; }
    }

    private class TumblrUserInfoResponse
    {
        public TumblrUserInfoData? response { get; set; }
    }

    private class TumblrUserInfoData
    {
        public TumblrUser? user { get; set; }
    }

    private class TumblrUser
    {
        public string? name { get; set; }
        public int? following { get; set; }
        public int? default_post_format { get; set; }
        public List<TumblrBlog>? blogs { get; set; }
    }

    private class TumblrBlog
    {
        public string? name { get; set; }
        public string? title { get; set; }
        public string? description { get; set; }
        public string? url { get; set; }
        public bool primary { get; set; }
        public int? posts { get; set; }
        public int? followers { get; set; }
    }

    private class TumblrPostsResponse
    {
        public TumblrPostsData? response { get; set; }
    }

    private class TumblrPostsData
    {
        public List<TumblrPost>? posts { get; set; }
        public int? total_posts { get; set; }
    }

    private class TumblrPost
    {
        public long? id { get; set; }
        public string? blog_name { get; set; }
        public string? post_url { get; set; }
        public string? type { get; set; }
        public long timestamp { get; set; }
        public string? summary { get; set; }
        public string? caption { get; set; }
        public List<string>? tags { get; set; }
        public int? note_count { get; set; }
    }
}
