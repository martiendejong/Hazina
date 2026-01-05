using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Hazina.Tools.Services.Social.Abstractions;

namespace Hazina.Tools.Services.Social.Publishers;

/// <summary>
/// WordPress.com blog publisher implementation.
/// Publishes posts to WordPress.com blogs using the WordPress REST API.
/// Note: This is for WordPress.com hosted blogs, not self-hosted WordPress sites.
/// </summary>
public class WordPressPublisher : ISocialPublisher
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WordPressPublisher> _logger;

    private const string ApiBaseUrl = "https://public-api.wordpress.com/rest/v1.1";

    public string ProviderId => "wordpress";
    public string DisplayName => "WordPress.com";

    public WordPressPublisher(
        HttpClient httpClient,
        ILogger<WordPressPublisher> logger)
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
            _logger.LogInformation("[WordPress] Publishing post: {PostId}", request.InternalPostId);

            // Get primary site ID
            var siteId = await GetPrimarySiteIdAsync(accessToken, cancellationToken);
            if (string.IsNullOrEmpty(siteId))
            {
                return new PublishResult
                {
                    Success = false,
                    Error = "Failed to get WordPress site ID",
                    ErrorCode = "site_id_fetch_failed"
                };
            }

            // Build post content
            var title = ExtractTitle(request.Content);
            var content = FormatAsHtml(request.Content);

            // Add hashtags as tags
            var tags = request.Hashtags
                .Select(h => h.StartsWith("#") ? h.Substring(1) : h)
                .ToList();

            var post = new
            {
                title = title,
                content = content,
                tags = string.Join(",", tags),
                status = "publish", // or "draft"
                format = "standard" // or "aside", "gallery", "link", "image", "quote", "status", "video", "audio", "chat"
            };

            var json = JsonSerializer.Serialize(post);
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{ApiBaseUrl}/sites/{siteId}/posts/new");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[WordPress] Publish failed: {Status} - {Response}",
                    response.StatusCode, responseBody);

                return new PublishResult
                {
                    Success = false,
                    Error = $"WordPress API error: {response.StatusCode}",
                    ErrorCode = GetErrorCode(response.StatusCode.ToString()),
                    Metadata = new Dictionary<string, string>
                    {
                        ["status_code"] = ((int)response.StatusCode).ToString(),
                        ["response"] = responseBody
                    }
                };
            }

            var publishResponse = JsonSerializer.Deserialize<WordPressPostResponse>(responseBody);
            if (publishResponse?.ID == null)
            {
                return new PublishResult
                {
                    Success = false,
                    Error = "Invalid response from WordPress",
                    ErrorCode = "invalid_response"
                };
            }

            var postId = publishResponse.ID.ToString();
            var postUrl = publishResponse.URL ?? "";

            _logger.LogInformation("[WordPress] Post published successfully: {PostId}", postId);

            return new PublishResult
            {
                Success = true,
                ExternalPostId = postId,
                Url = postUrl,
                PublishedAt = DateTime.UtcNow,
                Metadata = new Dictionary<string, string>
                {
                    ["post_id"] = postId,
                    ["site_id"] = siteId,
                    ["slug"] = publishResponse.slug ?? ""
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WordPress] Error publishing post");
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
            _logger.LogInformation("[WordPress] Deleting post: {PostId}", externalPostId);

            var siteId = await GetPrimarySiteIdAsync(accessToken, cancellationToken);
            if (string.IsNullOrEmpty(siteId))
            {
                return false;
            }

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{ApiBaseUrl}/sites/{siteId}/posts/{externalPostId}/delete");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("[WordPress] Post deleted successfully: {PostId}", externalPostId);
                return true;
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("[WordPress] Delete failed: {Status} - {Response}",
                response.StatusCode, responseBody);

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WordPress] Error deleting post: {PostId}", externalPostId);
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
            _logger.LogInformation("[WordPress] Fetching metrics for post: {PostId}", externalPostId);

            var siteId = await GetPrimarySiteIdAsync(accessToken, cancellationToken);
            if (string.IsNullOrEmpty(siteId))
            {
                return new PostMetrics
                {
                    ExternalPostId = externalPostId,
                    FetchedAt = DateTime.UtcNow
                };
            }

            // Get post stats
            var url = $"{ApiBaseUrl}/sites/{siteId}/stats/post/{externalPostId}";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[WordPress] Metrics fetch failed: {Status} - {Response}",
                    response.StatusCode, responseBody);

                return new PostMetrics
                {
                    ExternalPostId = externalPostId,
                    FetchedAt = DateTime.UtcNow
                };
            }

            var statsResponse = JsonSerializer.Deserialize<WordPressStatsResponse>(responseBody);

            return new PostMetrics
            {
                ExternalPostId = externalPostId,
                Likes = statsResponse?.likes ?? 0,
                Comments = statsResponse?.comments ?? 0,
                Shares = 0, // WordPress doesn't provide share count
                Views = statsResponse?.views ?? 0,
                FetchedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WordPress] Error fetching metrics for post: {PostId}", externalPostId);
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
            _logger.LogInformation("[WordPress] Validating access token");

            var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiBaseUrl}/me");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("[WordPress] Access token is valid");
                return true;
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("[WordPress] Access validation failed: {Status} - {Response}",
                response.StatusCode, responseBody);

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WordPress] Error validating access token");
            return false;
        }
    }

    private async Task<string> GetPrimarySiteIdAsync(string accessToken, CancellationToken cancellationToken)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiBaseUrl}/me/sites");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[WordPress] Failed to get sites: {Response}", json);
                return "";
            }

            var sitesResponse = JsonSerializer.Deserialize<WordPressSitesResponse>(json);
            var primarySite = sitesResponse?.sites?.FirstOrDefault(s => s.is_primary);

            if (primarySite != null)
            {
                return primarySite.ID?.ToString() ?? "";
            }

            // If no primary site, use first site
            var firstSite = sitesResponse?.sites?.FirstOrDefault();
            return firstSite?.ID?.ToString() ?? "";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WordPress] Error getting site ID");
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

    // WordPress API response classes
    private class WordPressSitesResponse
    {
        public List<WordPressSite>? sites { get; set; }
    }

    private class WordPressSite
    {
        public long? ID { get; set; }
        public string? URL { get; set; }
        public string? name { get; set; }
        public bool is_primary { get; set; }
    }

    private class WordPressPostResponse
    {
        public long? ID { get; set; }
        public string? URL { get; set; }
        public string? slug { get; set; }
        public string? title { get; set; }
        public string? status { get; set; }
    }

    private class WordPressStatsResponse
    {
        public int views { get; set; }
        public int likes { get; set; }
        public int comments { get; set; }
    }
}
