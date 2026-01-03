using System.Net.Http.Headers;
using System.Text.Json;
using System.Web;
using Microsoft.Extensions.Logging;
using Hazina.Tools.Services.Social.Abstractions;

namespace Hazina.Tools.Services.Social.Providers;

/// <summary>
/// Pinterest social media provider implementation.
/// Supports OAuth 2.0 authentication and content import.
/// Requires Pinterest App with API access.
/// </summary>
public class PinterestProvider : ISocialProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PinterestProvider> _logger;
    private readonly string _clientId;
    private readonly string _clientSecret;

    private const string AuthorizeUrl = "https://www.pinterest.com/oauth/";
    private const string TokenUrl = "https://api.pinterest.com/v5/oauth/token";
    private const string ApiBaseUrl = "https://api.pinterest.com/v5";

    public string ProviderId => "pinterest";
    public string DisplayName => "Pinterest";

    public PinterestProvider(
        HttpClient httpClient,
        ILogger<PinterestProvider> logger,
        string clientId,
        string clientSecret)
    {
        _httpClient = httpClient;
        _logger = logger;
        _clientId = clientId;
        _clientSecret = clientSecret;
    }

    public string GetAuthorizationUrl(string redirectUri, string state)
    {
        var scopes = "boards:read pins:read user_accounts:read";
        var encodedRedirect = HttpUtility.UrlEncode(redirectUri);
        var encodedScopes = HttpUtility.UrlEncode(scopes);

        return $"{AuthorizeUrl}?client_id={_clientId}&redirect_uri={encodedRedirect}&response_type=code&scope={encodedScopes}&state={state}";
    }

    public async Task<SocialAuthResult> ExchangeCodeAsync(
        string code,
        string redirectUri,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = redirectUri
            });

            // Pinterest requires Basic Auth
            var credentials = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{_clientId}:{_clientSecret}"));
            var request = new HttpRequestMessage(HttpMethod.Post, TokenUrl) { Content = content };
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Pinterest token exchange failed: {Response}", json);
                return new SocialAuthResult
                {
                    Success = false,
                    Error = $"Token exchange failed: {response.StatusCode}"
                };
            }

            var tokenResponse = JsonSerializer.Deserialize<PinterestTokenResponse>(json);
            if (tokenResponse?.access_token == null)
            {
                return new SocialAuthResult { Success = false, Error = "Invalid token response" };
            }

            return new SocialAuthResult
            {
                Success = true,
                AccessToken = tokenResponse.access_token,
                RefreshToken = tokenResponse.refresh_token,
                ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.expires_in)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exchanging Pinterest auth code");
            return new SocialAuthResult { Success = false, Error = ex.Message };
        }
    }

    public async Task<SocialAuthResult> RefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken
            });

            var credentials = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{_clientId}:{_clientSecret}"));
            var request = new HttpRequestMessage(HttpMethod.Post, TokenUrl) { Content = content };
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new SocialAuthResult
                {
                    Success = false,
                    Error = $"Token refresh failed: {response.StatusCode}"
                };
            }

            var tokenResponse = JsonSerializer.Deserialize<PinterestTokenResponse>(json);
            if (tokenResponse?.access_token == null)
            {
                return new SocialAuthResult { Success = false, Error = "Invalid token response" };
            }

            return new SocialAuthResult
            {
                Success = true,
                AccessToken = tokenResponse.access_token,
                RefreshToken = tokenResponse.refresh_token,
                ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.expires_in)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing Pinterest token");
            return new SocialAuthResult { Success = false, Error = ex.Message };
        }
    }

    public async Task<SocialProfile> GetProfileAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{ApiBaseUrl}/user_account";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Pinterest profile fetch failed: {Response}", json);
                return new SocialProfile { Id = "", Name = "Unknown" };
            }

            var userResponse = JsonSerializer.Deserialize<PinterestUserResponse>(json);
            if (userResponse == null)
            {
                return new SocialProfile { Id = "", Name = "Unknown" };
            }

            return new SocialProfile
            {
                Id = userResponse.username ?? "",
                Name = userResponse.business_name ?? userResponse.username ?? "Unknown",
                ProfileUrl = userResponse.profile_url ?? $"https://www.pinterest.com/{userResponse.username}",
                AvatarUrl = userResponse.profile_image,
                Metadata = new Dictionary<string, string>
                {
                    ["account_type"] = userResponse.account_type ?? "",
                    ["follower_count"] = userResponse.follower_count?.ToString() ?? "0",
                    ["following_count"] = userResponse.following_count?.ToString() ?? "0",
                    ["pin_count"] = userResponse.pin_count?.ToString() ?? "0",
                    ["board_count"] = userResponse.board_count?.ToString() ?? "0"
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Pinterest profile");
            return new SocialProfile { Id = "", Name = "Unknown" };
        }
    }

    public async Task<SocialImportResult> ImportContentAsync(
        string accessToken,
        SocialImportOptions options,
        CancellationToken cancellationToken = default)
    {
        var result = new SocialImportResult { Success = true };

        try
        {
            var profile = await GetProfileAsync(accessToken, cancellationToken);
            var username = profile.Id;

            if (options.ContentTypes.Contains("posts"))
            {
                var pins = await ImportPinsAsync(accessToken, username, options, cancellationToken);
                result.Posts.AddRange(pins);
            }

            result.TotalImported = result.Posts.Count + result.Articles.Count;
            _logger.LogInformation("Imported {Count} items from Pinterest", result.TotalImported);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing Pinterest content");
            result.Success = false;
            result.Error = ex.Message;
        }

        return result;
    }

    private async Task<List<SocialPost>> ImportPinsAsync(
        string accessToken,
        string username,
        SocialImportOptions options,
        CancellationToken cancellationToken)
    {
        var posts = new List<SocialPost>();

        try
        {
            // First get boards
            var boardsUrl = $"{ApiBaseUrl}/boards?page_size=25";
            var boardsRequest = new HttpRequestMessage(HttpMethod.Get, boardsUrl);
            boardsRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var boardsResponse = await _httpClient.SendAsync(boardsRequest, cancellationToken);
            var boardsJson = await boardsResponse.Content.ReadAsStringAsync(cancellationToken);

            if (!boardsResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("Pinterest boards fetch failed: {Status} - {Response}",
                    boardsResponse.StatusCode, boardsJson);
                return posts;
            }

            var boardsData = JsonSerializer.Deserialize<PinterestBoardsResponse>(boardsJson);
            if (boardsData?.items == null)
            {
                return posts;
            }

            // Import pins from each board (limit to first 5 boards)
            foreach (var board in boardsData.items.Take(5))
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var boardPins = await ImportBoardPinsAsync(
                    accessToken,
                    board.id ?? "",
                    board.name ?? "Board",
                    username,
                    options,
                    cancellationToken);

                posts.AddRange(boardPins);

                if (posts.Count >= options.MaxItems)
                    break;
            }

            _logger.LogInformation("Imported {Count} pins from Pinterest", posts.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error fetching Pinterest pins");
        }

        return posts.Take(options.MaxItems).ToList();
    }

    private async Task<List<SocialPost>> ImportBoardPinsAsync(
        string accessToken,
        string boardId,
        string boardName,
        string username,
        SocialImportOptions options,
        CancellationToken cancellationToken)
    {
        var posts = new List<SocialPost>();

        try
        {
            var url = $"{ApiBaseUrl}/boards/{boardId}/pins?page_size=25";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Pinterest pins fetch failed for board {Board}: {Status} - {Response}",
                    boardName, response.StatusCode, json);
                return posts;
            }

            var pinsData = JsonSerializer.Deserialize<PinterestPinsResponse>(json);
            if (pinsData?.items == null)
            {
                return posts;
            }

            foreach (var pin in pinsData.items)
            {
                var createdAt = DateTime.Parse(pin.created_at ?? DateTime.UtcNow.ToString());
                if (options.Since.HasValue && createdAt < options.Since.Value)
                    continue;

                var post = new SocialPost
                {
                    Id = pin.id ?? "",
                    AccountId = username,
                    Content = pin.description ?? pin.title ?? "",
                    CreatedAt = createdAt,
                    Url = pin.link ?? $"https://www.pinterest.com/pin/{pin.id}",
                    Metadata = new Dictionary<string, string>
                    {
                        ["source"] = boardName,
                        ["board_id"] = boardId,
                        ["title"] = pin.title ?? "",
                        ["media_type"] = pin.media?.media_type ?? "IMAGE",
                        ["image_url"] = pin.media?.images?.Size600x?.url ?? "",
                        ["alt_text"] = pin.alt_text ?? ""
                    }
                };

                posts.Add(post);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error fetching pins for board {Board}", boardName);
        }

        return posts;
    }

    public async Task<bool> RevokeAccessAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        // Pinterest doesn't have a documented revoke endpoint in v5 API
        // Access tokens expire naturally
        await Task.CompletedTask;
        return true;
    }

    // Pinterest API response classes
    private class PinterestTokenResponse
    {
        public string? access_token { get; set; }
        public string? refresh_token { get; set; }
        public int expires_in { get; set; }
        public string? token_type { get; set; }
        public string? scope { get; set; }
    }

    private class PinterestUserResponse
    {
        public string? account_type { get; set; }
        public string? profile_image { get; set; }
        public string? website_url { get; set; }
        public string? username { get; set; }
        public string? business_name { get; set; }
        public int? board_count { get; set; }
        public int? pin_count { get; set; }
        public int? follower_count { get; set; }
        public int? following_count { get; set; }
        public int? monthly_views { get; set; }
        public string? profile_url { get; set; }
    }

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
        public int? pin_count { get; set; }
    }

    private class PinterestPinsResponse
    {
        public List<PinterestPin>? items { get; set; }
        public string? bookmark { get; set; }
    }

    private class PinterestPin
    {
        public string? id { get; set; }
        public string? created_at { get; set; }
        public string? link { get; set; }
        public string? title { get; set; }
        public string? description { get; set; }
        public string? alt_text { get; set; }
        public string? board_id { get; set; }
        public PinterestMedia? media { get; set; }
    }

    private class PinterestMedia
    {
        public string? media_type { get; set; }
        public PinterestImages? images { get; set; }
    }

    private class PinterestImages
    {
        [System.Text.Json.Serialization.JsonPropertyName("150x150")]
        public PinterestImageSize? Size150x150 { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("400x300")]
        public PinterestImageSize? Size400x300 { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("600x")]
        public PinterestImageSize? Size600x { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("1200x")]
        public PinterestImageSize? Size1200x { get; set; }
    }

    private class PinterestImageSize
    {
        public int width { get; set; }
        public int height { get; set; }
        public string? url { get; set; }
    }
}
