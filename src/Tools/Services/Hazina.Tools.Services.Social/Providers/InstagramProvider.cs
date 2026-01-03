using System.Net.Http.Headers;
using System.Text.Json;
using System.Web;
using Microsoft.Extensions.Logging;
using Hazina.Tools.Services.Social.Abstractions;

namespace Hazina.Tools.Services.Social.Providers;

/// <summary>
/// Instagram social media provider implementation.
/// Supports OAuth 2.0 authentication via Facebook Graph API and content import.
/// Requires Instagram Business or Creator account connected to a Facebook Page.
/// </summary>
public class InstagramProvider : ISocialProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<InstagramProvider> _logger;
    private readonly string _appId;
    private readonly string _appSecret;

    private const string AuthorizeUrl = "https://www.facebook.com/v18.0/dialog/oauth";
    private const string TokenUrl = "https://graph.facebook.com/v18.0/oauth/access_token";
    private const string GraphApiUrl = "https://graph.facebook.com/v18.0";

    public string ProviderId => "instagram";
    public string DisplayName => "Instagram";

    public InstagramProvider(
        HttpClient httpClient,
        ILogger<InstagramProvider> logger,
        string appId,
        string appSecret)
    {
        _httpClient = httpClient;
        _logger = logger;
        _appId = appId;
        _appSecret = appSecret;
    }

    public string GetAuthorizationUrl(string redirectUri, string state)
    {
        // Instagram requires these Facebook permissions to access Instagram Business accounts
        var scopes = "instagram_basic,instagram_content_publish,pages_show_list,pages_read_engagement,public_profile,email";
        var encodedRedirect = HttpUtility.UrlEncode(redirectUri);
        var encodedScopes = HttpUtility.UrlEncode(scopes);

        return $"{AuthorizeUrl}?client_id={_appId}&redirect_uri={encodedRedirect}&state={state}&scope={encodedScopes}&response_type=code";
    }

    public async Task<SocialAuthResult> ExchangeCodeAsync(
        string code,
        string redirectUri,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{TokenUrl}?client_id={_appId}&client_secret={_appSecret}&code={code}&redirect_uri={HttpUtility.UrlEncode(redirectUri)}";

            var response = await _httpClient.GetAsync(url, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Instagram token exchange failed: {Response}", json);
                return new SocialAuthResult
                {
                    Success = false,
                    Error = $"Token exchange failed: {response.StatusCode}"
                };
            }

            var tokenResponse = JsonSerializer.Deserialize<InstagramTokenResponse>(json);
            if (tokenResponse?.access_token == null)
            {
                return new SocialAuthResult { Success = false, Error = "Invalid token response" };
            }

            // Exchange short-lived token for long-lived token (60 days)
            var longLivedToken = await ExchangeForLongLivedTokenAsync(tokenResponse.access_token, cancellationToken);
            if (longLivedToken == null)
            {
                return new SocialAuthResult { Success = false, Error = "Failed to get long-lived token" };
            }

            return new SocialAuthResult
            {
                Success = true,
                AccessToken = longLivedToken.access_token,
                ExpiresAt = DateTime.UtcNow.AddSeconds(longLivedToken.expires_in)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exchanging Instagram auth code");
            return new SocialAuthResult { Success = false, Error = ex.Message };
        }
    }

    private async Task<InstagramLongLivedTokenResponse?> ExchangeForLongLivedTokenAsync(
        string shortLivedToken,
        CancellationToken cancellationToken)
    {
        try
        {
            var url = $"{GraphApiUrl}/oauth/access_token?grant_type=fb_exchange_token&client_id={_appId}&client_secret={_appSecret}&fb_exchange_token={shortLivedToken}";

            var response = await _httpClient.GetAsync(url, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Long-lived token exchange failed: {Response}", json);
                return null;
            }

            return JsonSerializer.Deserialize<InstagramLongLivedTokenResponse>(json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error exchanging for long-lived token");
            return null;
        }
    }

    public async Task<SocialAuthResult> RefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        // Instagram uses long-lived tokens that can be refreshed before expiry
        try
        {
            var url = $"{GraphApiUrl}/oauth/access_token?grant_type=fb_exchange_token&client_id={_appId}&client_secret={_appSecret}&fb_exchange_token={refreshToken}";

            var response = await _httpClient.GetAsync(url, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new SocialAuthResult
                {
                    Success = false,
                    Error = $"Token refresh failed: {response.StatusCode}"
                };
            }

            var tokenResponse = JsonSerializer.Deserialize<InstagramLongLivedTokenResponse>(json);
            if (tokenResponse?.access_token == null)
            {
                return new SocialAuthResult { Success = false, Error = "Invalid token response" };
            }

            return new SocialAuthResult
            {
                Success = true,
                AccessToken = tokenResponse.access_token,
                ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.expires_in)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing Instagram token");
            return new SocialAuthResult { Success = false, Error = ex.Message };
        }
    }

    public async Task<SocialProfile> GetProfileAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get Facebook user info first
            var url = $"{GraphApiUrl}/me?fields=id,name,email&access_token={accessToken}";
            var response = await _httpClient.GetAsync(url, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Instagram profile fetch failed: {Response}", json);
                return new SocialProfile { Id = "", Name = "Unknown" };
            }

            var userInfo = JsonSerializer.Deserialize<InstagramUserInfo>(json);
            if (userInfo == null)
            {
                return new SocialProfile { Id = "", Name = "Unknown" };
            }

            // Try to get Instagram Business Account info
            var instagramAccounts = await GetInstagramAccountsAsync(accessToken, cancellationToken);
            var firstAccount = instagramAccounts.FirstOrDefault();

            return new SocialProfile
            {
                Id = userInfo.id ?? "",
                Name = firstAccount?.username ?? userInfo.name ?? "Unknown",
                Email = userInfo.email,
                ProfileUrl = firstAccount != null ? $"https://www.instagram.com/{firstAccount.username}" : "",
                AvatarUrl = firstAccount?.profile_picture_url,
                Metadata = new Dictionary<string, string>
                {
                    ["facebook_id"] = userInfo.id ?? "",
                    ["instagram_accounts_count"] = instagramAccounts.Count.ToString()
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Instagram profile");
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
            // Get all Instagram Business/Creator accounts connected to user's Facebook Pages
            var accounts = await GetInstagramAccountsAsync(accessToken, cancellationToken);

            if (accounts.Count == 0)
            {
                _logger.LogWarning("No Instagram Business accounts found");
                result.Success = false;
                result.Error = "No Instagram Business or Creator accounts found. Please connect your Instagram account to a Facebook Page.";
                return result;
            }

            // Import from each Instagram account (limit to 3 to avoid rate limits)
            foreach (var account in accounts.Take(3))
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                if (options.ContentTypes.Contains("posts"))
                {
                    var posts = await ImportMediaAsync(
                        accessToken,
                        account.id,
                        account.username ?? "Instagram",
                        options,
                        cancellationToken);
                    result.Posts.AddRange(posts);
                }
            }

            result.TotalImported = result.Posts.Count + result.Articles.Count;
            _logger.LogInformation("Imported {Count} items from {AccountCount} Instagram accounts",
                result.TotalImported, accounts.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing Instagram content");
            result.Success = false;
            result.Error = ex.Message;
        }

        return result;
    }

    private async Task<List<InstagramBusinessAccount>> GetInstagramAccountsAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get Facebook Pages first
            var pagesUrl = $"{GraphApiUrl}/me/accounts?fields=id,name,instagram_business_account&access_token={accessToken}";
            var response = await _httpClient.GetAsync(pagesUrl, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to get Facebook Pages: {Status} - {Response}",
                    response.StatusCode, json);
                return new List<InstagramBusinessAccount>();
            }

            var pagesResponse = JsonSerializer.Deserialize<FacebookPagesResponse>(json);
            if (pagesResponse?.data == null)
            {
                return new List<InstagramBusinessAccount>();
            }

            var instagramAccounts = new List<InstagramBusinessAccount>();

            // For each page with an Instagram Business Account, get details
            foreach (var page in pagesResponse.data.Where(p => p.instagram_business_account != null))
            {
                var igAccountId = page.instagram_business_account!.id;
                var igUrl = $"{GraphApiUrl}/{igAccountId}?fields=id,username,profile_picture_url,followers_count,media_count&access_token={accessToken}";

                var igResponse = await _httpClient.GetAsync(igUrl, cancellationToken);
                var igJson = await igResponse.Content.ReadAsStringAsync(cancellationToken);

                if (igResponse.IsSuccessStatusCode)
                {
                    var igAccount = JsonSerializer.Deserialize<InstagramBusinessAccount>(igJson);
                    if (igAccount != null)
                    {
                        instagramAccounts.Add(igAccount);
                    }
                }
            }

            _logger.LogInformation("Found {Count} Instagram Business accounts", instagramAccounts.Count);
            return instagramAccounts;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error fetching Instagram accounts");
            return new List<InstagramBusinessAccount>();
        }
    }

    private async Task<List<SocialPost>> ImportMediaAsync(
        string accessToken,
        string instagramAccountId,
        string accountName,
        SocialImportOptions options,
        CancellationToken cancellationToken)
    {
        var posts = new List<SocialPost>();

        try
        {
            var url = $"{GraphApiUrl}/{instagramAccountId}/media?fields=id,caption,media_type,media_url,permalink,timestamp,like_count,comments_count&limit={options.MaxItems}&access_token={accessToken}";

            var response = await _httpClient.GetAsync(url, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Instagram media fetch failed for {Account}: {Status} - {Response}",
                    accountName, response.StatusCode, json);
                return posts;
            }

            var mediaResponse = JsonSerializer.Deserialize<InstagramMediaResponse>(json);
            if (mediaResponse?.data == null)
            {
                return posts;
            }

            foreach (var media in mediaResponse.data)
            {
                var post = new SocialPost
                {
                    Id = media.id ?? "",
                    AccountId = instagramAccountId,
                    Content = media.caption ?? "",
                    CreatedAt = DateTime.Parse(media.timestamp ?? DateTime.UtcNow.ToString()),
                    Url = media.permalink ?? "",
                    Metadata = new Dictionary<string, string>
                    {
                        ["media_type"] = media.media_type ?? "IMAGE",
                        ["media_url"] = media.media_url ?? "",
                        ["source"] = accountName,
                        ["likes"] = media.like_count?.ToString() ?? "0",
                        ["comments"] = media.comments_count?.ToString() ?? "0"
                    }
                };

                if (options.Since.HasValue && post.CreatedAt < options.Since.Value)
                    continue;

                posts.Add(post);
            }

            _logger.LogInformation("Imported {Count} media from Instagram account {Account}",
                posts.Count, accountName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error fetching Instagram media for {Account}", accountName);
        }

        return posts;
    }

    public async Task<bool> RevokeAccessAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Revoke via Facebook Graph API
            var url = $"{GraphApiUrl}/me/permissions?access_token={accessToken}";
            var response = await _httpClient.DeleteAsync(url, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking Instagram access");
            return false;
        }
    }

    // Instagram/Facebook API response classes
    private class InstagramTokenResponse
    {
        public string? access_token { get; set; }
        public string? token_type { get; set; }
    }

    private class InstagramLongLivedTokenResponse
    {
        public string access_token { get; set; } = "";
        public string? token_type { get; set; }
        public int expires_in { get; set; }
    }

    private class InstagramUserInfo
    {
        public string? id { get; set; }
        public string? name { get; set; }
        public string? email { get; set; }
    }

    private class FacebookPagesResponse
    {
        public List<FacebookPage>? data { get; set; }
    }

    private class FacebookPage
    {
        public string? id { get; set; }
        public string? name { get; set; }
        public InstagramBusinessAccountRef? instagram_business_account { get; set; }
    }

    private class InstagramBusinessAccountRef
    {
        public string id { get; set; } = "";
    }

    private class InstagramBusinessAccount
    {
        public string id { get; set; } = "";
        public string? username { get; set; }
        public string? profile_picture_url { get; set; }
        public int? followers_count { get; set; }
        public int? media_count { get; set; }
    }

    private class InstagramMediaResponse
    {
        public List<InstagramMedia>? data { get; set; }
    }

    private class InstagramMedia
    {
        public string? id { get; set; }
        public string? caption { get; set; }
        public string? media_type { get; set; }
        public string? media_url { get; set; }
        public string? permalink { get; set; }
        public string? timestamp { get; set; }
        public int? like_count { get; set; }
        public int? comments_count { get; set; }
    }
}
