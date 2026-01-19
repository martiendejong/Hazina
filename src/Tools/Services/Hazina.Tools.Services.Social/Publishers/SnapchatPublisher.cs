using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Hazina.Tools.Services.Social.Abstractions;

namespace Hazina.Tools.Services.Social.Publishers;

/// <summary>
/// Snapchat social media publisher implementation.
/// Publishes content to Snapchat using the Marketing API.
/// Note: Snapchat's public API is primarily for advertising/marketing.
/// Requires Snapchat Business account and ad account setup.
/// </summary>
public class SnapchatPublisher : ISocialPublisher
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SnapchatPublisher> _logger;

    private const string ApiBaseUrl = "https://adsapi.snapchat.com/v1";

    public string ProviderId => "snapchat";
    public string DisplayName => "Snapchat";

    public SnapchatPublisher(
        HttpClient httpClient,
        ILogger<SnapchatPublisher> logger)
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
            _logger.LogInformation("[Snapchat] Publishing creative: {PostId}", request.InternalPostId);

            // Snapchat requires ad account ID and media must be pre-uploaded
            // For organic posting, Snapchat doesn't have a public API
            // Marketing API requires creatives to be part of ad campaigns

            // Get ad account (required for Marketing API)
            var adAccountId = await GetAdAccountIdAsync(accessToken, cancellationToken);
            if (string.IsNullOrEmpty(adAccountId))
            {
                return new PublishResult
                {
                    Success = false,
                    Error = "No ad account found. Snapchat Marketing API requires a business account with ad account setup.",
                    ErrorCode = "no_ad_account"
                };
            }

            // Snapchat requires media to be uploaded first
            if (!request.MediaUrls.Any())
            {
                return new PublishResult
                {
                    Success = false,
                    Error = "Snapchat creatives require at least one media URL (image or video)",
                    ErrorCode = "missing_media"
                };
            }

            var mediaUrl = request.MediaUrls.First();
            var creativeName = ExtractTitle(request.Content);

            // Create creative payload
            // Note: This is a simplified version. Full implementation would require:
            // 1. Media upload via /media endpoint
            // 2. Creative creation with proper ad_account_id
            // 3. Campaign/ad_squad association

            var creativePayload = new
            {
                creatives = new[]
                {
                    new
                    {
                        ad_account_id = adAccountId,
                        name = creativeName,
                        type = "SNAP_AD", // or "STORY_AD"
                        brand_name = "Brand2Boost",
                        shareable = true,
                        // headline would come from content
                        headline = creativeName,
                        // top_snap_media_id would be from uploaded media
                        // For demo purposes, using a placeholder
                        top_snap_media_id = "placeholder_media_id"
                    }
                }
            };

            var json = JsonSerializer.Serialize(creativePayload);
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{ApiBaseUrl}/adaccounts/{adAccountId}/creatives");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[Snapchat] Publish failed: {Status} - {Response}",
                    response.StatusCode, responseBody);

                // Special handling for Snapchat's limitations
                return new PublishResult
                {
                    Success = false,
                    Error = $"Snapchat Marketing API error: {response.StatusCode}. Note: Snapchat's public API is primarily for advertising. Organic posting requires the Snapchat app.",
                    ErrorCode = GetErrorCode(response.StatusCode.ToString()),
                    Metadata = new Dictionary<string, string>
                    {
                        ["status_code"] = ((int)response.StatusCode).ToString(),
                        ["response"] = responseBody,
                        ["note"] = "Snapchat Marketing API is for advertising only"
                    }
                };
            }

            var creativeResponse = JsonSerializer.Deserialize<SnapchatCreativeResponse>(responseBody);
            var creativeId = creativeResponse?.creatives?.FirstOrDefault()?.creative?.id;

            if (string.IsNullOrEmpty(creativeId))
            {
                return new PublishResult
                {
                    Success = false,
                    Error = "Invalid response from Snapchat",
                    ErrorCode = "invalid_response"
                };
            }

            _logger.LogInformation("[Snapchat] Creative published successfully: {CreativeId}", creativeId);

            return new PublishResult
            {
                Success = true,
                ExternalPostId = creativeId,
                Url = $"https://ads.snapchat.com/creative/{creativeId}",
                PublishedAt = DateTime.UtcNow,
                Metadata = new Dictionary<string, string>
                {
                    ["creative_id"] = creativeId,
                    ["ad_account_id"] = adAccountId,
                    ["type"] = "SNAP_AD",
                    ["note"] = "Created via Marketing API - requires campaign association for visibility"
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Snapchat] Error publishing creative");
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
            _logger.LogInformation("[Snapchat] Deleting creative: {CreativeId}", externalPostId);

            // Get ad account ID
            var adAccountId = await GetAdAccountIdAsync(accessToken, cancellationToken);
            if (string.IsNullOrEmpty(adAccountId))
            {
                _logger.LogWarning("[Snapchat] No ad account found for deletion");
                return false;
            }

            var httpRequest = new HttpRequestMessage(HttpMethod.Delete,
                $"{ApiBaseUrl}/adaccounts/{adAccountId}/creatives/{externalPostId}");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("[Snapchat] Creative deleted successfully: {CreativeId}", externalPostId);
                return true;
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("[Snapchat] Delete failed: {Status} - {Response}",
                response.StatusCode, responseBody);

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Snapchat] Error deleting creative: {CreativeId}", externalPostId);
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
            _logger.LogInformation("[Snapchat] Fetching metrics for creative: {CreativeId}", externalPostId);

            // Get ad account ID
            var adAccountId = await GetAdAccountIdAsync(accessToken, cancellationToken);
            if (string.IsNullOrEmpty(adAccountId))
            {
                _logger.LogWarning("[Snapchat] No ad account found for metrics");
                return new PostMetrics
                {
                    ExternalPostId = externalPostId,
                    FetchedAt = DateTime.UtcNow
                };
            }

            // Snapchat stats are available at ad/campaign level, not creative level
            // Creatives need to be part of an active campaign to get metrics
            var httpRequest = new HttpRequestMessage(HttpMethod.Get,
                $"{ApiBaseUrl}/adaccounts/{adAccountId}/creatives/{externalPostId}/stats");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[Snapchat] Metrics fetch failed: {Status}", response.StatusCode);
                return new PostMetrics
                {
                    ExternalPostId = externalPostId,
                    FetchedAt = DateTime.UtcNow,
                    Metadata = new Dictionary<string, string>
                    {
                        ["note"] = "Metrics available only when creative is part of an active campaign"
                    }
                };
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var statsResponse = JsonSerializer.Deserialize<SnapchatStatsResponse>(responseBody);

            var stats = statsResponse?.timeseries_stats?.FirstOrDefault()?.stats;

            return new PostMetrics
            {
                ExternalPostId = externalPostId,
                Views = (int)(stats?.impressions ?? 0),
                Likes = (int)(stats?.swipes ?? 0),
                Shares = (int)(stats?.shares ?? 0),
                Comments = 0, // Snapchat doesn't provide comment count via API
                FetchedAt = DateTime.UtcNow,
                Metadata = new Dictionary<string, string>
                {
                    ["impressions"] = (stats?.impressions ?? 0).ToString(),
                    ["swipes"] = (stats?.swipes ?? 0).ToString(),
                    ["spend"] = (stats?.spend ?? 0).ToString(),
                    ["video_views"] = (stats?.video_views ?? 0).ToString()
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Snapchat] Error fetching metrics for creative: {CreativeId}", externalPostId);
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
            _logger.LogInformation("[Snapchat] Validating access token");

            var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiBaseUrl}/me");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("[Snapchat] Access token is valid");
                return true;
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("[Snapchat] Access validation failed: {Status} - {Response}",
                response.StatusCode, responseBody);

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Snapchat] Error validating access token");
            return false;
        }
    }

    private async Task<string> GetAdAccountIdAsync(string accessToken, CancellationToken cancellationToken)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiBaseUrl}/me/adaccounts");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[Snapchat] Failed to get ad accounts: {Response}", json);
                return "";
            }

            var adAccountsResponse = JsonSerializer.Deserialize<SnapchatAdAccountsResponse>(json);
            var firstAccount = adAccountsResponse?.adaccounts?.FirstOrDefault();

            return firstAccount?.adaccount?.id ?? "";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Snapchat] Error getting ad account ID");
            return "";
        }
    }

    private string ExtractTitle(string content)
    {
        // Try to extract first line or first sentence as title
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var firstLine = lines.FirstOrDefault() ?? "Untitled";

        // Snapchat headline limit is typically shorter
        if (firstLine.Length > 100)
        {
            return firstLine.Substring(0, 97) + "...";
        }

        return firstLine;
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

    // Snapchat API response classes
    private class SnapchatAdAccountsResponse
    {
        public List<SnapchatAdAccountWrapper>? adaccounts { get; set; }
    }

    private class SnapchatAdAccountWrapper
    {
        public SnapchatAdAccount? adaccount { get; set; }
    }

    private class SnapchatAdAccount
    {
        public string? id { get; set; }
        public string? name { get; set; }
    }

    private class SnapchatCreativeResponse
    {
        public List<SnapchatCreativeWrapper>? creatives { get; set; }
    }

    private class SnapchatCreativeWrapper
    {
        public SnapchatCreative? creative { get; set; }
    }

    private class SnapchatCreative
    {
        public string? id { get; set; }
        public string? name { get; set; }
        public string? type { get; set; }
    }

    private class SnapchatStatsResponse
    {
        public List<SnapchatTimeseriesStats>? timeseries_stats { get; set; }
    }

    private class SnapchatTimeseriesStats
    {
        public SnapchatStats? stats { get; set; }
    }

    private class SnapchatStats
    {
        public long impressions { get; set; }
        public long swipes { get; set; }
        public long shares { get; set; }
        public long video_views { get; set; }
        public decimal spend { get; set; }
    }
}
