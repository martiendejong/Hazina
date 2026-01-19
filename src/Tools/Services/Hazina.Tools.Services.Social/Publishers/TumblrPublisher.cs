using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Hazina.Tools.Services.Social.Abstractions;

namespace Hazina.Tools.Services.Social.Publishers;

/// <summary>
/// Tumblr blog publisher implementation.
/// Publishes posts to Tumblr using the Tumblr API v2.
/// Supports text, photo, and link posts.
/// </summary>
public class TumblrPublisher : ISocialPublisher
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TumblrPublisher> _logger;

    private const string ApiBaseUrl = "https://api.tumblr.com/v2";

    public string ProviderId => "tumblr";
    public string DisplayName => "Tumblr";

    public TumblrPublisher(
        HttpClient httpClient,
        ILogger<TumblrPublisher> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<PublishResult> PublishPostAsync(
        string accessToken,
        PublishPostRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("[Tumblr] Publishing post: {PostId}", request.InternalPostId);

            // Get user's primary blog identifier
            var blogIdentifier = await GetPrimaryBlogIdentifierAsync(accessToken, cancellationToken);
            if (string.IsNullOrEmpty(blogIdentifier))
            {
                return new PublishResult
                {
                    Success = false,
                    Error = "Failed to get Tumblr blog identifier",
                    ErrorCode = "blog_identifier_fetch_failed"
                };
            }

            // Determine post type based on content and media
            var postType = DeterminePostType(request);

            // Build post payload
            var postPayload = BuildPostPayload(request, postType);
            var json = JsonSerializer.Serialize(postPayload);

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{ApiBaseUrl}/blog/{blogIdentifier}/posts");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[Tumblr] Publish failed: {Status} - {Response}",
                    response.StatusCode, responseBody);

                return new PublishResult
                {
                    Success = false,
                    Error = $"Tumblr API error: {response.StatusCode}",
                    ErrorCode = GetErrorCode(response.StatusCode.ToString()),
                    Metadata = new Dictionary<string, string>
                    {
                        ["status_code"] = ((int)response.StatusCode).ToString(),
                        ["response"] = responseBody
                    }
                };
            }

            var publishResponse = JsonSerializer.Deserialize<TumblrPostResponse>(responseBody);
            if (publishResponse?.response?.id == null)
            {
                return new PublishResult
                {
                    Success = false,
                    Error = "Invalid response from Tumblr",
                    ErrorCode = "invalid_response"
                };
            }

            var postId = publishResponse.response.id.ToString();
            var postUrl = $"https://{blogIdentifier}/post/{postId}";

            _logger.LogInformation("[Tumblr] Post published successfully: {PostId}", postId);

            return new PublishResult
            {
                Success = true,
                ExternalPostId = postId,
                Url = postUrl,
                PublishedAt = DateTime.UtcNow,
                Metadata = new Dictionary<string, string>
                {
                    ["post_id"] = postId,
                    ["blog_identifier"] = blogIdentifier,
                    ["post_type"] = postType
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Tumblr] Error publishing post");
            return new PublishResult
            {
                Success = false,
                Error = ex.Message,
                ErrorCode = "exception"
            };
        }
    }

    public async Task<bool> DeletePostAsync(
        string accessToken,
        string externalPostId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("[Tumblr] Deleting post: {PostId}", externalPostId);

            // Get user's primary blog identifier
            var blogIdentifier = await GetPrimaryBlogIdentifierAsync(accessToken, cancellationToken);
            if (string.IsNullOrEmpty(blogIdentifier))
            {
                _logger.LogWarning("[Tumblr] Failed to get blog identifier for deletion");
                return false;
            }

            var httpRequest = new HttpRequestMessage(HttpMethod.Delete, $"{ApiBaseUrl}/blog/{blogIdentifier}/posts/{externalPostId}");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("[Tumblr] Post deleted successfully: {PostId}", externalPostId);
                return true;
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("[Tumblr] Delete failed: {Status} - {Response}",
                response.StatusCode, responseBody);

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Tumblr] Error deleting post: {PostId}", externalPostId);
            return false;
        }
    }

    public async Task<PostMetrics> GetPostMetricsAsync(
        string accessToken,
        string externalPostId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("[Tumblr] Fetching metrics for post: {PostId}", externalPostId);

            // Get user's primary blog identifier
            var blogIdentifier = await GetPrimaryBlogIdentifierAsync(accessToken, cancellationToken);
            if (string.IsNullOrEmpty(blogIdentifier))
            {
                _logger.LogWarning("[Tumblr] Failed to get blog identifier for metrics");
                return new PostMetrics
                {
                    ExternalPostId = externalPostId,
                    FetchedAt = DateTime.UtcNow
                };
            }

            // Fetch post details
            var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"{ApiBaseUrl}/blog/{blogIdentifier}/posts/{externalPostId}");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[Tumblr] Metrics fetch failed: {Status} - {Response}",
                    response.StatusCode, responseBody);

                return new PostMetrics
                {
                    ExternalPostId = externalPostId,
                    FetchedAt = DateTime.UtcNow
                };
            }

            var postResponse = JsonSerializer.Deserialize<TumblrPostDetailsResponse>(responseBody);
            var post = postResponse?.response?.posts?.FirstOrDefault();

            if (post == null)
            {
                return new PostMetrics
                {
                    ExternalPostId = externalPostId,
                    FetchedAt = DateTime.UtcNow
                };
            }

            return new PostMetrics
            {
                ExternalPostId = externalPostId,
                Likes = post.note_count ?? 0, // Tumblr uses "notes" (likes + reblogs + replies)
                Comments = 0, // Tumblr doesn't separate these in basic API
                Shares = 0, // Reblogs are included in note_count
                Views = 0, // Not provided by Tumblr API
                FetchedAt = DateTime.UtcNow,
                Metadata = new Dictionary<string, string>
                {
                    ["note_count"] = (post.note_count ?? 0).ToString(),
                    ["reblog_key"] = post.reblog_key ?? "",
                    ["post_url"] = post.post_url ?? ""
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Tumblr] Error fetching metrics for post: {PostId}", externalPostId);
            return new PostMetrics
            {
                ExternalPostId = externalPostId,
                FetchedAt = DateTime.UtcNow
            };
        }
    }

    public async Task<bool> ValidateAccessAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("[Tumblr] Validating access token");

            var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiBaseUrl}/user/info");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("[Tumblr] Access token is valid");
                return true;
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("[Tumblr] Access validation failed: {Status} - {Response}",
                response.StatusCode, responseBody);

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Tumblr] Error validating access token");
            return false;
        }
    }

    private async Task<string> GetPrimaryBlogIdentifierAsync(string accessToken, CancellationToken cancellationToken)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiBaseUrl}/user/info");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[Tumblr] Failed to get user info: {Response}", json);
                return "";
            }

            var userResponse = JsonSerializer.Deserialize<TumblrUserInfoResponse>(json);
            var primaryBlog = userResponse?.response?.user?.blogs?.FirstOrDefault(b => b.primary == true);

            if (primaryBlog != null)
            {
                return primaryBlog.name ?? primaryBlog.uuid ?? "";
            }

            // Fallback to first blog if no primary
            var firstBlog = userResponse?.response?.user?.blogs?.FirstOrDefault();
            return firstBlog?.name ?? firstBlog?.uuid ?? "";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Tumblr] Error getting blog identifier");
            return "";
        }
    }

    private string DeterminePostType(PublishPostRequest request)
    {
        // Tumblr supports: text, photo, quote, link, chat, audio, video
        if (request.MediaUrls.Any())
        {
            var firstMedia = request.MediaUrls.First().ToLower();
            if (firstMedia.EndsWith(".jpg") || firstMedia.EndsWith(".png") ||
                firstMedia.EndsWith(".gif") || firstMedia.EndsWith(".jpeg"))
            {
                return "photo";
            }
            else if (firstMedia.StartsWith("http"))
            {
                return "link"; // External link
            }
        }

        return "text"; // Default to text post
    }

    private object BuildPostPayload(PublishPostRequest request, string postType)
    {
        var payload = new Dictionary<string, object>
        {
            ["type"] = postType,
            ["state"] = "published", // or "draft", "queue", "private"
        };

        // Add tags
        if (request.Hashtags.Any())
        {
            var tags = string.Join(",", request.Hashtags.Select(h => h.StartsWith("#") ? h.Substring(1) : h));
            payload["tags"] = tags;
        }

        // Content based on post type
        switch (postType)
        {
            case "text":
                payload["body"] = request.Content;
                // Extract title if available
                var title = ExtractTitle(request.Content);
                if (!string.IsNullOrEmpty(title))
                {
                    payload["title"] = title;
                }
                break;

            case "photo":
                payload["caption"] = request.Content;
                payload["source"] = request.MediaUrls.First(); // URL of the photo
                break;

            case "link":
                payload["url"] = request.MediaUrls.First();
                payload["description"] = request.Content;
                break;
        }

        return payload;
    }

    private string ExtractTitle(string content)
    {
        // Try to extract first line as title
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var firstLine = lines.FirstOrDefault() ?? "";

        if (firstLine.Length > 0 && firstLine.Length <= 100)
        {
            return firstLine;
        }

        return ""; // No title
    }

    private string GetErrorCode(string statusCode)
    {
        return statusCode switch
        {
            "401" => "invalid_token",
            "403" => "insufficient_permissions",
            "429" => "rate_limit",
            "400" => "invalid_request",
            "404" => "post_not_found",
            _ => "api_error"
        };
    }

    // Tumblr API response classes
    private class TumblrPostResponse
    {
        public TumblrPostData? response { get; set; }
    }

    private class TumblrPostData
    {
        public long? id { get; set; }
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
        public List<TumblrBlog>? blogs { get; set; }
    }

    private class TumblrBlog
    {
        public string? name { get; set; }
        public string? uuid { get; set; }
        public bool primary { get; set; }
    }

    private class TumblrPostDetailsResponse
    {
        public TumblrPostDetailsData? response { get; set; }
    }

    private class TumblrPostDetailsData
    {
        public List<TumblrPost>? posts { get; set; }
    }

    private class TumblrPost
    {
        public long? id { get; set; }
        public string? post_url { get; set; }
        public string? reblog_key { get; set; }
        public int? note_count { get; set; }
    }
}
