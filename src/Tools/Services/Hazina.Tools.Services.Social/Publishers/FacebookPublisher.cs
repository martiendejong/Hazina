using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Hazina.Tools.Services.Social.Abstractions;

namespace Hazina.Tools.Services.Social.Publishers;

/// <summary>
/// Facebook social media publisher implementation.
/// Publishes content to Facebook using the Graph API.
/// Supports publishing to user timeline and pages.
/// </summary>
public class FacebookPublisher : ISocialPublisher
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FacebookPublisher> _logger;

    private const string ApiBaseUrl = "https://graph.facebook.com/v18.0";

    public string ProviderId => "facebook";
    public string DisplayName => "Facebook";

    public FacebookPublisher(
        HttpClient httpClient,
        ILogger<FacebookPublisher> logger)
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
            _logger.LogInformation("[Facebook] Publishing post: {PostId}", request.InternalPostId);

            // Get user ID to publish to their feed
            var userId = await GetUserIdAsync(accessToken, cancellationToken);
            if (string.IsNullOrEmpty(userId))
            {
                return new PublishResult
                {
                    Success = false,
                    Error = "Failed to get user ID",
                    ErrorCode = "user_id_fetch_failed"
                };
            }

            // Build post content
            var content = request.Content;
            if (request.Hashtags.Any())
            {
                var hashtags = string.Join(" ", request.Hashtags.Select(h => h.StartsWith("#") ? h : $"#{h}"));
                content = $"{content}\n\n{hashtags}";
            }

            // Publish to Facebook
            var formData = new Dictionary<string, string>
            {
                ["message"] = content,
                ["access_token"] = accessToken
            };

            // Add link if media URL provided
            if (request.MediaUrls.Any())
            {
                formData["link"] = request.MediaUrls.First();
            }

            var httpContent = new FormUrlEncodedContent(formData);
            var response = await _httpClient.PostAsync($"{ApiBaseUrl}/{userId}/feed", httpContent, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[Facebook] Publish failed: {Status} - {Response}",
                    response.StatusCode, responseBody);

                return new PublishResult
                {
                    Success = false,
                    Error = $"Facebook API error: {response.StatusCode}",
                    ErrorCode = GetErrorCode(response.StatusCode.ToString()),
                    Metadata = new Dictionary<string, string>
                    {
                        ["status_code"] = ((int)response.StatusCode).ToString(),
                        ["response"] = responseBody
                    }
                };
            }

            var publishResponse = JsonSerializer.Deserialize<FacebookPostResponse>(responseBody);
            if (publishResponse?.id == null)
            {
                return new PublishResult
                {
                    Success = false,
                    Error = "Invalid response from Facebook",
                    ErrorCode = "invalid_response"
                };
            }

            var postId = publishResponse.id;
            var postUrl = $"https://www.facebook.com/{postId.Replace("_", "/posts/")}";

            _logger.LogInformation("[Facebook] Post published successfully: {PostId}", postId);

            return new PublishResult
            {
                Success = true,
                ExternalPostId = postId,
                Url = postUrl,
                PublishedAt = DateTime.UtcNow,
                Metadata = new Dictionary<string, string>
                {
                    ["post_id"] = postId,
                    ["user_id"] = userId
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Facebook] Error publishing post");
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
            _logger.LogInformation("[Facebook] Deleting post: {PostId}", externalPostId);

            var url = $"{ApiBaseUrl}/{externalPostId}?access_token={accessToken}";
            var response = await _httpClient.DeleteAsync(url, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("[Facebook] Post deleted successfully: {PostId}", externalPostId);
                return true;
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("[Facebook] Delete failed: {Status} - {Response}",
                response.StatusCode, responseBody);

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Facebook] Error deleting post: {PostId}", externalPostId);
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
            _logger.LogInformation("[Facebook] Fetching metrics for post: {PostId}", externalPostId);

            // Facebook Graph API for post insights
            var url = $"{ApiBaseUrl}/{externalPostId}?fields=likes.summary(true),comments.summary(true),shares&access_token={accessToken}";

            var response = await _httpClient.GetAsync(url, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[Facebook] Metrics fetch failed: {Status} - {Response}",
                    response.StatusCode, responseBody);

                return new PostMetrics
                {
                    ExternalPostId = externalPostId,
                    FetchedAt = DateTime.UtcNow
                };
            }

            var metricsResponse = JsonSerializer.Deserialize<FacebookMetricsResponse>(responseBody);

            return new PostMetrics
            {
                ExternalPostId = externalPostId,
                Likes = metricsResponse?.likes?.summary?.total_count ?? 0,
                Comments = metricsResponse?.comments?.summary?.total_count ?? 0,
                Shares = metricsResponse?.shares?.count ?? 0,
                Views = 0, // Facebook doesn't provide view count via Graph API for posts
                FetchedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Facebook] Error fetching metrics for post: {PostId}", externalPostId);
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
            _logger.LogInformation("[Facebook] Validating access token");

            var url = $"{ApiBaseUrl}/me?access_token={accessToken}";
            var response = await _httpClient.GetAsync(url, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("[Facebook] Access token is valid");
                return true;
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("[Facebook] Access validation failed: {Status} - {Response}",
                response.StatusCode, responseBody);

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Facebook] Error validating access token");
            return false;
        }
    }

    private async Task<string> GetUserIdAsync(string accessToken, CancellationToken cancellationToken)
    {
        try
        {
            var url = $"{ApiBaseUrl}/me?access_token={accessToken}";
            var response = await _httpClient.GetAsync(url, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[Facebook] Failed to get user ID: {Response}", json);
                return "";
            }

            var userInfo = JsonSerializer.Deserialize<FacebookUserInfo>(json);
            return userInfo?.id ?? "";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Facebook] Error getting user ID");
            return "";
        }
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

    // Facebook API response classes
    private class FacebookPostResponse
    {
        public string? id { get; set; }
    }

    private class FacebookUserInfo
    {
        public string? id { get; set; }
        public string? name { get; set; }
    }

    private class FacebookMetricsResponse
    {
        public FacebookLikes? likes { get; set; }
        public FacebookComments? comments { get; set; }
        public FacebookShares? shares { get; set; }
    }

    private class FacebookLikes
    {
        public FacebookSummary? summary { get; set; }
    }

    private class FacebookComments
    {
        public FacebookSummary? summary { get; set; }
    }

    private class FacebookSummary
    {
        public int total_count { get; set; }
    }

    private class FacebookShares
    {
        public int count { get; set; }
    }
}
