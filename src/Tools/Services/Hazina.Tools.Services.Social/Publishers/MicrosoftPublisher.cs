using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using Hazina.Tools.Services.Social.Abstractions;

namespace Hazina.Tools.Services.Social.Publishers;

/// <summary>
/// Microsoft Viva Engage (Yammer) social media publisher implementation.
/// Publishes content to Microsoft's enterprise social network using the Yammer API.
/// Requires OAuth 2.0 access token with appropriate Yammer scopes.
/// </summary>
public class MicrosoftPublisher : ISocialPublisher
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<MicrosoftPublisher> _logger;
    private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;

    private const string ApiBaseUrl = "https://www.yammer.com/api/v1";

    public string ProviderId => "microsoft";
    public string DisplayName => "Microsoft";

    public MicrosoftPublisher(
        HttpClient httpClient,
        ILogger<MicrosoftPublisher> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        // Configure retry policy with exponential backoff for rate limits and transient errors
        _retryPolicy = Policy
            .HandleResult<HttpResponseMessage>(r =>
                (int)r.StatusCode == 429 || // Rate limit
                (int)r.StatusCode >= 500)    // Server errors
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (outcome, timespan, retryAttempt, context) =>
                {
                    _logger.LogWarning(
                        "[Microsoft] Retry {RetryAttempt} after {Delay}s. Status: {StatusCode}",
                        retryAttempt, timespan.TotalSeconds, outcome.Result?.StatusCode);
                });
    }

    public async Task<PublishResult> PublishPostAsync(
        string accessToken,
        PublishPostRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("[Microsoft] Publishing post: {PostId}", request.InternalPostId);

            // Build message payload for Yammer
            var messagePayload = BuildMessagePayload(request);

            // Publish to Yammer with retry policy
            var response = await _retryPolicy.ExecuteAsync(async () =>
            {
                var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{ApiBaseUrl}/messages.json");
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                httpRequest.Content = new FormUrlEncodedContent(messagePayload);

                return await _httpClient.SendAsync(httpRequest, cancellationToken);
            });

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[Microsoft] Publish failed: {Status} - {Response}",
                    response.StatusCode, responseBody);

                return new PublishResult
                {
                    Success = false,
                    Error = $"Microsoft Yammer API error: {response.StatusCode}",
                    ErrorCode = GetErrorCode(response.StatusCode.ToString()),
                    Metadata = new Dictionary<string, string>
                    {
                        ["status_code"] = ((int)response.StatusCode).ToString(),
                        ["response"] = responseBody
                    }
                };
            }

            var publishResponse = JsonSerializer.Deserialize<YammerMessageResponse>(responseBody);
            if (publishResponse?.messages == null || !publishResponse.messages.Any())
            {
                return new PublishResult
                {
                    Success = false,
                    Error = "Invalid response from Microsoft Yammer",
                    ErrorCode = "invalid_response"
                };
            }

            var message = publishResponse.messages.First();
            var messageId = message.id.ToString();
            var postUrl = message.web_url ?? $"https://www.yammer.com/messages/{messageId}";

            _logger.LogInformation("[Microsoft] Post published successfully: {MessageId}", messageId);

            return new PublishResult
            {
                Success = true,
                ExternalPostId = messageId,
                Url = postUrl,
                PublishedAt = DateTime.UtcNow,
                Metadata = new Dictionary<string, string>
                {
                    ["message_id"] = messageId,
                    ["network_id"] = message.network_id?.ToString() ?? "",
                    ["group_id"] = message.group_id?.ToString() ?? ""
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Microsoft] Error publishing post");
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
            _logger.LogInformation("[Microsoft] Deleting post: {PostId}", externalPostId);

            var response = await _retryPolicy.ExecuteAsync(async () =>
            {
                var request = new HttpRequestMessage(HttpMethod.Delete, $"{ApiBaseUrl}/messages/{externalPostId}.json");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                return await _httpClient.SendAsync(request, cancellationToken);
            });

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("[Microsoft] Post deleted successfully: {PostId}", externalPostId);
                return true;
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("[Microsoft] Delete failed: {Status} - {Response}",
                response.StatusCode, responseBody);

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Microsoft] Error deleting post: {PostId}", externalPostId);
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
            _logger.LogInformation("[Microsoft] Fetching metrics for post: {PostId}", externalPostId);

            var response = await _retryPolicy.ExecuteAsync(async () =>
            {
                var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiBaseUrl}/messages/{externalPostId}.json");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                return await _httpClient.SendAsync(request, cancellationToken);
            });

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[Microsoft] Metrics fetch failed: {Status} - {Response}",
                    response.StatusCode, responseBody);

                return new PostMetrics
                {
                    ExternalPostId = externalPostId,
                    FetchedAt = DateTime.UtcNow
                };
            }

            var messageResponse = JsonSerializer.Deserialize<YammerMessageDetailResponse>(responseBody);

            return new PostMetrics
            {
                ExternalPostId = externalPostId,
                Likes = messageResponse?.liked_by?.count ?? 0,
                Comments = messageResponse?.stats?.updates ?? 0,
                Shares = messageResponse?.stats?.shares ?? 0,
                Views = messageResponse?.stats?.page_views ?? 0,
                FetchedAt = DateTime.UtcNow,
                AdditionalMetrics = new Dictionary<string, int>
                {
                    ["followers"] = messageResponse?.stats?.followers ?? 0,
                    ["following"] = messageResponse?.stats?.following ?? 0
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Microsoft] Error fetching metrics for post: {PostId}", externalPostId);
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
            _logger.LogInformation("[Microsoft] Validating access token");

            var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiBaseUrl}/users/current.json");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("[Microsoft] Access token is valid");
                return true;
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("[Microsoft] Access validation failed: {Status} - {Response}",
                response.StatusCode, responseBody);

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Microsoft] Error validating access token");
            return false;
        }
    }

    private Dictionary<string, string> BuildMessagePayload(PublishPostRequest request)
    {
        // Format content with hashtags
        var content = request.Content;
        if (request.Hashtags.Any())
        {
            var hashtags = string.Join(" ", request.Hashtags.Select(h => h.StartsWith("#") ? h : $"#{h}"));
            content = $"{content}\n\n{hashtags}";
        }

        var payload = new Dictionary<string, string>
        {
            ["body"] = content
        };

        // Add media attachments if provided (og:image for link previews)
        if (request.MediaUrls.Any())
        {
            // Yammer supports og:image for link previews
            // For actual image uploads, use the attachments API separately
            payload["og_url"] = request.MediaUrls.First();
        }

        return payload;
    }

    private string GetErrorCode(string statusCode)
    {
        return statusCode switch
        {
            "401" => "invalid_token",
            "403" => "insufficient_permissions",
            "429" => "rate_limit",
            "400" => "invalid_request",
            "404" => "not_found",
            _ => "api_error"
        };
    }

    // Yammer API response classes
    private class YammerMessageResponse
    {
        public List<YammerMessage>? messages { get; set; }
    }

    private class YammerMessage
    {
        public long id { get; set; }
        public string? web_url { get; set; }
        public long? network_id { get; set; }
        public long? group_id { get; set; }
    }

    private class YammerMessageDetailResponse
    {
        public long id { get; set; }
        public YammerLikedBy? liked_by { get; set; }
        public YammerStats? stats { get; set; }
    }

    private class YammerLikedBy
    {
        public int count { get; set; }
    }

    private class YammerStats
    {
        public int updates { get; set; }
        public int shares { get; set; }
        public int page_views { get; set; }
        public int followers { get; set; }
        public int following { get; set; }
    }
}
