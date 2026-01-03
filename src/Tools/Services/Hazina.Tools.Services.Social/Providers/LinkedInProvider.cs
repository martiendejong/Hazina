using System.Net.Http.Headers;
using System.Text.Json;
using System.Web;
using Microsoft.Extensions.Logging;
using Hazina.Tools.Services.Social.Abstractions;

namespace Hazina.Tools.Services.Social.Providers;

/// <summary>
/// LinkedIn social media provider implementation.
/// Supports OAuth 2.0 authentication and content import.
/// </summary>
public class LinkedInProvider : ISocialProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<LinkedInProvider> _logger;
    private readonly string _clientId;
    private readonly string _clientSecret;

    private const string AuthorizeUrl = "https://www.linkedin.com/oauth/v2/authorization";
    private const string TokenUrl = "https://www.linkedin.com/oauth/v2/accessToken";
    private const string ApiBaseUrl = "https://api.linkedin.com/v2";

    public string ProviderId => "linkedin";
    public string DisplayName => "LinkedIn";

    public LinkedInProvider(
        HttpClient httpClient,
        ILogger<LinkedInProvider> logger,
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
        var scopes = "openid profile email w_member_social r_liteprofile";
        var encodedRedirect = HttpUtility.UrlEncode(redirectUri);
        var encodedScopes = HttpUtility.UrlEncode(scopes);

        return $"{AuthorizeUrl}?response_type=code&client_id={_clientId}&redirect_uri={encodedRedirect}&state={state}&scope={encodedScopes}";
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
                ["redirect_uri"] = redirectUri,
                ["client_id"] = _clientId,
                ["client_secret"] = _clientSecret
            });

            var response = await _httpClient.PostAsync(TokenUrl, content, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("LinkedIn token exchange failed: {Response}", json);
                return new SocialAuthResult
                {
                    Success = false,
                    Error = $"Token exchange failed: {response.StatusCode}"
                };
            }

            var tokenResponse = JsonSerializer.Deserialize<LinkedInTokenResponse>(json);
            if (tokenResponse == null)
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
            _logger.LogError(ex, "Error exchanging LinkedIn auth code");
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
                ["refresh_token"] = refreshToken,
                ["client_id"] = _clientId,
                ["client_secret"] = _clientSecret
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

            var tokenResponse = JsonSerializer.Deserialize<LinkedInTokenResponse>(json);
            if (tokenResponse == null)
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
            _logger.LogError(ex, "Error refreshing LinkedIn token");
            return new SocialAuthResult { Success = false, Error = ex.Message };
        }
    }

    public async Task<SocialProfile> GetProfileAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiBaseUrl}/userinfo");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("LinkedIn profile fetch failed: {Response}", json);
            return new SocialProfile { Id = "", Name = "Unknown" };
        }

        var profile = JsonSerializer.Deserialize<LinkedInUserInfo>(json);
        if (profile == null)
        {
            return new SocialProfile { Id = "", Name = "Unknown" };
        }

        return new SocialProfile
        {
            Id = profile.sub ?? "",
            Name = profile.name ?? "",
            Email = profile.email,
            ProfileUrl = $"https://www.linkedin.com/in/{profile.sub}",
            AvatarUrl = profile.picture,
            Metadata = new Dictionary<string, string>
            {
                ["given_name"] = profile.given_name ?? "",
                ["family_name"] = profile.family_name ?? "",
                ["locale"] = profile.locale ?? ""
            }
        };
    }

    public async Task<SocialImportResult> ImportContentAsync(
        string accessToken,
        SocialImportOptions options,
        CancellationToken cancellationToken = default)
    {
        var result = new SocialImportResult { Success = true };

        try
        {
            // Get user profile first to get the URN
            var profile = await GetProfileAsync(accessToken, cancellationToken);
            var memberUrn = $"urn:li:person:{profile.Id}";

            // Import posts (shares)
            if (options.ContentTypes.Contains("posts"))
            {
                var posts = await ImportPostsAsync(accessToken, memberUrn, options, cancellationToken);
                result.Posts.AddRange(posts);
            }

            // Import articles
            if (options.ContentTypes.Contains("articles"))
            {
                var articles = await ImportArticlesAsync(accessToken, memberUrn, options, cancellationToken);
                result.Articles.AddRange(articles);
            }

            result.TotalImported = result.Posts.Count + result.Articles.Count;
            _logger.LogInformation("Imported {Count} items from LinkedIn", result.TotalImported);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing LinkedIn content");
            result.Success = false;
            result.Error = ex.Message;
        }

        return result;
    }

    private async Task<List<SocialPost>> ImportPostsAsync(
        string accessToken,
        string memberUrn,
        SocialImportOptions options,
        CancellationToken cancellationToken)
    {
        var posts = new List<SocialPost>();

        try
        {
            // LinkedIn v2 API for posts
            var url = $"{ApiBaseUrl}/shares?q=owners&owners={HttpUtility.UrlEncode(memberUrn)}&count={options.MaxItems}";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("LinkedIn posts fetch failed: {Status} - {Response}",
                    response.StatusCode, json);
                return posts;
            }

            var sharesResponse = JsonSerializer.Deserialize<LinkedInSharesResponse>(json);
            if (sharesResponse?.elements == null)
            {
                return posts;
            }

            foreach (var share in sharesResponse.elements)
            {
                var post = new SocialPost
                {
                    Id = share.id ?? "",
                    AccountId = memberUrn,
                    Content = share.text?.text ?? "",
                    CreatedAt = DateTimeOffset.FromUnixTimeMilliseconds(share.created?.time ?? 0).UtcDateTime,
                    Url = $"https://www.linkedin.com/feed/update/{share.activity}",
                    Metadata = new Dictionary<string, string>
                    {
                        ["activity"] = share.activity ?? ""
                    }
                };

                if (options.Since.HasValue && post.CreatedAt < options.Since.Value)
                    continue;

                posts.Add(post);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error fetching LinkedIn posts");
        }

        return posts;
    }

    private async Task<List<SocialArticle>> ImportArticlesAsync(
        string accessToken,
        string memberUrn,
        SocialImportOptions options,
        CancellationToken cancellationToken)
    {
        // LinkedIn articles are accessed differently and may require additional scopes
        // This is a placeholder for article import
        return new List<SocialArticle>();
    }

    public async Task<bool> RevokeAccessAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["token"] = accessToken,
                ["client_id"] = _clientId,
                ["client_secret"] = _clientSecret
            });

            var response = await _httpClient.PostAsync(
                "https://www.linkedin.com/oauth/v2/revoke",
                content,
                cancellationToken);

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking LinkedIn access");
            return false;
        }
    }

    // LinkedIn API response classes
    private class LinkedInTokenResponse
    {
        public string? access_token { get; set; }
        public string? refresh_token { get; set; }
        public int expires_in { get; set; }
    }

    private class LinkedInUserInfo
    {
        public string? sub { get; set; }
        public string? name { get; set; }
        public string? given_name { get; set; }
        public string? family_name { get; set; }
        public string? picture { get; set; }
        public string? email { get; set; }
        public string? locale { get; set; }
    }

    private class LinkedInSharesResponse
    {
        public List<LinkedInShare>? elements { get; set; }
    }

    private class LinkedInShare
    {
        public string? id { get; set; }
        public string? activity { get; set; }
        public LinkedInText? text { get; set; }
        public LinkedInCreated? created { get; set; }
    }

    private class LinkedInText
    {
        public string? text { get; set; }
    }

    private class LinkedInCreated
    {
        public long time { get; set; }
    }
}
