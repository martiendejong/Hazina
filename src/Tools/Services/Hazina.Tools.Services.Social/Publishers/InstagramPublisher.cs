using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Hazina.Tools.Services.Social.Abstractions;

namespace Hazina.Tools.Services.Social.Publishers;

/// <summary>
/// Instagram social media publisher implementation.
/// Publishes content to Instagram using the Graph API.
/// Requires Facebook Business account and Instagram Business/Creator account.
/// Note: Instagram requires at least one image or video - text-only posts are not supported.
/// </summary>
public class InstagramPublisher : ISocialPublisher
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<InstagramPublisher> _logger;

    private const string ApiBaseUrl = "https://graph.facebook.com/v18.0";

    public string ProviderId => "instagram";
    public string DisplayName => "Instagram";

    public InstagramPublisher(
        HttpClient httpClient,
        ILogger<InstagramPublisher> logger)
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
            _logger.LogInformation("[Instagram] Publishing post: {PostId}", request.InternalPostId);

            // Instagram requires at least one image
            if (!request.MediaUrls.Any())
            {
                return new PublishResult
                {
                    Success = false,
                    Error = "Instagram requires at least one image or video",
                    ErrorCode = "media_required"
                };
            }

            // Get Instagram Business Account ID
            var instagramAccountId = await GetInstagramAccountIdAsync(accessToken, cancellationToken);
            if (string.IsNullOrEmpty(instagramAccountId))
            {
                return new PublishResult
                {
                    Success = false,
                    Error = "Failed to get Instagram Business Account ID",
                    ErrorCode = "account_id_fetch_failed"
                };
            }

            // Build caption with hashtags
            var caption = request.Content;
            if (request.Hashtags.Any())
            {
                var hashtags = string.Join(" ", request.Hashtags.Select(h => h.StartsWith("#") ? h : $"#{h}"));
                caption = $"{caption}\n\n{hashtags}";
            }

            // Instagram posting is a two-step process:
            // 1. Create a container (media object)
            // 2. Publish the container

            // Step 1: Create container
            var containerId = await CreateMediaContainerAsync(
                instagramAccountId,
                request.MediaUrls.First(),
                caption,
                accessToken,
                cancellationToken);

            if (string.IsNullOrEmpty(containerId))
            {
                return new PublishResult
                {
                    Success = false,
                    Error = "Failed to create media container",
                    ErrorCode = "container_creation_failed"
                };
            }

            // Step 2: Publish container
            var mediaId = await PublishMediaContainerAsync(instagramAccountId, containerId, accessToken, cancellationToken);

            if (string.IsNullOrEmpty(mediaId))
            {
                return new PublishResult
                {
                    Success = false,
                    Error = "Failed to publish media container",
                    ErrorCode = "container_publish_failed"
                };
            }

            // Instagram doesn't provide a direct URL to the post in the API response
            // URL format: https://www.instagram.com/p/{shortcode}/
            // We'll store the media ID and users can view it on their profile
            var postUrl = $"https://www.instagram.com/p/{mediaId}/";

            _logger.LogInformation("[Instagram] Post published successfully: {MediaId}", mediaId);

            return new PublishResult
            {
                Success = true,
                ExternalPostId = mediaId,
                Url = postUrl,
                PublishedAt = DateTime.UtcNow,
                Metadata = new Dictionary<string, string>
                {
                    ["media_id"] = mediaId,
                    ["container_id"] = containerId,
                    ["instagram_account_id"] = instagramAccountId
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Instagram] Error publishing post");
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
            _logger.LogInformation("[Instagram] Deleting post: {MediaId}", externalPostId);

            // Instagram Graph API allows deleting media
            var url = $"{ApiBaseUrl}/{externalPostId}?access_token={accessToken}";
            var response = await _httpClient.DeleteAsync(url, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("[Instagram] Post deleted successfully: {MediaId}", externalPostId);
                return true;
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("[Instagram] Delete failed: {Status} - {Response}",
                response.StatusCode, responseBody);

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Instagram] Error deleting post: {MediaId}", externalPostId);
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
            _logger.LogInformation("[Instagram] Fetching metrics for post: {MediaId}", externalPostId);

            // Instagram Insights API
            var url = $"{ApiBaseUrl}/{externalPostId}/insights?metric=engagement,impressions,reach,saved&access_token={accessToken}";

            var response = await _httpClient.GetAsync(url, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[Instagram] Metrics fetch failed: {Status} - {Response}",
                    response.StatusCode, responseBody);

                return new PostMetrics
                {
                    ExternalPostId = externalPostId,
                    FetchedAt = DateTime.UtcNow
                };
            }

            var metricsResponse = JsonSerializer.Deserialize<InstagramInsightsResponse>(responseBody);

            // Extract metrics from insights
            var engagement = metricsResponse?.data?.FirstOrDefault(d => d.name == "engagement")?.values?.FirstOrDefault()?.value ?? 0;
            var impressions = metricsResponse?.data?.FirstOrDefault(d => d.name == "impressions")?.values?.FirstOrDefault()?.value ?? 0;
            var reach = metricsResponse?.data?.FirstOrDefault(d => d.name == "reach")?.values?.FirstOrDefault()?.value ?? 0;
            var saved = metricsResponse?.data?.FirstOrDefault(d => d.name == "saved")?.values?.FirstOrDefault()?.value ?? 0;

            return new PostMetrics
            {
                ExternalPostId = externalPostId,
                Likes = 0, // Instagram Insights doesn't provide individual like count
                Comments = 0, // Would need separate API call
                Shares = 0, // Not available
                Views = impressions,
                FetchedAt = DateTime.UtcNow,
                AdditionalMetrics = new Dictionary<string, int>
                {
                    ["engagement"] = engagement,
                    ["reach"] = reach,
                    ["saved"] = saved
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Instagram] Error fetching metrics for post: {MediaId}", externalPostId);
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
            _logger.LogInformation("[Instagram] Validating access token");

            var url = $"{ApiBaseUrl}/me?access_token={accessToken}";
            var response = await _httpClient.GetAsync(url, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("[Instagram] Access token is valid");
                return true;
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("[Instagram] Access validation failed: {Status} - {Response}",
                response.StatusCode, responseBody);

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Instagram] Error validating access token");
            return false;
        }
    }

    private async Task<string> GetInstagramAccountIdAsync(string accessToken, CancellationToken cancellationToken)
    {
        try
        {
            // Get Facebook pages and their Instagram Business Accounts
            var url = $"{ApiBaseUrl}/me/accounts?fields=instagram_business_account&access_token={accessToken}";
            var response = await _httpClient.GetAsync(url, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[Instagram] Failed to get Instagram account ID: {Response}", json);
                return "";
            }

            var accountsResponse = JsonSerializer.Deserialize<InstagramAccountsResponse>(json);
            var instagramAccount = accountsResponse?.data?.FirstOrDefault()?.instagram_business_account;

            return instagramAccount?.id ?? "";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Instagram] Error getting Instagram account ID");
            return "";
        }
    }

    private async Task<string> CreateMediaContainerAsync(
        string instagramAccountId,
        string imageUrl,
        string caption,
        string accessToken,
        CancellationToken cancellationToken)
    {
        try
        {
            var formData = new Dictionary<string, string>
            {
                ["image_url"] = imageUrl,
                ["caption"] = caption,
                ["access_token"] = accessToken
            };

            var httpContent = new FormUrlEncodedContent(formData);
            var response = await _httpClient.PostAsync(
                $"{ApiBaseUrl}/{instagramAccountId}/media",
                httpContent,
                cancellationToken);

            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[Instagram] Failed to create media container: {Response}", json);
                return "";
            }

            var containerResponse = JsonSerializer.Deserialize<InstagramContainerResponse>(json);
            return containerResponse?.id ?? "";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Instagram] Error creating media container");
            return "";
        }
    }

    private async Task<string> PublishMediaContainerAsync(
        string instagramAccountId,
        string containerId,
        string accessToken,
        CancellationToken cancellationToken)
    {
        try
        {
            var formData = new Dictionary<string, string>
            {
                ["creation_id"] = containerId,
                ["access_token"] = accessToken
            };

            var httpContent = new FormUrlEncodedContent(formData);
            var response = await _httpClient.PostAsync(
                $"{ApiBaseUrl}/{instagramAccountId}/media_publish",
                httpContent,
                cancellationToken);

            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[Instagram] Failed to publish media container: {Response}", json);
                return "";
            }

            var publishResponse = JsonSerializer.Deserialize<InstagramPublishResponse>(json);
            return publishResponse?.id ?? "";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Instagram] Error publishing media container");
            return "";
        }
    }

    // Instagram API response classes
    private class InstagramAccountsResponse
    {
        public List<InstagramPageData>? data { get; set; }
    }

    private class InstagramPageData
    {
        public InstagramBusinessAccount? instagram_business_account { get; set; }
    }

    private class InstagramBusinessAccount
    {
        public string? id { get; set; }
    }

    private class InstagramContainerResponse
    {
        public string? id { get; set; }
    }

    private class InstagramPublishResponse
    {
        public string? id { get; set; }
    }

    private class InstagramInsightsResponse
    {
        public List<InstagramInsightData>? data { get; set; }
    }

    private class InstagramInsightData
    {
        public string? name { get; set; }
        public List<InstagramInsightValue>? values { get; set; }
    }

    private class InstagramInsightValue
    {
        public int value { get; set; }
    }
}
