using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Hazina.Tools.Services.Social.Abstractions;

namespace Hazina.Tools.Services.Social.Publishers;

/// <summary>
/// Reddit social media publisher implementation.
/// Publishes posts to Reddit using the Reddit API.
/// Supports self posts (text), link posts, and image posts.
/// Posts to user profile by default.
/// </summary>
public class RedditPublisher : ISocialPublisher
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RedditPublisher> _logger;

    private const string ApiBaseUrl = "https://oauth.reddit.com";
    private const string UserAgent = "Brand2Boost/1.0";

    public string ProviderId => "reddit";
    public string DisplayName => "Reddit";

    public RedditPublisher(
        HttpClient httpClient,
        ILogger<RedditPublisher> logger)
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
            _logger.LogInformation("[Reddit] Publishing post: {PostId}", request.InternalPostId);

            // Get username for posting to user profile
            var username = await GetUsernameAsync(accessToken, cancellationToken);
            if (string.IsNullOrEmpty(username))
            {
                return new PublishResult
                {
                    Success = false,
                    Error = "Failed to get Reddit username",
                    ErrorCode = "username_fetch_failed"
                };
            }

            // Determine post type and prepare payload
            var postType = DeterminePostType(request);
            var title = ExtractTitle(request.Content);
            var subreddit = $"u_{username}"; // Post to user profile

            // Build submission payload based on type
            var submissionData = new Dictionary<string, string>
            {
                ["sr"] = subreddit,
                ["kind"] = postType,
                ["title"] = title,
                ["api_type"] = "json"
            };

            if (postType == "self")
            {
                // Text post
                var textContent = request.Content;
                if (request.Hashtags.Any())
                {
                    textContent = $"{request.Content}\n\n{string.Join(" ", request.Hashtags)}";
                }
                submissionData["text"] = textContent;
            }
            else if (postType == "link" && request.MediaUrls.Any())
            {
                // Link post (first URL as link)
                submissionData["url"] = request.MediaUrls.First();
            }
            else if (postType == "image" && request.MediaUrls.Any())
            {
                // Image post
                submissionData["url"] = request.MediaUrls.First();
            }

            var content = new FormUrlEncodedContent(submissionData);

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{ApiBaseUrl}/api/submit");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            httpRequest.Headers.UserAgent.ParseAdd(UserAgent);
            httpRequest.Content = content;

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[Reddit] Publish failed: {Status} - {Response}",
                    response.StatusCode, responseBody);

                return new PublishResult
                {
                    Success = false,
                    Error = $"Reddit API error: {response.StatusCode}",
                    ErrorCode = GetErrorCode(response.StatusCode.ToString()),
                    Metadata = new Dictionary<string, string>
                    {
                        ["status_code"] = ((int)response.StatusCode).ToString(),
                        ["response"] = responseBody
                    }
                };
            }

            var submitResponse = JsonSerializer.Deserialize<RedditSubmitResponse>(responseBody);

            // Reddit API returns errors even with 200 status
            if (submitResponse?.json?.errors != null && submitResponse.json.errors.Any())
            {
                var errorMsg = string.Join(", ", submitResponse.json.errors.Select(e => string.Join(" ", e)));
                _logger.LogWarning("[Reddit] Submit failed with errors: {Errors}", errorMsg);

                return new PublishResult
                {
                    Success = false,
                    Error = $"Reddit submission error: {errorMsg}",
                    ErrorCode = "submission_error"
                };
            }

            var postId = submitResponse?.json?.data?.name;
            var postUrl = submitResponse?.json?.data?.url;

            if (string.IsNullOrEmpty(postId))
            {
                return new PublishResult
                {
                    Success = false,
                    Error = "Invalid response from Reddit",
                    ErrorCode = "invalid_response"
                };
            }

            _logger.LogInformation("[Reddit] Post published successfully: {PostId}", postId);

            return new PublishResult
            {
                Success = true,
                ExternalPostId = postId,
                Url = postUrl ?? $"https://www.reddit.com/user/{username}/comments/{postId}",
                PublishedAt = DateTime.UtcNow,
                Metadata = new Dictionary<string, string>
                {
                    ["post_id"] = postId,
                    ["subreddit"] = subreddit,
                    ["post_type"] = postType,
                    ["username"] = username
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Reddit] Error publishing post");
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
            _logger.LogInformation("[Reddit] Deleting post: {PostId}", externalPostId);

            var deleteData = new Dictionary<string, string>
            {
                ["id"] = externalPostId
            };

            var content = new FormUrlEncodedContent(deleteData);

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{ApiBaseUrl}/api/del");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            httpRequest.Headers.UserAgent.ParseAdd(UserAgent);
            httpRequest.Content = content;

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("[Reddit] Post deleted successfully: {PostId}", externalPostId);
                return true;
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("[Reddit] Delete failed: {Status} - {Response}",
                response.StatusCode, responseBody);

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Reddit] Error deleting post: {PostId}", externalPostId);
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
            _logger.LogInformation("[Reddit] Fetching metrics for post: {PostId}", externalPostId);

            // Extract post ID without prefix (t3_)
            var postIdWithoutPrefix = externalPostId.StartsWith("t3_")
                ? externalPostId.Substring(3)
                : externalPostId;

            var httpRequest = new HttpRequestMessage(HttpMethod.Get,
                $"{ApiBaseUrl}/api/info.json?id={externalPostId}");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            httpRequest.Headers.UserAgent.ParseAdd(UserAgent);

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[Reddit] Metrics fetch failed: {Status}", response.StatusCode);
                return new PostMetrics
                {
                    ExternalPostId = externalPostId,
                    FetchedAt = DateTime.UtcNow
                };
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var infoResponse = JsonSerializer.Deserialize<RedditInfoResponse>(responseBody);

            var postData = infoResponse?.data?.children?.FirstOrDefault()?.data;

            if (postData == null)
            {
                return new PostMetrics
                {
                    ExternalPostId = externalPostId,
                    FetchedAt = DateTime.UtcNow
                };
            }

            // Reddit metrics: score (upvotes - downvotes), num_comments
            var upvotes = postData.ups ?? 0;
            var downvotes = postData.downs ?? 0;
            var score = postData.score ?? 0;
            var comments = postData.num_comments ?? 0;

            return new PostMetrics
            {
                ExternalPostId = externalPostId,
                Views = 0, // Reddit doesn't provide view count via API
                Likes = upvotes,
                Shares = 0, // Reddit doesn't track shares directly
                Comments = comments,
                FetchedAt = DateTime.UtcNow,
                Metadata = new Dictionary<string, string>
                {
                    ["score"] = score.ToString(),
                    ["upvotes"] = upvotes.ToString(),
                    ["downvotes"] = downvotes.ToString(),
                    ["upvote_ratio"] = (postData.upvote_ratio?.ToString() ?? "0"),
                    ["num_crossposts"] = (postData.num_crossposts?.ToString() ?? "0"),
                    ["subreddit"] = postData.subreddit ?? ""
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Reddit] Error fetching metrics for post: {PostId}", externalPostId);
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
            _logger.LogInformation("[Reddit] Validating access token");

            var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiBaseUrl}/api/v1/me");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.UserAgent.ParseAdd(UserAgent);

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("[Reddit] Access token is valid");
                return true;
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("[Reddit] Access validation failed: {Status} - {Response}",
                response.StatusCode, responseBody);

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Reddit] Error validating access token");
            return false;
        }
    }

    private async Task<string> GetUsernameAsync(string accessToken, CancellationToken cancellationToken)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiBaseUrl}/api/v1/me");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.UserAgent.ParseAdd(UserAgent);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[Reddit] Failed to get username: {Response}", json);
                return "";
            }

            var userResponse = JsonSerializer.Deserialize<RedditUserResponse>(json);
            return userResponse?.name ?? "";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Reddit] Error getting username");
            return "";
        }
    }

    private string DeterminePostType(PublishPostRequest request)
    {
        // If media URLs exist, check if it's an image
        if (request.MediaUrls.Any())
        {
            var firstUrl = request.MediaUrls.First().ToLower();
            if (firstUrl.EndsWith(".jpg") || firstUrl.EndsWith(".jpeg") ||
                firstUrl.EndsWith(".png") || firstUrl.EndsWith(".gif"))
            {
                return "image";
            }
            return "link";
        }

        // Default to text post
        return "self";
    }

    private string ExtractTitle(string content)
    {
        // Try to extract first line or first sentence as title
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var firstLine = lines.FirstOrDefault() ?? "Untitled";

        // Reddit title limit is 300 characters
        if (firstLine.Length > 300)
        {
            return firstLine.Substring(0, 297) + "...";
        }

        return firstLine;
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

    // Reddit API response classes
    private class RedditUserResponse
    {
        public string? name { get; set; }
        public string? id { get; set; }
    }

    private class RedditSubmitResponse
    {
        public RedditSubmitJson? json { get; set; }
    }

    private class RedditSubmitJson
    {
        public List<List<string>>? errors { get; set; }
        public RedditSubmitData? data { get; set; }
    }

    private class RedditSubmitData
    {
        public string? name { get; set; } // Post ID (e.g., "t3_xxxxx")
        public string? url { get; set; }
        public string? id { get; set; }
    }

    private class RedditInfoResponse
    {
        public RedditListingData? data { get; set; }
    }

    private class RedditListingData
    {
        public List<RedditPost>? children { get; set; }
    }

    private class RedditPost
    {
        public RedditPostData? data { get; set; }
    }

    private class RedditPostData
    {
        public string? id { get; set; }
        public string? name { get; set; }
        public string? title { get; set; }
        public string? subreddit { get; set; }
        public int? score { get; set; }
        public int? ups { get; set; }
        public int? downs { get; set; }
        public int? num_comments { get; set; }
        public int? num_crossposts { get; set; }
        public double? upvote_ratio { get; set; }
        public string? url { get; set; }
    }
}
