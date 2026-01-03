using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Web;
using Microsoft.Extensions.Logging;
using Hazina.Tools.Services.Social.Abstractions;

namespace Hazina.Tools.Services.Social.Providers;

/// <summary>
/// TikTok social media provider implementation.
/// Supports OAuth 2.0 authentication and content import.
/// Requires TikTok for Business account and API access.
/// </summary>
public class TikTokProvider : ISocialProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TikTokProvider> _logger;
    private readonly string _clientKey;
    private readonly string _clientSecret;

    private const string AuthorizeUrl = "https://www.tiktok.com/v2/auth/authorize";
    private const string TokenUrl = "https://open.tiktokapis.com/v2/oauth/token/";
    private const string ApiBaseUrl = "https://open.tiktokapis.com/v2";

    public string ProviderId => "tiktok";
    public string DisplayName => "TikTok";

    public TikTokProvider(
        HttpClient httpClient,
        ILogger<TikTokProvider> logger,
        string clientKey,
        string clientSecret)
    {
        _httpClient = httpClient;
        _logger = logger;
        _clientKey = clientKey;
        _clientSecret = clientSecret;
    }

    public string GetAuthorizationUrl(string redirectUri, string state)
    {
        // TikTok required scopes for content import
        var scopes = "user.info.basic,video.list,video.publish";
        var encodedRedirect = HttpUtility.UrlEncode(redirectUri);

        return $"{AuthorizeUrl}/?client_key={_clientKey}&scope={scopes}&response_type=code&redirect_uri={encodedRedirect}&state={state}";
    }

    public async Task<SocialAuthResult> ExchangeCodeAsync(
        string code,
        string redirectUri,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var requestBody = new
            {
                client_key = _clientKey,
                client_secret = _clientSecret,
                code = code,
                grant_type = "authorization_code",
                redirect_uri = redirectUri
            };

            var content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync(TokenUrl, content, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("TikTok token exchange failed: {Response}", json);
                return new SocialAuthResult
                {
                    Success = false,
                    Error = $"Token exchange failed: {response.StatusCode}"
                };
            }

            var tokenResponse = JsonSerializer.Deserialize<TikTokTokenResponse>(json);
            if (tokenResponse?.data?.access_token == null)
            {
                return new SocialAuthResult { Success = false, Error = "Invalid token response" };
            }

            return new SocialAuthResult
            {
                Success = true,
                AccessToken = tokenResponse.data.access_token,
                RefreshToken = tokenResponse.data.refresh_token,
                ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.data.expires_in)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exchanging TikTok auth code");
            return new SocialAuthResult { Success = false, Error = ex.Message };
        }
    }

    public async Task<SocialAuthResult> RefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var requestBody = new
            {
                client_key = _clientKey,
                client_secret = _clientSecret,
                grant_type = "refresh_token",
                refresh_token = refreshToken
            };

            var content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync(TokenUrl, content, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new SocialAuthResult
                {
                    Success = false,
                    Error = $"Token refresh failed: {response.StatusCode}"
                };
            }

            var tokenResponse = JsonSerializer.Deserialize<TikTokTokenResponse>(json);
            if (tokenResponse?.data?.access_token == null)
            {
                return new SocialAuthResult { Success = false, Error = "Invalid token response" };
            }

            return new SocialAuthResult
            {
                Success = true,
                AccessToken = tokenResponse.data.access_token,
                RefreshToken = tokenResponse.data.refresh_token,
                ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.data.expires_in)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing TikTok token");
            return new SocialAuthResult { Success = false, Error = ex.Message };
        }
    }

    public async Task<SocialProfile> GetProfileAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{ApiBaseUrl}/user/info/?fields=open_id,union_id,avatar_url,display_name,username";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("TikTok profile fetch failed: {Response}", json);
                return new SocialProfile { Id = "", Name = "Unknown" };
            }

            var profileResponse = JsonSerializer.Deserialize<TikTokUserInfoResponse>(json);
            if (profileResponse?.data?.user == null)
            {
                return new SocialProfile { Id = "", Name = "Unknown" };
            }

            var user = profileResponse.data.user;
            return new SocialProfile
            {
                Id = user.open_id ?? "",
                Name = user.display_name ?? user.username ?? "Unknown",
                ProfileUrl = user.username != null ? $"https://www.tiktok.com/@{user.username}" : "",
                AvatarUrl = user.avatar_url,
                Metadata = new Dictionary<string, string>
                {
                    ["union_id"] = user.union_id ?? "",
                    ["username"] = user.username ?? ""
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting TikTok profile");
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
            var username = profile.Metadata.TryGetValue("username", out var u) ? u : "TikTok";

            if (options.ContentTypes.Contains("posts"))
            {
                var videos = await ImportVideosAsync(accessToken, username, options, cancellationToken);
                result.Posts.AddRange(videos);
            }

            result.TotalImported = result.Posts.Count + result.Articles.Count;
            _logger.LogInformation("Imported {Count} items from TikTok", result.TotalImported);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing TikTok content");
            result.Success = false;
            result.Error = ex.Message;
        }

        return result;
    }

    private async Task<List<SocialPost>> ImportVideosAsync(
        string accessToken,
        string username,
        SocialImportOptions options,
        CancellationToken cancellationToken)
    {
        var posts = new List<SocialPost>();

        try
        {
            // TikTok video list endpoint
            var url = $"{ApiBaseUrl}/video/list/?fields=id,create_time,cover_image_url,share_url,video_description,duration,height,width,title,embed_link,like_count,comment_count,share_count,view_count&max_count={Math.Min(options.MaxItems, 20)}";

            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("TikTok videos fetch failed: {Status} - {Response}",
                    response.StatusCode, json);
                return posts;
            }

            var videosResponse = JsonSerializer.Deserialize<TikTokVideoListResponse>(json);
            if (videosResponse?.data?.videos == null)
            {
                return posts;
            }

            foreach (var video in videosResponse.data.videos)
            {
                var post = new SocialPost
                {
                    Id = video.id ?? "",
                    AccountId = username,
                    Content = video.video_description ?? video.title ?? "",
                    CreatedAt = DateTimeOffset.FromUnixTimeSeconds(video.create_time).UtcDateTime,
                    Url = video.share_url ?? video.embed_link ?? "",
                    Metadata = new Dictionary<string, string>
                    {
                        ["source"] = username,
                        ["media_type"] = "VIDEO",
                        ["cover_image_url"] = video.cover_image_url ?? "",
                        ["duration"] = video.duration.ToString(),
                        ["width"] = video.width.ToString(),
                        ["height"] = video.height.ToString(),
                        ["likes"] = video.like_count.ToString(),
                        ["comments"] = video.comment_count.ToString(),
                        ["shares"] = video.share_count.ToString(),
                        ["views"] = video.view_count.ToString()
                    }
                };

                if (options.Since.HasValue && post.CreatedAt < options.Since.Value)
                    continue;

                posts.Add(post);
            }

            _logger.LogInformation("Imported {Count} videos from TikTok", posts.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error fetching TikTok videos");
        }

        return posts;
    }

    public async Task<bool> RevokeAccessAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{ApiBaseUrl}/oauth/revoke/";
            var requestBody = new
            {
                client_key = _clientKey,
                client_secret = _clientSecret,
                token = accessToken
            };

            var content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync(url, content, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking TikTok access");
            return false;
        }
    }

    // TikTok API response classes
    private class TikTokTokenResponse
    {
        public TikTokTokenData? data { get; set; }
    }

    private class TikTokTokenData
    {
        public string access_token { get; set; } = "";
        public string? refresh_token { get; set; }
        public int expires_in { get; set; }
        public string? token_type { get; set; }
    }

    private class TikTokUserInfoResponse
    {
        public TikTokUserInfoData? data { get; set; }
    }

    private class TikTokUserInfoData
    {
        public TikTokUser? user { get; set; }
    }

    private class TikTokUser
    {
        public string? open_id { get; set; }
        public string? union_id { get; set; }
        public string? avatar_url { get; set; }
        public string? display_name { get; set; }
        public string? username { get; set; }
    }

    private class TikTokVideoListResponse
    {
        public TikTokVideoListData? data { get; set; }
    }

    private class TikTokVideoListData
    {
        public List<TikTokVideo>? videos { get; set; }
        public bool has_more { get; set; }
        public long cursor { get; set; }
    }

    private class TikTokVideo
    {
        public string? id { get; set; }
        public long create_time { get; set; }
        public string? cover_image_url { get; set; }
        public string? share_url { get; set; }
        public string? video_description { get; set; }
        public int duration { get; set; }
        public int height { get; set; }
        public int width { get; set; }
        public string? title { get; set; }
        public string? embed_link { get; set; }
        public int like_count { get; set; }
        public int comment_count { get; set; }
        public int share_count { get; set; }
        public int view_count { get; set; }
    }
}
