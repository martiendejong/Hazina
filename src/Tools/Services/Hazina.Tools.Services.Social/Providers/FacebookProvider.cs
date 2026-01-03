using System.Net.Http.Headers;
using System.Text.Json;
using System.Web;
using Microsoft.Extensions.Logging;
using Hazina.Tools.Services.Social.Abstractions;

namespace Hazina.Tools.Services.Social.Providers;

/// <summary>
/// Facebook social media provider implementation.
/// Supports OAuth 2.0 authentication and content import from Facebook Pages.
/// </summary>
public class FacebookProvider : ISocialProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FacebookProvider> _logger;
    private readonly string _appId;
    private readonly string _appSecret;

    private const string AuthorizeUrl = "https://www.facebook.com/v18.0/dialog/oauth";
    private const string TokenUrl = "https://graph.facebook.com/v18.0/oauth/access_token";
    private const string ApiBaseUrl = "https://graph.facebook.com/v18.0";

    public string ProviderId => "facebook";
    public string DisplayName => "Facebook";

    public FacebookProvider(
        HttpClient httpClient,
        ILogger<FacebookProvider> logger,
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
        // Request permissions for reading pages and posts
        var scopes = "pages_show_list,pages_read_engagement,pages_read_user_content,public_profile,email";
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
                _logger.LogWarning("Facebook token exchange failed: {Response}", json);
                return new SocialAuthResult
                {
                    Success = false,
                    Error = $"Token exchange failed: {response.StatusCode}"
                };
            }

            var tokenResponse = JsonSerializer.Deserialize<FacebookTokenResponse>(json);
            if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.access_token))
            {
                return new SocialAuthResult { Success = false, Error = "Invalid token response" };
            }

            // Facebook short-lived tokens expire in ~2 hours
            // Long-lived tokens expire in ~60 days (can be obtained via token exchange)
            var expiresIn = tokenResponse.expires_in > 0 ? tokenResponse.expires_in : 7200; // Default 2 hours

            return new SocialAuthResult
            {
                Success = true,
                AccessToken = tokenResponse.access_token,
                ExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exchanging Facebook auth code");
            return new SocialAuthResult { Success = false, Error = ex.Message };
        }
    }

    public async Task<SocialAuthResult> RefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        // Facebook doesn't use traditional refresh tokens
        // Instead, you can exchange short-lived token for long-lived token
        try
        {
            var url = $"{TokenUrl}?grant_type=fb_exchange_token&client_id={_appId}&client_secret={_appSecret}&fb_exchange_token={refreshToken}";

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

            var tokenResponse = JsonSerializer.Deserialize<FacebookTokenResponse>(json);
            if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.access_token))
            {
                return new SocialAuthResult { Success = false, Error = "Invalid token response" };
            }

            var expiresIn = tokenResponse.expires_in > 0 ? tokenResponse.expires_in : 5184000; // Default 60 days

            return new SocialAuthResult
            {
                Success = true,
                AccessToken = tokenResponse.access_token,
                ExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing Facebook token");
            return new SocialAuthResult { Success = false, Error = ex.Message };
        }
    }

    public async Task<SocialProfile> GetProfileAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{ApiBaseUrl}/me?fields=id,name,email,picture&access_token={accessToken}";

            var response = await _httpClient.GetAsync(url, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Facebook profile fetch failed: {Response}", json);
                return new SocialProfile { Id = "", Name = "Unknown" };
            }

            var profile = JsonSerializer.Deserialize<FacebookUserProfile>(json);
            if (profile == null)
            {
                return new SocialProfile { Id = "", Name = "Unknown" };
            }

            return new SocialProfile
            {
                Id = profile.id ?? "",
                Name = profile.name ?? "",
                Email = profile.email,
                ProfileUrl = $"https://www.facebook.com/{profile.id}",
                AvatarUrl = profile.picture?.data?.url,
                Metadata = new Dictionary<string, string>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Facebook profile");
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
            // First, get the user's pages
            var pages = await GetUserPagesAsync(accessToken, cancellationToken);

            if (!pages.Any())
            {
                _logger.LogWarning("No Facebook pages found for user");
                return result; // Empty result but still success
            }

            // Import posts from each page
            foreach (var page in pages.Take(5)) // Limit to 5 pages to avoid rate limits
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var posts = await ImportPagePostsAsync(
                    page.access_token ?? accessToken,
                    page.id,
                    page.name,
                    options,
                    cancellationToken);

                result.Posts.AddRange(posts);
            }

            result.TotalImported = result.Posts.Count + result.Articles.Count;
            _logger.LogInformation("Imported {Count} items from {PageCount} Facebook pages",
                result.TotalImported, pages.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing Facebook content");
            result.Success = false;
            result.Error = ex.Message;
        }

        return result;
    }

    private async Task<List<FacebookPage>> GetUserPagesAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        try
        {
            var url = $"{ApiBaseUrl}/me/accounts?access_token={accessToken}";

            var response = await _httpClient.GetAsync(url, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Facebook pages fetch failed: {Response}", json);
                return new List<FacebookPage>();
            }

            var pagesResponse = JsonSerializer.Deserialize<FacebookPagesResponse>(json);
            return pagesResponse?.data ?? new List<FacebookPage>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Facebook pages");
            return new List<FacebookPage>();
        }
    }

    private async Task<List<SocialPost>> ImportPagePostsAsync(
        string pageAccessToken,
        string? pageId,
        string? pageName,
        SocialImportOptions options,
        CancellationToken cancellationToken)
    {
        var posts = new List<SocialPost>();

        try
        {
            var limit = Math.Min(options.MaxItems, 100);
            var url = $"{ApiBaseUrl}/{pageId}/posts?fields=id,message,created_time,permalink_url,reactions.summary(true),comments.summary(true),shares&limit={limit}&access_token={pageAccessToken}";

            var response = await _httpClient.GetAsync(url, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Facebook posts fetch failed for page {PageId}: {Response}",
                    pageId, json);
                return posts;
            }

            var postsResponse = JsonSerializer.Deserialize<FacebookPostsResponse>(json);
            if (postsResponse?.data == null)
            {
                return posts;
            }

            foreach (var fbPost in postsResponse.data)
            {
                var post = new SocialPost
                {
                    Id = fbPost.id ?? "",
                    AccountId = pageId ?? "",
                    Content = fbPost.message ?? "",
                    CreatedAt = DateTime.TryParse(fbPost.created_time, out var createdAt)
                        ? createdAt.ToUniversalTime()
                        : DateTime.UtcNow,
                    Url = fbPost.permalink_url,
                    LikeCount = fbPost.reactions?.summary?.total_count ?? 0,
                    CommentCount = fbPost.comments?.summary?.total_count ?? 0,
                    ShareCount = fbPost.shares?.count ?? 0,
                    Metadata = new Dictionary<string, string>
                    {
                        ["page_name"] = pageName ?? "",
                        ["page_id"] = pageId ?? ""
                    }
                };

                // Apply date filter
                if (options.Since.HasValue && post.CreatedAt < options.Since.Value)
                    continue;

                // Import comments if requested
                if (options.IncludeEngagement && fbPost.comments?.data != null)
                {
                    foreach (var fbComment in fbPost.comments.data.Take(10)) // Limit to 10 comments per post
                    {
                        post.Comments.Add(new SocialComment
                        {
                            Id = fbComment.id ?? "",
                            AuthorName = fbComment.from?.name ?? "Unknown",
                            AuthorId = fbComment.from?.id,
                            Content = fbComment.message ?? "",
                            CreatedAt = DateTime.TryParse(fbComment.created_time, out var commentDate)
                                ? commentDate.ToUniversalTime()
                                : DateTime.UtcNow
                        });
                    }
                }

                posts.Add(post);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error fetching posts for Facebook page {PageId}", pageId);
        }

        return posts;
    }

    public async Task<bool> RevokeAccessAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{ApiBaseUrl}/me/permissions?access_token={accessToken}";

            var response = await _httpClient.DeleteAsync(url, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking Facebook access");
            return false;
        }
    }

    // Facebook API response classes
    private class FacebookTokenResponse
    {
        public string? access_token { get; set; }
        public string? token_type { get; set; }
        public int expires_in { get; set; }
    }

    private class FacebookUserProfile
    {
        public string? id { get; set; }
        public string? name { get; set; }
        public string? email { get; set; }
        public FacebookPicture? picture { get; set; }
    }

    private class FacebookPicture
    {
        public FacebookPictureData? data { get; set; }
    }

    private class FacebookPictureData
    {
        public string? url { get; set; }
    }

    private class FacebookPagesResponse
    {
        public List<FacebookPage>? data { get; set; }
    }

    private class FacebookPage
    {
        public string? id { get; set; }
        public string? name { get; set; }
        public string? access_token { get; set; }
    }

    private class FacebookPostsResponse
    {
        public List<FacebookPost>? data { get; set; }
        public FacebookPaging? paging { get; set; }
    }

    private class FacebookPost
    {
        public string? id { get; set; }
        public string? message { get; set; }
        public string? created_time { get; set; }
        public string? permalink_url { get; set; }
        public FacebookReactions? reactions { get; set; }
        public FacebookComments? comments { get; set; }
        public FacebookShares? shares { get; set; }
    }

    private class FacebookReactions
    {
        public FacebookSummary? summary { get; set; }
    }

    private class FacebookComments
    {
        public List<FacebookComment>? data { get; set; }
        public FacebookSummary? summary { get; set; }
    }

    private class FacebookComment
    {
        public string? id { get; set; }
        public string? message { get; set; }
        public string? created_time { get; set; }
        public FacebookFrom? from { get; set; }
    }

    private class FacebookFrom
    {
        public string? id { get; set; }
        public string? name { get; set; }
    }

    private class FacebookSummary
    {
        public int total_count { get; set; }
    }

    private class FacebookShares
    {
        public int count { get; set; }
    }

    private class FacebookPaging
    {
        public string? next { get; set; }
        public string? previous { get; set; }
    }
}
