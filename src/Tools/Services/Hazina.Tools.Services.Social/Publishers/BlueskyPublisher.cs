using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Hazina.Tools.Services.Social.Abstractions;

namespace Hazina.Tools.Services.Social.Publishers;

/// <summary>
/// Bluesky social media publisher implementation.
/// Publishes content to Bluesky using the AT Protocol API.
/// Note: Bluesky uses app passwords instead of OAuth.
/// </summary>
public class BlueskyPublisher : ISocialPublisher
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BlueskyPublisher> _logger;

    private const string ApiBaseUrl = "https://bsky.social/xrpc";

    public string ProviderId => "bluesky";
    public string DisplayName => "Bluesky";

    public BlueskyPublisher(
        HttpClient httpClient,
        ILogger<BlueskyPublisher> logger)
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
            _logger.LogInformation("[Bluesky] Publishing post: {PostId}", request.InternalPostId);

            // Build post content with hashtags
            var text = request.Content;
            if (request.Hashtags.Any())
            {
                var hashtags = string.Join(" ", request.Hashtags.Select(h => h.StartsWith("#") ? h : $"#{h}"));
                text = $"{text}\n\n{hashtags}";
            }

            // Ensure content doesn't exceed 300 characters
            if (text.Length > 300)
            {
                _logger.LogWarning("[Bluesky] Content exceeds 300 characters ({Length}), truncating", text.Length);
                text = text.Substring(0, 297) + "...";
            }

            // Get user's DID (Decentralized Identifier) from access token
            // For Bluesky, the access token format includes session info
            var session = JsonSerializer.Deserialize<BlueskySession>(accessToken);
            if (session?.did == null)
            {
                return new PublishResult
                {
                    Success = false,
                    Error = "Invalid Bluesky session token",
                    ErrorCode = "invalid_token"
                };
            }

            // Create post record
            var record = new
            {
                text = text,
                createdAt = DateTime.UtcNow.ToString("o"),
                langs = new[] { "en" }
            };

            var postPayload = new
            {
                repo = session.did,
                collection = "app.bsky.feed.post",
                record = record
            };

            var json = JsonSerializer.Serialize(postPayload);
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{ApiBaseUrl}/com.atproto.repo.createRecord");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.accessJwt ?? accessToken);
            httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[Bluesky] Publish failed: {Status} - {Response}",
                    response.StatusCode, responseBody);

                return new PublishResult
                {
                    Success = false,
                    Error = $"Bluesky API error: {response.StatusCode}",
                    ErrorCode = GetErrorCode(response.StatusCode.ToString()),
                    Metadata = new Dictionary<string, string>
                    {
                        ["status_code"] = ((int)response.StatusCode).ToString(),
                        ["response"] = responseBody
                    }
                };
            }

            var publishResponse = JsonSerializer.Deserialize<BlueskyPostResponse>(responseBody);
            if (publishResponse?.uri == null)
            {
                return new PublishResult
                {
                    Success = false,
                    Error = "Invalid response from Bluesky",
                    ErrorCode = "invalid_response"
                };
            }

            // Extract post ID from AT URI (at://did:plc:xxx/app.bsky.feed.post/xxx)
            var postId = publishResponse.uri.Split('/').Last();
            var handle = session.handle ?? session.did;
            var postUrl = $"https://bsky.app/profile/{handle}/post/{postId}";

            _logger.LogInformation("[Bluesky] Post published successfully: {PostId}", postId);

            return new PublishResult
            {
                Success = true,
                ExternalPostId = publishResponse.uri,
                Url = postUrl,
                PublishedAt = DateTime.UtcNow,
                Metadata = new Dictionary<string, string>
                {
                    ["uri"] = publishResponse.uri,
                    ["cid"] = publishResponse.cid ?? ""
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Bluesky] Error publishing post");
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
            _logger.LogInformation("[Bluesky] Deleting post: {Uri}", externalPostId);

            var session = JsonSerializer.Deserialize<BlueskySession>(accessToken);
            if (session?.did == null)
            {
                return false;
            }

            var deletePayload = new
            {
                repo = session.did,
                collection = "app.bsky.feed.post",
                rkey = externalPostId.Split('/').Last()
            };

            var json = JsonSerializer.Serialize(deletePayload);
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{ApiBaseUrl}/com.atproto.repo.deleteRecord");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.accessJwt ?? accessToken);
            httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("[Bluesky] Post deleted successfully: {Uri}", externalPostId);
                return true;
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("[Bluesky] Delete failed: {Status} - {Response}",
                response.StatusCode, responseBody);

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Bluesky] Error deleting post: {Uri}", externalPostId);
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
            _logger.LogInformation("[Bluesky] Fetching metrics for post: {Uri}", externalPostId);

            // Bluesky AT Protocol for post metrics
            var url = $"{ApiBaseUrl}/app.bsky.feed.getPostThread?uri={Uri.EscapeDataString(externalPostId)}";

            var session = JsonSerializer.Deserialize<BlueskySession>(accessToken);
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session?.accessJwt ?? accessToken);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[Bluesky] Metrics fetch failed: {Status} - {Response}",
                    response.StatusCode, responseBody);

                return new PostMetrics
                {
                    ExternalPostId = externalPostId,
                    FetchedAt = DateTime.UtcNow
                };
            }

            var metricsResponse = JsonSerializer.Deserialize<BlueskyThreadResponse>(responseBody);
            var post = metricsResponse?.thread?.post;

            return new PostMetrics
            {
                ExternalPostId = externalPostId,
                Likes = post?.likeCount ?? 0,
                Comments = post?.replyCount ?? 0,
                Shares = post?.repostCount ?? 0,
                Views = 0, // Bluesky doesn't provide view count
                FetchedAt = DateTime.UtcNow,
                AdditionalMetrics = new Dictionary<string, int>
                {
                    ["quote_count"] = post?.quoteCount ?? 0
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Bluesky] Error fetching metrics for post: {Uri}", externalPostId);
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
            _logger.LogInformation("[Bluesky] Validating access token");

            var session = JsonSerializer.Deserialize<BlueskySession>(accessToken);
            if (session?.accessJwt == null)
            {
                return false;
            }

            var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiBaseUrl}/com.atproto.server.getSession");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.accessJwt);

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("[Bluesky] Access token is valid");
                return true;
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("[Bluesky] Access validation failed: {Status} - {Response}",
                response.StatusCode, responseBody);

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Bluesky] Error validating access token");
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

    // Bluesky API classes
    private class BlueskySession
    {
        public string? did { get; set; }
        public string? handle { get; set; }
        public string? accessJwt { get; set; }
        public string? refreshJwt { get; set; }
    }

    private class BlueskyPostResponse
    {
        public string? uri { get; set; }
        public string? cid { get; set; }
    }

    private class BlueskyThreadResponse
    {
        public BlueskyThread? thread { get; set; }
    }

    private class BlueskyThread
    {
        public BlueskyPost? post { get; set; }
    }

    private class BlueskyPost
    {
        public int likeCount { get; set; }
        public int replyCount { get; set; }
        public int repostCount { get; set; }
        public int quoteCount { get; set; }
    }
}
