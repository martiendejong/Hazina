using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using Hazina.Tools.Services.Social.Abstractions;

namespace Hazina.Tools.Services.Social.Publishers;

/// <summary>
/// LinkedIn social media publisher implementation.
/// Publishes content to LinkedIn using the Community Management Posts API.
/// Requires OAuth 2.0 access token with w_member_social scope.
/// </summary>
public class LinkedInPublisher : ISocialPublisher
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<LinkedInPublisher> _logger;
    private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;

    private const string RestApiBaseUrl = "https://api.linkedin.com/rest";
    private const string V2ApiBaseUrl = "https://api.linkedin.com/v2";
    private const string LinkedInApiVersion = "202506";

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

            // Upload image if provided
            string? imageUrn = null;
            if (request.ImageData != null && request.ImageData.Length > 0)
            {
                imageUrn = await UploadImageAsync(accessToken, personUrn, request, cancellationToken);
                if (imageUrn == null)
                {
                    _logger.LogWarning("[LinkedIn] Image upload failed, publishing text-only post");
                }
            }

            // Build post content with hashtags
            var commentary = request.Content;
            if (request.Hashtags.Any())
            {
                var hashtags = string.Join(" ", request.Hashtags
                    .Where(h => !h.Contains(':')) // Skip metadata tags like category:xxx
                    .Select(h => h.StartsWith("#") ? h : $"#{h}"));
                if (!string.IsNullOrEmpty(hashtags))
                {
                    commentary = $"{commentary}\n\n{hashtags}";
                }
            }

            // Build the Posts API request body
            var postBody = new LinkedInPostRequest
            {
                Author = personUrn,
                Commentary = commentary,
                Visibility = "PUBLIC",
                Distribution = new LinkedInDistribution
                {
                    FeedDistribution = "MAIN_FEED"
                },
                LifecycleState = "PUBLISHED"
            };

            // Add image content if uploaded
            if (!string.IsNullOrEmpty(imageUrn))
            {
                postBody.Content = new LinkedInPostContent
                {
                    Media = new LinkedInPostMedia
                    {
                        Id = imageUrn
                    }
                };
            }

            var json = JsonSerializer.Serialize(postBody, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });

            _logger.LogInformation("[LinkedIn] Posting to REST API: {Json}", json);

            // Publish to LinkedIn Posts API with retry policy
            var response = await _retryPolicy.ExecuteAsync(async () =>
            {
                var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{RestApiBaseUrl}/posts");
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                httpRequest.Headers.Add("LinkedIn-Version", LinkedInApiVersion);
                httpRequest.Headers.Add("X-Restli-Protocol-Version", "2.0.0");
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
                    Error = $"LinkedIn API error: {response.StatusCode} - {responseBody}",
                    ErrorCode = GetErrorCode(((int)response.StatusCode).ToString()),
                    Metadata = new Dictionary<string, string>
                    {
                        ["status_code"] = ((int)response.StatusCode).ToString(),
                        ["response"] = responseBody
                    }
                };
            }

            // The Posts API returns the post URN in the x-restli-id header
            var postUrn = response.Headers.Contains("x-restli-id")
                ? response.Headers.GetValues("x-restli-id").FirstOrDefault()
                : null;

            // Also try to get it from the response body
            if (string.IsNullOrEmpty(postUrn) && !string.IsNullOrEmpty(responseBody))
            {
                try
                {
                    var postResponse = JsonSerializer.Deserialize<LinkedInPostResponse>(responseBody);
                    postUrn = postResponse?.id;
                }
                catch { /* ignore parse errors */ }
            }

            if (string.IsNullOrEmpty(postUrn))
            {
                // 201 Created without an ID is still a success
                _logger.LogWarning("[LinkedIn] Post created but no URN returned. Headers: {Headers}",
                    string.Join(", ", response.Headers.Select(h => $"{h.Key}={string.Join(",", h.Value)}")));
                postUrn = "unknown";
            }

            var shareId = ExtractShareId(postUrn);
            var postUrl = postUrn.StartsWith("urn:")
                ? $"https://www.linkedin.com/feed/update/{postUrn}"
                : "https://www.linkedin.com/feed/";

            _logger.LogInformation("[LinkedIn] Post published successfully: {PostUrn}", postUrn);

            return new PublishResult
            {
                Success = true,
                ExternalPostId = postUrn,
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

    /// <summary>
    /// Uploads an image to LinkedIn and returns the image URN.
    /// Uses the Images API: POST /rest/images?action=initializeUpload, then PUT binary.
    /// </summary>
    private async Task<string?> UploadImageAsync(
        string accessToken,
        string ownerUrn,
        PublishPostRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Step 1: Initialize upload
            var initBody = new
            {
                initializeUploadRequest = new
                {
                    owner = ownerUrn
                }
            };

            var initJson = JsonSerializer.Serialize(initBody);
            var initRequest = new HttpRequestMessage(HttpMethod.Post, $"{RestApiBaseUrl}/images?action=initializeUpload");
            initRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            initRequest.Headers.Add("LinkedIn-Version", LinkedInApiVersion);
            initRequest.Headers.Add("X-Restli-Protocol-Version", "2.0.0");
            initRequest.Content = new StringContent(initJson, Encoding.UTF8, "application/json");

            var initResponse = await _httpClient.SendAsync(initRequest, cancellationToken);
            var initResponseBody = await initResponse.Content.ReadAsStringAsync(cancellationToken);

            if (!initResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("[LinkedIn] Image upload init failed: {Status} - {Response}",
                    initResponse.StatusCode, initResponseBody);
                return null;
            }

            var initResult = JsonSerializer.Deserialize<LinkedInImageInitResponse>(initResponseBody);
            var uploadUrl = initResult?.value?.uploadUrl;
            var imageUrn = initResult?.value?.image;

            if (string.IsNullOrEmpty(uploadUrl) || string.IsNullOrEmpty(imageUrn))
            {
                _logger.LogWarning("[LinkedIn] Image upload init returned no URL/URN: {Response}", initResponseBody);
                return null;
            }

            // Step 2: Upload binary image
            var uploadRequest = new HttpRequestMessage(HttpMethod.Put, uploadUrl);
            uploadRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var imageContent = new ByteArrayContent(request.ImageData!);
            imageContent.Headers.ContentType = new MediaTypeHeaderValue(request.ImageContentType ?? "image/jpeg");
            uploadRequest.Content = imageContent;

            var uploadResponse = await _httpClient.SendAsync(uploadRequest, cancellationToken);

            if (!uploadResponse.IsSuccessStatusCode)
            {
                var uploadBody = await uploadResponse.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("[LinkedIn] Image binary upload failed: {Status} - {Response}",
                    uploadResponse.StatusCode, uploadBody);
                return null;
            }

            _logger.LogInformation("[LinkedIn] Image uploaded successfully: {ImageUrn}", imageUrn);
            return imageUrn;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[LinkedIn] Error uploading image");
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
            _logger.LogInformation("[LinkedIn] Deleting post: {PostId}", externalPostId);

            var response = await _retryPolicy.ExecuteAsync(async () =>
            {
                var request = new HttpRequestMessage(HttpMethod.Delete,
                    $"{RestApiBaseUrl}/posts/{Uri.EscapeDataString(externalPostId)}");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                request.Headers.Add("LinkedIn-Version", LinkedInApiVersion);
                request.Headers.Add("X-Restli-Protocol-Version", "2.0.0");

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

            var url = $"{RestApiBaseUrl}/socialActions/{Uri.EscapeDataString(externalPostId)}";

            var response = await _retryPolicy.ExecuteAsync(async () =>
            {
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                request.Headers.Add("LinkedIn-Version", LinkedInApiVersion);
                request.Headers.Add("X-Restli-Protocol-Version", "2.0.0");

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

            var request = new HttpRequestMessage(HttpMethod.Get, $"{V2ApiBaseUrl}/userinfo");
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
            var request = new HttpRequestMessage(HttpMethod.Get, $"{V2ApiBaseUrl}/userinfo");
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

    private string ExtractShareId(string urn)
    {
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
            "422" => "invalid_request",
            _ => "api_error"
        };
    }

    // LinkedIn Posts API request/response classes
    private class LinkedInPostRequest
    {
        [JsonPropertyName("author")]
        public string Author { get; set; } = "";

        [JsonPropertyName("commentary")]
        public string Commentary { get; set; } = "";

        [JsonPropertyName("visibility")]
        public string Visibility { get; set; } = "PUBLIC";

        [JsonPropertyName("distribution")]
        public LinkedInDistribution Distribution { get; set; } = new();

        [JsonPropertyName("lifecycleState")]
        public string LifecycleState { get; set; } = "PUBLISHED";

        [JsonPropertyName("content")]
        public LinkedInPostContent? Content { get; set; }
    }

    private class LinkedInDistribution
    {
        [JsonPropertyName("feedDistribution")]
        public string FeedDistribution { get; set; } = "MAIN_FEED";

        [JsonPropertyName("targetEntities")]
        public List<object> TargetEntities { get; set; } = new();

        [JsonPropertyName("thirdPartyDistributionChannels")]
        public List<object> ThirdPartyDistributionChannels { get; set; } = new();
    }

    private class LinkedInPostContent
    {
        [JsonPropertyName("media")]
        public LinkedInPostMedia? Media { get; set; }
    }

    private class LinkedInPostMedia
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";
    }

    private class LinkedInPostResponse
    {
        public string? id { get; set; }
    }

    // Image upload response classes
    private class LinkedInImageInitResponse
    {
        public LinkedInImageInitValue? value { get; set; }
    }

    private class LinkedInImageInitValue
    {
        public string? uploadUrl { get; set; }
        public string? image { get; set; }
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
