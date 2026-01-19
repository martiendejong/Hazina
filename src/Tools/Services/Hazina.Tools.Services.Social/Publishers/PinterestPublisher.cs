using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Hazina.Tools.Services.Social.Abstractions;

namespace Hazina.Tools.Services.Social.Publishers;

/// <summary>
/// Pinterest pin publisher implementation.
/// Publishes pins to Pinterest boards using the Pinterest API v5.
/// Requires at least one image URL per pin.
/// </summary>
public class PinterestPublisher : ISocialPublisher
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PinterestPublisher> _logger;

    private const string ApiBaseUrl = "https://api.pinterest.com/v5";

    public string ProviderId => "pinterest";
    public string DisplayName => "Pinterest";

    public PinterestPublisher(
        HttpClient httpClient,
        ILogger<PinterestPublisher> logger)
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
            _logger.LogInformation("[Pinterest] Publishing pin: {PostId}", request.InternalPostId);

            // Pinterest requires at least one image
            if (!request.MediaUrls.Any())
            {
                return new PublishResult
                {
                    Success = false,
                    Error = "Pinterest pins require at least one image URL",
                    ErrorCode = "missing_media"
                };
            }

            // Get default board to post to
            var boardId = await GetDefaultBoardIdAsync(accessToken, cancellationToken);
            if (string.IsNullOrEmpty(boardId))
            {
                return new PublishResult
                {
                    Success = false,
                    Error = "Failed to get Pinterest board ID",
                    ErrorCode = "board_id_fetch_failed"
                };
            }

            var imageUrl = request.MediaUrls.First();
            var title = ExtractTitle(request.Content);
            var description = request.Content;

            // Add hashtags to description
            if (request.Hashtags.Any())
            {
                description = $"{request.Content}\n\n{string.Join(" ", request.Hashtags)}";
            }

            // Build pin payload
            var pinPayload = new
            {
                board_id = boardId,
                media_source = new
                {
                    source_type = "image_url",
                    url = imageUrl
                },
                title = title,
                description = description,
                link = request.MediaUrls.Count > 1 ? request.MediaUrls[1] : null, // Optional destination link
                alt_text = title
            };

            var json = JsonSerializer.Serialize(pinPayload);
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{ApiBaseUrl}/pins");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[Pinterest] Publish failed: {Status} - {Response}",
                    response.StatusCode, responseBody);

                return new PublishResult
                {
                    Success = false,
                    Error = $"Pinterest API error: {response.StatusCode}",
                    ErrorCode = GetErrorCode(response.StatusCode.ToString()),
                    Metadata = new Dictionary<string, string>
                    {
                        ["status_code"] = ((int)response.StatusCode).ToString(),
                        ["response"] = responseBody
                    }
                };
            }

            var pinResponse = JsonSerializer.Deserialize<PinterestPinResponse>(responseBody);
            if (pinResponse?.id == null)
            {
                return new PublishResult
                {
                    Success = false,
                    Error = "Invalid response from Pinterest",
                    ErrorCode = "invalid_response"
                };
            }

            var pinId = pinResponse.id;
            var pinUrl = pinResponse.link ?? $"https://www.pinterest.com/pin/{pinId}";

            _logger.LogInformation("[Pinterest] Pin published successfully: {PinId}", pinId);

            return new PublishResult
            {
                Success = true,
                ExternalPostId = pinId,
                Url = pinUrl,
                PublishedAt = DateTime.UtcNow,
                Metadata = new Dictionary<string, string>
                {
                    ["pin_id"] = pinId,
                    ["board_id"] = boardId,
                    ["media_url"] = imageUrl
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Pinterest] Error publishing pin");
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
            _logger.LogInformation("[Pinterest] Deleting pin: {PinId}", externalPostId);

            var httpRequest = new HttpRequestMessage(HttpMethod.Delete, $"{ApiBaseUrl}/pins/{externalPostId}");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("[Pinterest] Pin deleted successfully: {PinId}", externalPostId);
                return true;
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("[Pinterest] Delete failed: {Status} - {Response}",
                response.StatusCode, responseBody);

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Pinterest] Error deleting pin: {PinId}", externalPostId);
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
            _logger.LogInformation("[Pinterest] Fetching metrics for pin: {PinId}", externalPostId);

            var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"{ApiBaseUrl}/pins/{externalPostId}/analytics");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[Pinterest] Metrics fetch failed: {Status}", response.StatusCode);
                return new PostMetrics
                {
                    ExternalPostId = externalPostId,
                    FetchedAt = DateTime.UtcNow
                };
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var analyticsResponse = JsonSerializer.Deserialize<PinterestAnalyticsResponse>(responseBody);

            var metrics = analyticsResponse?.all?.daily?.FirstOrDefault();

            return new PostMetrics
            {
                ExternalPostId = externalPostId,
                Views = metrics?.metrics?.IMPRESSION ?? 0,
                Likes = metrics?.metrics?.PIN_CLICK ?? 0,
                Shares = metrics?.metrics?.SAVE ?? 0,
                Comments = 0, // Pinterest doesn't provide comment count in analytics
                FetchedAt = DateTime.UtcNow,
                Metadata = new Dictionary<string, string>
                {
                    ["impressions"] = (metrics?.metrics?.IMPRESSION ?? 0).ToString(),
                    ["pin_clicks"] = (metrics?.metrics?.PIN_CLICK ?? 0).ToString(),
                    ["saves"] = (metrics?.metrics?.SAVE ?? 0).ToString(),
                    ["outbound_clicks"] = (metrics?.metrics?.OUTBOUND_CLICK ?? 0).ToString()
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Pinterest] Error fetching metrics for pin: {PinId}", externalPostId);
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
            _logger.LogInformation("[Pinterest] Validating access token");

            var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiBaseUrl}/user_account");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("[Pinterest] Access token is valid");
                return true;
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("[Pinterest] Access validation failed: {Status} - {Response}",
                response.StatusCode, responseBody);

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Pinterest] Error validating access token");
            return false;
        }
    }

    private async Task<string> GetDefaultBoardIdAsync(string accessToken, CancellationToken cancellationToken)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiBaseUrl}/boards?page_size=1");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[Pinterest] Failed to get boards: {Response}", json);
                return "";
            }

            var boardsResponse = JsonSerializer.Deserialize<PinterestBoardsResponse>(json);
            var firstBoard = boardsResponse?.items?.FirstOrDefault();

            if (firstBoard == null)
            {
                _logger.LogWarning("[Pinterest] No boards found for user");
                return "";
            }

            return firstBoard.id ?? "";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Pinterest] Error getting board ID");
            return "";
        }
    }

    private string ExtractTitle(string content)
    {
        // Try to extract first line or first sentence as title
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var firstLine = lines.FirstOrDefault() ?? "Untitled";

        // Pinterest title limit is 100 characters
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

    // Pinterest API response classes
    private class PinterestBoardsResponse
    {
        public List<PinterestBoard>? items { get; set; }
        public string? bookmark { get; set; }
    }

    private class PinterestBoard
    {
        public string? id { get; set; }
        public string? name { get; set; }
        public string? description { get; set; }
    }

    private class PinterestPinResponse
    {
        public string? id { get; set; }
        public string? created_at { get; set; }
        public string? link { get; set; }
        public string? title { get; set; }
        public string? description { get; set; }
        public string? board_id { get; set; }
    }

    private class PinterestAnalyticsResponse
    {
        public PinterestAnalyticsData? all { get; set; }
    }

    private class PinterestAnalyticsData
    {
        public List<PinterestDailyMetrics>? daily { get; set; }
    }

    private class PinterestDailyMetrics
    {
        public string? date { get; set; }
        public PinterestMetricsData? metrics { get; set; }
    }

    private class PinterestMetricsData
    {
        public int IMPRESSION { get; set; }
        public int SAVE { get; set; }
        public int PIN_CLICK { get; set; }
        public int OUTBOUND_CLICK { get; set; }
    }
}
