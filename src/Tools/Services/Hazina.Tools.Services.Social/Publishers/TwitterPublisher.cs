using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Hazina.Tools.Services.Social.Abstractions;

namespace Hazina.Tools.Services.Social.Publishers;

/// <summary>
/// Twitter (X) social media publisher implementation.
/// Publishes content to Twitter using the v2 API.
/// Requires OAuth 2.0 access token with tweet.write scope.
/// </summary>
public class TwitterPublisher : ISocialPublisher
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TwitterPublisher> _logger;

    private const string ApiBaseUrl = "https://api.twitter.com/2";

    public string ProviderId => "twitter";
    public string DisplayName => "Twitter";

    public TwitterPublisher(
        HttpClient httpClient,
        ILogger<TwitterPublisher> logger)
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
            _logger.LogInformation("[Twitter] Publishing tweet: {PostId}", request.InternalPostId);

            // Build tweet payload
            var tweet = BuildTweet(request);
            var json = JsonSerializer.Serialize(tweet, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

            // Publish to Twitter
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{ApiBaseUrl}/tweets");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[Twitter] Publish failed: {Status} - {Response}",
                    response.StatusCode, responseBody);

                return new PublishResult
                {
                    Success = false,
                    Error = $"Twitter API error: {response.StatusCode}",
                    ErrorCode = GetErrorCode(response.StatusCode.ToString()),
                    Metadata = new Dictionary<string, string>
                    {
                        ["status_code"] = ((int)response.StatusCode).ToString(),
                        ["response"] = responseBody
                    }
                };
            }

            var publishResponse = JsonSerializer.Deserialize<TwitterTweetResponse>(responseBody);
            if (publishResponse?.data?.id == null)
            {
                return new PublishResult
                {
                    Success = false,
                    Error = "Invalid response from Twitter",
                    ErrorCode = "invalid_response"
                };
            }

            var tweetId = publishResponse.data.id;
            var tweetUrl = $"https://twitter.com/i/web/status/{tweetId}";

            _logger.LogInformation("[Twitter] Tweet published successfully: {TweetId}", tweetId);

            return new PublishResult
            {
                Success = true,
                ExternalPostId = tweetId,
                Url = tweetUrl,
                PublishedAt = DateTime.UtcNow,
                Metadata = new Dictionary<string, string>
                {
                    ["tweet_id"] = tweetId
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Twitter] Error publishing tweet");
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
            _logger.LogInformation("[Twitter] Deleting tweet: {TweetId}", externalPostId);

            var request = new HttpRequestMessage(HttpMethod.Delete, $"{ApiBaseUrl}/tweets/{externalPostId}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("[Twitter] Tweet deleted successfully: {TweetId}", externalPostId);
                return true;
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("[Twitter] Delete failed: {Status} - {Response}",
                response.StatusCode, responseBody);

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Twitter] Error deleting tweet: {TweetId}", externalPostId);
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
            _logger.LogInformation("[Twitter] Fetching metrics for tweet: {TweetId}", externalPostId);

            // Twitter v2 API for tweet metrics
            var url = $"{ApiBaseUrl}/tweets/{externalPostId}?tweet.fields=public_metrics";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[Twitter] Metrics fetch failed: {Status} - {Response}",
                    response.StatusCode, responseBody);

                return new PostMetrics
                {
                    ExternalPostId = externalPostId,
                    FetchedAt = DateTime.UtcNow
                };
            }

            var metricsResponse = JsonSerializer.Deserialize<TwitterMetricsResponse>(responseBody);
            var metrics = metricsResponse?.data?.public_metrics;

            return new PostMetrics
            {
                ExternalPostId = externalPostId,
                Likes = metrics?.like_count ?? 0,
                Comments = metrics?.reply_count ?? 0,
                Shares = metrics?.retweet_count ?? 0,
                Views = metrics?.impression_count ?? 0,
                FetchedAt = DateTime.UtcNow,
                AdditionalMetrics = new Dictionary<string, int>
                {
                    ["quote_count"] = metrics?.quote_count ?? 0,
                    ["bookmark_count"] = metrics?.bookmark_count ?? 0
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Twitter] Error fetching metrics for tweet: {TweetId}", externalPostId);
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
            _logger.LogInformation("[Twitter] Validating access token");

            var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiBaseUrl}/users/me");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("[Twitter] Access token is valid");
                return true;
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("[Twitter] Access validation failed: {Status} - {Response}",
                response.StatusCode, responseBody);

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Twitter] Error validating access token");
            return false;
        }
    }

    private TwitterTweet BuildTweet(PublishPostRequest request)
    {
        // Format content with hashtags
        var content = request.Content;
        if (request.Hashtags.Any())
        {
            var hashtags = string.Join(" ", request.Hashtags.Select(h => h.StartsWith("#") ? h : $"#{h}"));
            content = $"{content}\n\n{hashtags}";
        }

        // Ensure content doesn't exceed 280 characters
        if (content.Length > 280)
        {
            _logger.LogWarning("[Twitter] Content exceeds 280 characters ({Length}), truncating", content.Length);
            content = content.Substring(0, 277) + "...";
        }

        var tweet = new TwitterTweet
        {
            Text = content
        };

        // Add media if provided (Twitter requires media to be uploaded first, then referenced by ID)
        // For now, we'll just include the first media URL in the text if it fits
        if (request.MediaUrls.Any() && content.Length < 250)
        {
            tweet.Text = $"{content}\n\n{request.MediaUrls.First()}";
        }

        return tweet;
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

    // Twitter API request/response classes
    private class TwitterTweet
    {
        public string Text { get; set; } = "";
    }

    private class TwitterTweetResponse
    {
        public TwitterTweetData? data { get; set; }
    }

    private class TwitterTweetData
    {
        public string? id { get; set; }
        public string? text { get; set; }
    }

    private class TwitterMetricsResponse
    {
        public TwitterTweetMetricsData? data { get; set; }
    }

    private class TwitterTweetMetricsData
    {
        public string? id { get; set; }
        public TwitterPublicMetrics? public_metrics { get; set; }
    }

    private class TwitterPublicMetrics
    {
        public int retweet_count { get; set; }
        public int reply_count { get; set; }
        public int like_count { get; set; }
        public int quote_count { get; set; }
        public int bookmark_count { get; set; }
        public int impression_count { get; set; }
    }
}
