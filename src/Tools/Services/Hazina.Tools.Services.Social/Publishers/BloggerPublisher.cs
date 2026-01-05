using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Hazina.Tools.Services.Social.Abstractions;

namespace Hazina.Tools.Services.Social.Publishers;

/// <summary>
/// Blogger (Blogspot) publisher implementation.
/// Publishes posts to Blogger blogs using the Blogger API v3.
/// Requires Google OAuth 2.0 with blogger scope.
/// </summary>
public class BloggerPublisher : ISocialPublisher
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BloggerPublisher> _logger;

    private const string ApiBaseUrl = "https://www.googleapis.com/blogger/v3";

    public string ProviderId => "blogger";
    public string DisplayName => "Blogger";

    public BloggerPublisher(
        HttpClient httpClient,
        ILogger<BloggerPublisher> logger)
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
            _logger.LogInformation("[Blogger] Publishing post: {PostId}", request.InternalPostId);

            // Get first blog ID (users can have multiple blogs)
            var blogId = await GetBlogIdAsync(accessToken, cancellationToken);
            if (string.IsNullOrEmpty(blogId))
            {
                return new PublishResult
                {
                    Success = false,
                    Error = "Failed to get Blogger blog ID",
                    ErrorCode = "blog_id_fetch_failed"
                };
            }

            // Build post content
            var title = ExtractTitle(request.Content);
            var content = FormatAsHtml(request.Content);

            // Add hashtags as labels (tags)
            var labels = request.Hashtags
                .Select(h => h.StartsWith("#") ? h.Substring(1) : h)
                .ToList();

            var post = new
            {
                kind = "blogger#post",
                title = title,
                content = content,
                labels = labels
            };

            var json = JsonSerializer.Serialize(post);
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{ApiBaseUrl}/blogs/{blogId}/posts");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[Blogger] Publish failed: {Status} - {Response}",
                    response.StatusCode, responseBody);

                return new PublishResult
                {
                    Success = false,
                    Error = $"Blogger API error: {response.StatusCode}",
                    ErrorCode = GetErrorCode(response.StatusCode.ToString()),
                    Metadata = new Dictionary<string, string>
                    {
                        ["status_code"] = ((int)response.StatusCode).ToString(),
                        ["response"] = responseBody
                    }
                };
            }

            var publishResponse = JsonSerializer.Deserialize<BloggerPostResponse>(responseBody);
            if (publishResponse?.id == null)
            {
                return new PublishResult
                {
                    Success = false,
                    Error = "Invalid response from Blogger",
                    ErrorCode = "invalid_response"
                };
            }

            var postId = publishResponse.id;
            var postUrl = publishResponse.url ?? "";

            _logger.LogInformation("[Blogger] Post published successfully: {PostId}", postId);

            return new PublishResult
            {
                Success = true,
                ExternalPostId = postId,
                Url = postUrl,
                PublishedAt = DateTime.Parse(publishResponse.published ?? DateTime.UtcNow.ToString("o")),
                Metadata = new Dictionary<string, string>
                {
                    ["post_id"] = postId,
                    ["blog_id"] = blogId,
                    ["self_link"] = publishResponse.selfLink ?? ""
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Blogger] Error publishing post");
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
            _logger.LogInformation("[Blogger] Deleting post: {PostId}", externalPostId);

            var blogId = await GetBlogIdAsync(accessToken, cancellationToken);
            if (string.IsNullOrEmpty(blogId))
            {
                return false;
            }

            var httpRequest = new HttpRequestMessage(HttpMethod.Delete, $"{ApiBaseUrl}/blogs/{blogId}/posts/{externalPostId}");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("[Blogger] Post deleted successfully: {PostId}", externalPostId);
                return true;
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("[Blogger] Delete failed: {Status} - {Response}",
                response.StatusCode, responseBody);

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Blogger] Error deleting post: {PostId}", externalPostId);
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
            _logger.LogInformation("[Blogger] Fetching metrics for post: {PostId}", externalPostId);

            var blogId = await GetBlogIdAsync(accessToken, cancellationToken);
            if (string.IsNullOrEmpty(blogId))
            {
                return new PostMetrics
                {
                    ExternalPostId = externalPostId,
                    FetchedAt = DateTime.UtcNow
                };
            }

            // Get post with view count
            var url = $"{ApiBaseUrl}/blogs/{blogId}/posts/{externalPostId}?view=READER";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[Blogger] Metrics fetch failed: {Status} - {Response}",
                    response.StatusCode, responseBody);

                return new PostMetrics
                {
                    ExternalPostId = externalPostId,
                    FetchedAt = DateTime.UtcNow
                };
            }

            var postResponse = JsonSerializer.Deserialize<BloggerPostResponse>(responseBody);

            // Blogger doesn't provide detailed metrics via API
            // View count may be available in Google Analytics
            return new PostMetrics
            {
                ExternalPostId = externalPostId,
                Likes = 0, // Blogger doesn't expose likes via API
                Comments = postResponse?.replies?.totalItems ?? 0,
                Shares = 0, // Not available
                Views = 0, // Would need Google Analytics integration
                FetchedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Blogger] Error fetching metrics for post: {PostId}", externalPostId);
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
            _logger.LogInformation("[Blogger] Validating access token");

            var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiBaseUrl}/users/self/blogs");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("[Blogger] Access token is valid");
                return true;
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("[Blogger] Access validation failed: {Status} - {Response}",
                response.StatusCode, responseBody);

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Blogger] Error validating access token");
            return false;
        }
    }

    private async Task<string> GetBlogIdAsync(string accessToken, CancellationToken cancellationToken)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiBaseUrl}/users/self/blogs");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[Blogger] Failed to get blogs: {Response}", json);
                return "";
            }

            var blogsResponse = JsonSerializer.Deserialize<BloggerBlogsResponse>(json);
            var firstBlog = blogsResponse?.items?.FirstOrDefault();

            return firstBlog?.id ?? "";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Blogger] Error getting blog ID");
            return "";
        }
    }

    private string ExtractTitle(string content)
    {
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var firstLine = lines.FirstOrDefault() ?? "Untitled";

        if (firstLine.Length > 100)
        {
            return firstLine.Substring(0, 97) + "...";
        }

        return firstLine;
    }

    private string FormatAsHtml(string content)
    {
        var paragraphs = content.Split("\n\n", StringSplitOptions.RemoveEmptyEntries);
        var html = new StringBuilder();

        foreach (var para in paragraphs)
        {
            html.AppendLine($"<p>{para.Replace("\n", "<br>")}</p>");
        }

        return html.ToString();
    }

    private string GetErrorCode(string statusCode)
    {
        return statusCode switch
        {
            "401" => "invalid_token",
            "403" => "insufficient_permissions",
            "429" => "rate_limit",
            "400" => "invalid_request",
            _ => "api_error"
        };
    }

    // Blogger API response classes
    private class BloggerBlogsResponse
    {
        public string? kind { get; set; }
        public List<BloggerBlog>? items { get; set; }
    }

    private class BloggerBlog
    {
        public string? id { get; set; }
        public string? name { get; set; }
        public string? url { get; set; }
    }

    private class BloggerPostResponse
    {
        public string? kind { get; set; }
        public string? id { get; set; }
        public string? title { get; set; }
        public string? content { get; set; }
        public string? url { get; set; }
        public string? selfLink { get; set; }
        public string? published { get; set; }
        public BloggerReplies? replies { get; set; }
    }

    private class BloggerReplies
    {
        public int totalItems { get; set; }
    }
}
