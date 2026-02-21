using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Hazina.Tools.Services.Social.Abstractions;

namespace Hazina.Tools.Services.Social.Publishers;

/// <summary>
/// Twitter (X) social media publisher implementation.
/// Publishes content to X using the v2 API with media upload via v1.1 API.
/// Requires OAuth 2.0 access token with tweet.write scope.
/// </summary>
public class TwitterPublisher : ISocialPublisher
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TwitterPublisher> _logger;

    private const string ApiBaseUrl = "https://api.twitter.com/2";
    private const string MediaUploadUrl = "https://upload.twitter.com/1.1/media/upload.json";

    public string ProviderId => "twitter";
    public string DisplayName => "X (Twitter)";

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

            // Upload image if provided
            string? mediaId = null;
            if (request.ImageData != null && request.ImageData.Length > 0)
            {
                mediaId = await UploadMediaAsync(accessToken, request, cancellationToken);
                if (mediaId != null)
                {
                    _logger.LogInformation("[Twitter] Image uploaded, media_id: {MediaId}", mediaId);
                }
                else
                {
                    _logger.LogWarning("[Twitter] Image upload failed, posting text only");
                }
            }

            // Build tweet content with hashtags
            var content = request.Content;
            if (request.Hashtags.Any())
            {
                var hashtags = string.Join(" ", request.Hashtags
                    .Where(h => !h.Contains(':'))
                    .Select(h => h.StartsWith("#") ? h : $"#{h}"));
                if (!string.IsNullOrEmpty(hashtags))
                {
                    content = $"{content}\n\n{hashtags}";
                }
            }

            // Ensure content doesn't exceed 280 characters
            if (content.Length > 280)
            {
                _logger.LogWarning("[Twitter] Content exceeds 280 characters ({Length}), truncating", content.Length);
                content = content.Substring(0, 277) + "...";
            }

            // Build tweet request body
            var tweetBody = new Dictionary<string, object>
            {
                ["text"] = content
            };

            if (!string.IsNullOrEmpty(mediaId))
            {
                tweetBody["media"] = new Dictionary<string, object>
                {
                    ["media_ids"] = new[] { mediaId }
                };
            }

            var json = JsonSerializer.Serialize(tweetBody);
            _logger.LogInformation("[Twitter] Tweet payload: {Json}", json);

            // Publish tweet via v2 API
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{ApiBaseUrl}/tweets");
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
                    Error = $"Twitter API error: {response.StatusCode} - {responseBody}",
                    ErrorCode = GetErrorCode(((int)response.StatusCode).ToString()),
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
            var tweetUrl = $"https://x.com/i/web/status/{tweetId}";

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

    /// <summary>
    /// Uploads media to Twitter via the v1.1 media upload endpoint.
    /// Returns the media_id_string on success, or null on failure.
    /// </summary>
    private async Task<string?> UploadMediaAsync(
        string accessToken,
        PublishPostRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("[Twitter] Uploading image ({Size} bytes, type: {Type})",
                request.ImageData!.Length, request.ImageContentType ?? "unknown");

            using var multipart = new MultipartFormDataContent();

            var imageContent = new ByteArrayContent(request.ImageData!);
            imageContent.Headers.ContentType = new MediaTypeHeaderValue(request.ImageContentType ?? "image/jpeg");
            multipart.Add(imageContent, "media_data", request.ImageFileName ?? "image.jpg");

            // Twitter media upload uses base64 encoding for the media_data field
            // Alternative: use binary upload with "media" field
            // Let's use the simpler binary approach with "media" field
            using var binaryMultipart = new MultipartFormDataContent();
            var binaryContent = new ByteArrayContent(request.ImageData!);
            binaryContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            binaryMultipart.Add(binaryContent, "media", request.ImageFileName ?? "image.jpg");

            using var uploadRequest = new HttpRequestMessage(HttpMethod.Post, MediaUploadUrl);
            uploadRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            uploadRequest.Content = binaryMultipart;

            var response = await _httpClient.SendAsync(uploadRequest, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[Twitter] Media upload failed: {Status} - {Response}",
                    response.StatusCode, responseBody);
                return null;
            }

            var mediaResponse = JsonSerializer.Deserialize<TwitterMediaUploadResponse>(responseBody);
            var mediaId = mediaResponse?.media_id_string;

            if (string.IsNullOrEmpty(mediaId))
            {
                _logger.LogWarning("[Twitter] Media upload returned no media_id: {Response}", responseBody);
                return null;
            }

            _logger.LogInformation("[Twitter] Media uploaded: {MediaId}", mediaId);
            return mediaId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Twitter] Error uploading media");
            return null;
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

            using var request = new HttpRequestMessage(HttpMethod.Delete, $"{ApiBaseUrl}/tweets/{externalPostId}");
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

            var url = $"{ApiBaseUrl}/tweets/{externalPostId}?tweet.fields=public_metrics";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
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

            using var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiBaseUrl}/users/me");
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

    // Twitter API response classes
    private class TwitterTweetResponse
    {
        public TwitterTweetData? data { get; set; }
    }

    private class TwitterTweetData
    {
        public string? id { get; set; }
        public string? text { get; set; }
    }

    private class TwitterMediaUploadResponse
    {
        public long media_id { get; set; }
        public string? media_id_string { get; set; }
        public string? type { get; set; }
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
