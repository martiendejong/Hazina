using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using Hazina.Tools.Services.Social.Abstractions;

namespace Hazina.Tools.Services.Social.Publishers;

/// <summary>
/// LinkedIn social media publisher implementation.
/// Publishes content to LinkedIn using the UGC API.
/// Requires OAuth 2.0 access token with w_member_social scope.
/// </summary>
public class LinkedInPublisher : ISocialPublisher
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<LinkedInPublisher> _logger;
    private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;

    private const string ApiBaseUrl = "https://api.linkedin.com/v2";

    public string ProviderId => "linkedin";
    public string DisplayName => "LinkedIn";

    public LinkedInPublisher(
        HttpClient httpClient,
        ILogger<LinkedInPublisher> logger)
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
                        "[LinkedIn] Retry {RetryAttempt} after {Delay}s. Status: {StatusCode}",
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
            _logger.LogInformation("[LinkedIn] Publishing post: {PostId}", request.InternalPostId);

            // Get user profile to get person URN
            var personUrn = await GetPersonUrnAsync(accessToken, cancellationToken);
            if (string.IsNullOrEmpty(personUrn))
            {
                return new PublishResult
                {
                    Success = false,
                    Error = "Failed to get user profile",
                    ErrorCode = "profile_fetch_failed"
                };
            }

            // Build UGC post payload
            var ugcPost = BuildUgcPost(personUrn, request);
            var json = JsonSerializer.Serialize(ugcPost, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            // Publish to LinkedIn with retry policy
            var response = await _retryPolicy.ExecuteAsync(async () =>
            {
                var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{ApiBaseUrl}/ugcPosts");
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

                return await _httpClient.SendAsync(httpRequest, cancellationToken);
            });

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[LinkedIn] Publish failed: {Status} - {Response}",
                    response.StatusCode, responseBody);

                return new PublishResult
                {
                    Success = false,
                    Error = $"LinkedIn API error: {response.StatusCode}",
                    ErrorCode = GetErrorCode(response.StatusCode.ToString()),
                    Metadata = new Dictionary<string, string>
                    {
                        ["status_code"] = ((int)response.StatusCode).ToString(),
                        ["response"] = responseBody
                    }
                };
            }

            var publishResponse = JsonSerializer.Deserialize<LinkedInUgcPostResponse>(responseBody);
            if (publishResponse?.id == null)
            {
                return new PublishResult
                {
                    Success = false,
                    Error = "Invalid response from LinkedIn",
                    ErrorCode = "invalid_response"
                };
            }

            // Extract share ID from URN (format: urn:li:share:123456789)
            var shareId = ExtractShareId(publishResponse.id);
            var postUrl = $"https://www.linkedin.com/feed/update/{publishResponse.id}";

            _logger.LogInformation("[LinkedIn] Post published successfully: {ShareId}", shareId);

            return new PublishResult
            {
                Success = true,
                ExternalPostId = publishResponse.id,
                Url = postUrl,
                PublishedAt = DateTime.UtcNow,
                Metadata = new Dictionary<string, string>
                {
                    ["share_id"] = shareId,
                    ["person_urn"] = personUrn
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[LinkedIn] Error publishing post");
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
            _logger.LogInformation("[LinkedIn] Deleting post: {PostId}", externalPostId);

            var response = await _retryPolicy.ExecuteAsync(async () =>
            {
                var request = new HttpRequestMessage(HttpMethod.Delete, $"{ApiBaseUrl}/ugcPosts/{externalPostId}");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                return await _httpClient.SendAsync(request, cancellationToken);
            });

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("[LinkedIn] Post deleted successfully: {PostId}", externalPostId);
                return true;
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("[LinkedIn] Delete failed: {Status} - {Response}",
                response.StatusCode, responseBody);

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[LinkedIn] Error deleting post: {PostId}", externalPostId);
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
            _logger.LogInformation("[LinkedIn] Fetching metrics for post: {PostId}", externalPostId);

            // LinkedIn Social Actions API for engagement metrics
            var shareId = ExtractShareId(externalPostId);
            var url = $"{ApiBaseUrl}/socialActions/{externalPostId}";

            var response = await _retryPolicy.ExecuteAsync(async () =>
            {
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                return await _httpClient.SendAsync(request, cancellationToken);
            });

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[LinkedIn] Metrics fetch failed: {Status} - {Response}",
                    response.StatusCode, responseBody);

                return new PostMetrics
                {
                    ExternalPostId = externalPostId,
                    FetchedAt = DateTime.UtcNow
                };
            }

            var metricsResponse = JsonSerializer.Deserialize<LinkedInSocialActionsResponse>(responseBody);

            return new PostMetrics
            {
                ExternalPostId = externalPostId,
                Likes = metricsResponse?.likesSummary?.totalLikes ?? 0,
                Comments = metricsResponse?.commentsSummary?.totalComments ?? 0,
                Shares = metricsResponse?.sharesSummary?.totalShares ?? 0,
                Views = metricsResponse?.impressionCount ?? 0,
                FetchedAt = DateTime.UtcNow,
                AdditionalMetrics = new Dictionary<string, int>
                {
                    ["engagement_rate"] = metricsResponse?.engagementRate ?? 0,
                    ["click_count"] = metricsResponse?.clickCount ?? 0
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[LinkedIn] Error fetching metrics for post: {PostId}", externalPostId);
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
            _logger.LogInformation("[LinkedIn] Validating access token");

            var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiBaseUrl}/userinfo");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("[LinkedIn] Access token is valid");
                return true;
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("[LinkedIn] Access validation failed: {Status} - {Response}",
                response.StatusCode, responseBody);

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[LinkedIn] Error validating access token");
            return false;
        }
    }

    private async Task<string> GetPersonUrnAsync(string accessToken, CancellationToken cancellationToken)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiBaseUrl}/userinfo");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[LinkedIn] Failed to get user info: {Response}", json);
                return "";
            }

            var userInfo = JsonSerializer.Deserialize<LinkedInUserInfo>(json);
            if (userInfo?.sub == null)
            {
                return "";
            }

            return $"urn:li:person:{userInfo.sub}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[LinkedIn] Error getting person URN");
            return "";
        }
    }

    private LinkedInUgcPost BuildUgcPost(string personUrn, PublishPostRequest request)
    {
        // Format content with hashtags
        var content = request.Content;
        if (request.Hashtags.Any())
        {
            var hashtags = string.Join(" ", request.Hashtags.Select(h => h.StartsWith("#") ? h : $"#{h}"));
            content = $"{content}\n\n{hashtags}";
        }

        var ugcPost = new LinkedInUgcPost
        {
            Author = personUrn,
            LifecycleState = "PUBLISHED",
            SpecificContent = new LinkedInSpecificContent
            {
                ShareContent = new LinkedInShareContent
                {
                    ShareCommentary = new LinkedInText
                    {
                        Text = content
                    },
                    ShareMediaCategory = request.MediaUrls.Any() ? "IMAGE" : "NONE"
                }
            },
            Visibility = new LinkedInVisibility
            {
                MemberNetworkVisibility = "PUBLIC"
            }
        };

        // Add media if provided
        if (request.MediaUrls.Any())
        {
            ugcPost.SpecificContent.ShareContent.Media = request.MediaUrls
                .Take(9) // LinkedIn allows max 9 images
                .Select(url => new LinkedInMedia
                {
                    Status = "READY",
                    OriginalUrl = url
                })
                .ToList();
        }

        return ugcPost;
    }

    private string ExtractShareId(string urn)
    {
        // Extract ID from URN format: urn:li:share:123456789
        var parts = urn.Split(':');
        return parts.Length > 0 ? parts[^1] : urn;
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

    // LinkedIn API request/response classes
    private class LinkedInUgcPost
    {
        public string Author { get; set; } = "";
        public string LifecycleState { get; set; } = "";
        public LinkedInSpecificContent SpecificContent { get; set; } = new();
        public LinkedInVisibility Visibility { get; set; } = new();
    }

    private class LinkedInSpecificContent
    {
        public LinkedInShareContent ShareContent { get; set; } = new();

        // Custom property name for LinkedIn API
        [System.Text.Json.Serialization.JsonPropertyName("com.linkedin.ugc.ShareContent")]
        public LinkedInShareContent? ComLinkedInUgcShareContent
        {
            get => ShareContent;
            set => ShareContent = value ?? new LinkedInShareContent();
        }
    }

    private class LinkedInShareContent
    {
        public LinkedInText ShareCommentary { get; set; } = new();
        public string ShareMediaCategory { get; set; } = "";
        public List<LinkedInMedia>? Media { get; set; }
    }

    private class LinkedInText
    {
        public string Text { get; set; } = "";
    }

    private class LinkedInMedia
    {
        public string Status { get; set; } = "";
        public string OriginalUrl { get; set; } = "";
    }

    private class LinkedInVisibility
    {
        public string MemberNetworkVisibility { get; set; } = "";

        // Custom property name for LinkedIn API
        [System.Text.Json.Serialization.JsonPropertyName("com.linkedin.ugc.MemberNetworkVisibility")]
        public string? ComLinkedInUgcMemberNetworkVisibility
        {
            get => MemberNetworkVisibility;
            set => MemberNetworkVisibility = value ?? "";
        }
    }

    private class LinkedInUgcPostResponse
    {
        public string? id { get; set; }
    }

    private class LinkedInUserInfo
    {
        public string? sub { get; set; }
    }

    private class LinkedInSocialActionsResponse
    {
        public LinkedInLikesSummary? likesSummary { get; set; }
        public LinkedInCommentsSummary? commentsSummary { get; set; }
        public LinkedInSharesSummary? sharesSummary { get; set; }
        public int impressionCount { get; set; }
        public int engagementRate { get; set; }
        public int clickCount { get; set; }
    }

    private class LinkedInLikesSummary
    {
        public int totalLikes { get; set; }
    }

    private class LinkedInCommentsSummary
    {
        public int totalComments { get; set; }
    }

    private class LinkedInSharesSummary
    {
        public int totalShares { get; set; }
    }
}
