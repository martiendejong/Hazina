using System.Net.Http.Headers;
using System.Text.Json;
using System.Web;
using Microsoft.Extensions.Logging;
using Hazina.Tools.Services.Social.Abstractions;

namespace Hazina.Tools.Services.Social.Providers;

/// <summary>
/// YouTube social media provider implementation.
/// Supports OAuth 2.0 authentication and content import.
/// Requires Google Cloud project with YouTube Data API v3 enabled.
/// </summary>
public class YouTubeProvider : ISocialProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<YouTubeProvider> _logger;
    private readonly string _clientId;
    private readonly string _clientSecret;

    private const string AuthorizeUrl = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string TokenUrl = "https://oauth2.googleapis.com/token";
    private const string RevokeUrl = "https://oauth2.googleapis.com/revoke";
    private const string ApiBaseUrl = "https://www.googleapis.com/youtube/v3";

    public string ProviderId => "youtube";
    public string DisplayName => "YouTube";

    public YouTubeProvider(
        HttpClient httpClient,
        ILogger<YouTubeProvider> logger,
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
        var scopes = "https://www.googleapis.com/auth/youtube.readonly https://www.googleapis.com/auth/userinfo.profile https://www.googleapis.com/auth/userinfo.email";
        var encodedRedirect = HttpUtility.UrlEncode(redirectUri);
        var encodedScopes = HttpUtility.UrlEncode(scopes);

        return $"{AuthorizeUrl}?client_id={_clientId}&redirect_uri={encodedRedirect}&response_type=code&scope={encodedScopes}&state={state}&access_type=offline&prompt=consent";
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
                ["code"] = code,
                ["client_id"] = _clientId,
                ["client_secret"] = _clientSecret,
                ["redirect_uri"] = redirectUri,
                ["grant_type"] = "authorization_code"
            });

            var response = await _httpClient.PostAsync(TokenUrl, content, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("YouTube token exchange failed: {Response}", json);
                return new SocialAuthResult
                {
                    Success = false,
                    Error = $"Token exchange failed: {response.StatusCode}"
                };
            }

            var tokenResponse = JsonSerializer.Deserialize<YouTubeTokenResponse>(json);
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
            _logger.LogError(ex, "Error exchanging YouTube auth code");
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
                ["client_id"] = _clientId,
                ["client_secret"] = _clientSecret,
                ["refresh_token"] = refreshToken,
                ["grant_type"] = "refresh_token"
            });

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

            var tokenResponse = JsonSerializer.Deserialize<YouTubeTokenResponse>(json);
            if (tokenResponse?.access_token == null)
            {
                return new SocialAuthResult { Success = false, Error = "Invalid token response" };
            }

            return new SocialAuthResult
            {
                Success = true,
                AccessToken = tokenResponse.access_token,
                RefreshToken = refreshToken, // Refresh token not returned in refresh
                ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.expires_in)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing YouTube token");
            return new SocialAuthResult { Success = false, Error = ex.Message };
        }
    }

    public async Task<SocialProfile> GetProfileAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get channel info for the authenticated user
            var url = $"{ApiBaseUrl}/channels?part=snippet,statistics&mine=true&access_token={accessToken}";

            var response = await _httpClient.GetAsync(url, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("YouTube profile fetch failed: {Response}", json);
                return new SocialProfile { Id = "", Name = "Unknown" };
            }

            var channelResponse = JsonSerializer.Deserialize<YouTubeChannelsResponse>(json);
            if (channelResponse?.items == null || channelResponse.items.Count == 0)
            {
                return new SocialProfile { Id = "", Name = "Unknown" };
            }

            var channel = channelResponse.items[0];
            return new SocialProfile
            {
                Id = channel.id ?? "",
                Name = channel.snippet?.title ?? "Unknown",
                ProfileUrl = $"https://www.youtube.com/channel/{channel.id}",
                AvatarUrl = channel.snippet?.thumbnails?.@default?.url,
                Metadata = new Dictionary<string, string>
                {
                    ["channel_title"] = channel.snippet?.title ?? "",
                    ["description"] = channel.snippet?.description ?? "",
                    ["subscriber_count"] = channel.statistics?.subscriberCount ?? "",
                    ["video_count"] = channel.statistics?.videoCount ?? "",
                    ["view_count"] = channel.statistics?.viewCount ?? ""
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting YouTube profile");
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
            var channelTitle = profile.Metadata.TryGetValue("channel_title", out var title) ? title : "YouTube";

            if (options.ContentTypes.Contains("posts"))
            {
                var videos = await ImportVideosAsync(accessToken, profile.Id, channelTitle, options, cancellationToken);
                result.Posts.AddRange(videos);
            }

            result.TotalImported = result.Posts.Count + result.Articles.Count;
            _logger.LogInformation("Imported {Count} items from YouTube channel", result.TotalImported);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing YouTube content");
            result.Success = false;
            result.Error = ex.Message;
        }

        return result;
    }

    private async Task<List<SocialPost>> ImportVideosAsync(
        string accessToken,
        string channelId,
        string channelTitle,
        SocialImportOptions options,
        CancellationToken cancellationToken)
    {
        var posts = new List<SocialPost>();

        try
        {
            // First, get the "uploads" playlist ID
            var channelUrl = $"{ApiBaseUrl}/channels?part=contentDetails&id={channelId}&access_token={accessToken}";
            var channelResponse = await _httpClient.GetAsync(channelUrl, cancellationToken);
            var channelJson = await channelResponse.Content.ReadAsStringAsync(cancellationToken);

            if (!channelResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("YouTube channel fetch failed: {Status} - {Response}",
                    channelResponse.StatusCode, channelJson);
                return posts;
            }

            var channelData = JsonSerializer.Deserialize<YouTubeChannelsResponse>(channelJson);
            var uploadsPlaylistId = channelData?.items?[0]?.contentDetails?.relatedPlaylists?.uploads;

            if (string.IsNullOrEmpty(uploadsPlaylistId))
            {
                _logger.LogWarning("No uploads playlist found for channel {ChannelId}", channelId);
                return posts;
            }

            // Get videos from uploads playlist
            var playlistUrl = $"{ApiBaseUrl}/playlistItems?part=snippet,contentDetails&playlistId={uploadsPlaylistId}&maxResults={Math.Min(options.MaxItems, 50)}&access_token={accessToken}";
            var playlistResponse = await _httpClient.GetAsync(playlistUrl, cancellationToken);
            var playlistJson = await playlistResponse.Content.ReadAsStringAsync(cancellationToken);

            if (!playlistResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("YouTube playlist fetch failed: {Status} - {Response}",
                    playlistResponse.StatusCode, playlistJson);
                return posts;
            }

            var playlistData = JsonSerializer.Deserialize<YouTubePlaylistItemsResponse>(playlistJson);
            if (playlistData?.items == null)
            {
                return posts;
            }

            // Get video IDs for statistics
            var videoIds = string.Join(",", playlistData.items
                .Where(i => i.contentDetails?.videoId != null)
                .Select(i => i.contentDetails!.videoId));

            if (string.IsNullOrEmpty(videoIds))
            {
                return posts;
            }

            // Get video statistics
            var videosUrl = $"{ApiBaseUrl}/videos?part=statistics,contentDetails&id={videoIds}&access_token={accessToken}";
            var videosResponse = await _httpClient.GetAsync(videosUrl, cancellationToken);
            var videosJson = await videosResponse.Content.ReadAsStringAsync(cancellationToken);

            var videosData = JsonSerializer.Deserialize<YouTubeVideosResponse>(videosJson);
            var statsLookup = videosData?.items?.ToDictionary(v => v.id ?? "", v => v) ?? new Dictionary<string, YouTubeVideo>();

            // Create posts from playlist items with statistics
            foreach (var item in playlistData.items)
            {
                var videoId = item.contentDetails?.videoId;
                if (string.IsNullOrEmpty(videoId))
                    continue;

                var publishedAt = DateTime.Parse(item.snippet?.publishedAt ?? DateTime.UtcNow.ToString());
                if (options.Since.HasValue && publishedAt < options.Since.Value)
                    continue;

                statsLookup.TryGetValue(videoId, out var videoStats);

                var post = new SocialPost
                {
                    Id = videoId,
                    AccountId = channelId,
                    Content = item.snippet?.description ?? "",
                    CreatedAt = publishedAt,
                    Url = $"https://www.youtube.com/watch?v={videoId}",
                    Metadata = new Dictionary<string, string>
                    {
                        ["source"] = channelTitle,
                        ["title"] = item.snippet?.title ?? "",
                        ["thumbnail_url"] = item.snippet?.thumbnails?.high?.url ?? item.snippet?.thumbnails?.medium?.url ?? "",
                        ["media_type"] = "VIDEO",
                        ["views"] = videoStats?.statistics?.viewCount ?? "0",
                        ["likes"] = videoStats?.statistics?.likeCount ?? "0",
                        ["comments"] = videoStats?.statistics?.commentCount ?? "0",
                        ["duration"] = videoStats?.contentDetails?.duration ?? ""
                    }
                };

                posts.Add(post);
            }

            _logger.LogInformation("Imported {Count} videos from YouTube channel {Channel}",
                posts.Count, channelTitle);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error fetching YouTube videos for channel {Channel}", channelTitle);
        }

        return posts;
    }

    public async Task<bool> RevokeAccessAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{RevokeUrl}?token={accessToken}";
            var response = await _httpClient.PostAsync(url, null, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking YouTube access");
            return false;
        }
    }

    // YouTube API response classes
    private class YouTubeTokenResponse
    {
        public string? access_token { get; set; }
        public string? refresh_token { get; set; }
        public int expires_in { get; set; }
        public string? token_type { get; set; }
        public string? scope { get; set; }
    }

    private class YouTubeChannelsResponse
    {
        public List<YouTubeChannel>? items { get; set; }
    }

    private class YouTubeChannel
    {
        public string? id { get; set; }
        public YouTubeChannelSnippet? snippet { get; set; }
        public YouTubeChannelStatistics? statistics { get; set; }
        public YouTubeChannelContentDetails? contentDetails { get; set; }
    }

    private class YouTubeChannelSnippet
    {
        public string? title { get; set; }
        public string? description { get; set; }
        public YouTubeThumbnails? thumbnails { get; set; }
    }

    private class YouTubeChannelStatistics
    {
        public string? viewCount { get; set; }
        public string? subscriberCount { get; set; }
        public string? videoCount { get; set; }
    }

    private class YouTubeChannelContentDetails
    {
        public YouTubeRelatedPlaylists? relatedPlaylists { get; set; }
    }

    private class YouTubeRelatedPlaylists
    {
        public string? uploads { get; set; }
    }

    private class YouTubePlaylistItemsResponse
    {
        public List<YouTubePlaylistItem>? items { get; set; }
        public string? nextPageToken { get; set; }
    }

    private class YouTubePlaylistItem
    {
        public YouTubePlaylistItemSnippet? snippet { get; set; }
        public YouTubePlaylistItemContentDetails? contentDetails { get; set; }
    }

    private class YouTubePlaylistItemSnippet
    {
        public string? publishedAt { get; set; }
        public string? title { get; set; }
        public string? description { get; set; }
        public YouTubeThumbnails? thumbnails { get; set; }
    }

    private class YouTubePlaylistItemContentDetails
    {
        public string? videoId { get; set; }
    }

    private class YouTubeVideosResponse
    {
        public List<YouTubeVideo>? items { get; set; }
    }

    private class YouTubeVideo
    {
        public string? id { get; set; }
        public YouTubeVideoStatistics? statistics { get; set; }
        public YouTubeVideoContentDetails? contentDetails { get; set; }
    }

    private class YouTubeVideoStatistics
    {
        public string? viewCount { get; set; }
        public string? likeCount { get; set; }
        public string? commentCount { get; set; }
    }

    private class YouTubeVideoContentDetails
    {
        public string? duration { get; set; }
    }

    private class YouTubeThumbnails
    {
        public YouTubeThumbnail? @default { get; set; }
        public YouTubeThumbnail? medium { get; set; }
        public YouTubeThumbnail? high { get; set; }
    }

    private class YouTubeThumbnail
    {
        public string? url { get; set; }
        public int width { get; set; }
        public int height { get; set; }
    }
}
