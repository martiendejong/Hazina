using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Hazina.Tools.Services.Social.Abstractions;

namespace Hazina.Tools.Services.Social.Publishers;

/// <summary>
/// Medium blog publisher implementation.
/// Publishes articles to Medium using the Medium API.
/// Supports publishing to user's profile and publications.
/// </summary>
public class MediumPublisher : ISocialPublisher
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<MediumPublisher> _logger;

    private const string ApiBaseUrl = "https://api.medium.com/v1";

    public string ProviderId => "medium";
    public string DisplayName => "Medium";

    public MediumPublisher(
        HttpClient httpClient,
        ILogger<MediumPublisher> logger)
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
            _logger.LogInformation("[Medium] Publishing article: {PostId}", request.InternalPostId);

            // Get user ID
            var userId = await GetUserIdAsync(accessToken, cancellationToken);
            if (string.IsNullOrEmpty(userId))
            {
                return new PublishResult
                {
                    Success = false,
                    Error = "Failed to get Medium user ID",
                    ErrorCode = "user_id_fetch_failed"
                };
            }

            // Build article content with hashtags
            var content = request.Content;
            if (request.Hashtags.Any())
            {
                var tags = request.Hashtags.Select(h => h.StartsWith("#") ? h.Substring(1) : h).ToList();
                content = $"{request.Content}\n\n{string.Join(", ", request.Hashtags)}";
            }

            // Medium uses Markdown or HTML
            var article = new
            {
                title = ExtractTitle(request.Content),
                contentFormat = "html",
                content = FormatAsHtml(content),
                tags = request.Hashtags.Select(h => h.StartsWith("#") ? h.Substring(1) : h).Take(5).ToArray(),
                publishStatus = "public", // or "draft", "unlisted"
                canonicalUrl = request.MediaUrls.FirstOrDefault() // Optional: original URL if cross-posting
            };

            var json = JsonSerializer.Serialize(article);
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{ApiBaseUrl}/users/{userId}/posts");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[Medium] Publish failed: {Status} - {Response}",
                    response.StatusCode, responseBody);

                return new PublishResult
                {
                    Success = false,
                    Error = $"Medium API error: {response.StatusCode}",
                    ErrorCode = GetErrorCode(response.StatusCode.ToString()),
                    Metadata = new Dictionary<string, string>
                    {
                        ["status_code"] = ((int)response.StatusCode).ToString(),
                        ["response"] = responseBody
                    }
                };
            }

            var publishResponse = JsonSerializer.Deserialize<MediumPostResponse>(responseBody);
            if (publishResponse?.data?.id == null)
            {
                return new PublishResult
                {
                    Success = false,
                    Error = "Invalid response from Medium",
                    ErrorCode = "invalid_response"
                };
            }

            var postId = publishResponse.data.id;
            var postUrl = publishResponse.data.url ?? $"https://medium.com/@{userId}/{postId}";

            _logger.LogInformation("[Medium] Article published successfully: {PostId}", postId);

            return new PublishResult
            {
                Success = true,
                ExternalPostId = postId,
                Url = postUrl,
                PublishedAt = DateTime.UtcNow,
                Metadata = new Dictionary<string, string>
                {
                    ["post_id"] = postId,
                    ["user_id"] = userId,
                    ["canonical_url"] = publishResponse.data.canonicalUrl ?? ""
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Medium] Error publishing article");
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
            _logger.LogWarning("[Medium] Medium API does not support post deletion");
            // Medium doesn't provide a delete API - users must delete manually from the website
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Medium] Error attempting to delete post: {PostId}", externalPostId);
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
            _logger.LogWarning("[Medium] Medium API does not provide metrics via API");
            // Medium doesn't provide metrics through their API
            // Users must check stats on the Medium website
            return new PostMetrics
            {
                ExternalPostId = externalPostId,
                FetchedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Medium] Error fetching metrics for post: {PostId}", externalPostId);
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
            _logger.LogInformation("[Medium] Validating access token");

            var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiBaseUrl}/me");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("[Medium] Access token is valid");
                return true;
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("[Medium] Access validation failed: {Status} - {Response}",
                response.StatusCode, responseBody);

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Medium] Error validating access token");
            return false;
        }
    }

    private async Task<string> GetUserIdAsync(string accessToken, CancellationToken cancellationToken)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiBaseUrl}/me");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[Medium] Failed to get user ID: {Response}", json);
                return "";
            }

            var userResponse = JsonSerializer.Deserialize<MediumUserResponse>(json);
            return userResponse?.data?.id ?? "";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Medium] Error getting user ID");
            return "";
        }
    }

    private string ExtractTitle(string content)
    {
        // Try to extract first line or first sentence as title
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var firstLine = lines.FirstOrDefault() ?? "Untitled";

        // Limit title length
        if (firstLine.Length > 100)
        {
            return firstLine.Substring(0, 97) + "...";
        }

        return firstLine;
    }

    private string FormatAsHtml(string content)
    {
        // Convert plain text to basic HTML
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

    // Medium API response classes
    private class MediumUserResponse
    {
        public MediumUserData? data { get; set; }
    }

    private class MediumUserData
    {
        public string? id { get; set; }
        public string? username { get; set; }
        public string? name { get; set; }
    }

    private class MediumPostResponse
    {
        public MediumPostData? data { get; set; }
    }

    private class MediumPostData
    {
        public string? id { get; set; }
        public string? title { get; set; }
        public string? url { get; set; }
        public string? canonicalUrl { get; set; }
        public string? publishStatus { get; set; }
    }
}
