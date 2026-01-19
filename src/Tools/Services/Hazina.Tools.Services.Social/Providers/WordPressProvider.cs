using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Hazina.Tools.Services.Social.Abstractions;

namespace Hazina.Tools.Services.Social.Providers;

/// <summary>
/// WordPress provider implementation for importing content.
/// Supports self-hosted WordPress and WordPress.com sites using Application Passwords.
/// </summary>
/// <remarks>
/// WordPress uses Application Passwords instead of OAuth.
/// Authentication flow:
/// 1. User provides: website URL, username, application password
/// 2. Test connection with WordPress REST API v2
/// 3. Store credentials (application password as access token)
/// 4. Import pages, posts, and products (WooCommerce)
/// </remarks>
public class WordPressProvider : ISocialProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WordPressProvider> _logger;

    public string ProviderId => "wordpress";
    public string DisplayName => "WordPress";

    public WordPressProvider(
        HttpClient httpClient,
        ILogger<WordPressProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// WordPress doesn't use OAuth authorization URLs.
    /// Returns a placeholder URL that triggers the custom connection modal.
    /// </summary>
    public string GetAuthorizationUrl(string redirectUri, string state)
    {
        // WordPress uses Application Passwords, not OAuth
        // Return a special URL that the frontend recognizes
        return $"{redirectUri}?provider=wordpress&state={state}&auth_type=application_password";
    }

    /// <summary>
    /// WordPress doesn't use OAuth code exchange.
    /// Instead, validates credentials and tests connection.
    /// </summary>
    /// <param name="code">Format: {websiteUrl}|||{username}|||{applicationPassword}</param>
    /// <param name="redirectUri">Not used for WordPress</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task<SocialAuthResult> ExchangeCodeAsync(
        string code,
        string redirectUri,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Parse the "code" which contains: websiteUrl|||username|||applicationPassword
            var parts = code.Split("|||");
            if (parts.Length != 3)
            {
                return new SocialAuthResult
                {
                    Success = false,
                    Error = "Invalid WordPress credentials format. Expected: websiteUrl|||username|||applicationPassword"
                };
            }

            var websiteUrl = parts[0].Trim().TrimEnd('/');
            var username = parts[1].Trim();
            var applicationPassword = parts[2].Trim();

            // Validate URL format
            if (!Uri.TryCreate(websiteUrl, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                return new SocialAuthResult
                {
                    Success = false,
                    Error = "Invalid website URL. Must be a valid HTTP or HTTPS URL."
                };
            }

            // Test connection with WordPress REST API
            var testResult = await TestConnectionAsync(websiteUrl, username, applicationPassword, cancellationToken);
            if (!testResult.Success)
            {
                return testResult;
            }

            // Create access token (Format: websiteUrl|||Base64Credentials)
            // This allows us to reconstruct the full URL during import operations
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{applicationPassword}"));
            var accessToken = $"{websiteUrl}|||{credentials}";

            return new SocialAuthResult
            {
                Success = true,
                AccessToken = accessToken,
                RefreshToken = null, // WordPress doesn't use refresh tokens
                ExpiresAt = null, // Application passwords don't expire
                UserId = testResult.UserId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating WordPress credentials");
            return new SocialAuthResult { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// WordPress Application Passwords don't expire and don't have refresh tokens.
    /// </summary>
    public Task<SocialAuthResult> RefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        // WordPress Application Passwords don't expire
        return Task.FromResult(new SocialAuthResult
        {
            Success = false,
            Error = "WordPress Application Passwords do not require refresh"
        });
    }

    /// <summary>
    /// Gets the connected WordPress user's profile.
    /// </summary>
    public async Task<SocialProfile> GetProfileAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Extract website URL and credentials from access token
            var (websiteUrl, credentials) = ParseAccessToken(accessToken);

            var request = new HttpRequestMessage(HttpMethod.Get, $"{websiteUrl}/wp-json/wp/v2/users/me");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("WordPress profile fetch failed: {Response}", json);
                throw new Exception($"Failed to fetch WordPress profile: {response.StatusCode}");
            }

            var user = JsonSerializer.Deserialize<WordPressUser>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (user == null)
            {
                throw new Exception("Invalid WordPress user response");
            }

            return new SocialProfile
            {
                Id = user.Id.ToString(),
                Name = user.Name ?? user.Slug ?? "WordPress User",
                Email = user.Email,
                ProfileUrl = user.Link,
                AvatarUrl = user.AvatarUrls?.ContainsKey("96") == true ? user.AvatarUrls["96"] : null,
                Headline = user.Description,
                Metadata = new Dictionary<string, string>
                {
                    ["website_url"] = websiteUrl,
                    ["username"] = user.Slug ?? "",
                    ["registered_date"] = user.RegisteredDate?.ToString("O") ?? "",
                    ["roles"] = string.Join(",", user.Roles ?? new List<string>())
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching WordPress profile");
            throw;
        }
    }

    /// <summary>
    /// Imports content from WordPress (pages, posts, products).
    /// </summary>
    public async Task<SocialImportResult> ImportContentAsync(
        string accessToken,
        SocialImportOptions options,
        CancellationToken cancellationToken = default)
    {
        var result = new SocialImportResult { Success = true };

        try
        {
            var contentTypes = options.ContentTypes ?? new HashSet<string> { "posts", "pages", "products" };

            // Import posts
            if (contentTypes.Contains("posts") || contentTypes.Contains("blogs"))
            {
                var posts = await FetchPostsAsync(accessToken, options, cancellationToken);
                result.Posts.AddRange(posts);
            }

            // Import pages
            if (contentTypes.Contains("pages"))
            {
                var pages = await FetchPagesAsync(accessToken, options, cancellationToken);
                result.Articles.AddRange(pages);
            }

            // Import products (WooCommerce)
            if (contentTypes.Contains("products"))
            {
                var products = await FetchProductsAsync(accessToken, options, cancellationToken);
                result.Articles.AddRange(products);
            }

            result.TotalImported = result.Posts.Count + result.Articles.Count;
            _logger.LogInformation("WordPress import complete: {Count} items", result.TotalImported);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing WordPress content");
            result.Success = false;
            result.Error = ex.Message;
        }

        return result;
    }

    /// <summary>
    /// WordPress Application Passwords can be revoked through the WordPress admin panel.
    /// This method returns true as we can't programmatically revoke them.
    /// </summary>
    public Task<bool> RevokeAccessAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        // WordPress Application Passwords must be revoked manually in WordPress admin
        // Return true to allow disconnection in our system
        return Task.FromResult(true);
    }

    // ============================================
    // Private Helper Methods
    // ============================================

    /// <summary>
    /// Parses the WordPress access token to extract website URL and credentials.
    /// </summary>
    /// <param name="accessToken">Format: websiteUrl|||Base64Credentials</param>
    /// <returns>Tuple of (websiteUrl, credentials)</returns>
    private static (string websiteUrl, string credentials) ParseAccessToken(string accessToken)
    {
        var parts = accessToken.Split("|||");
        if (parts.Length != 2)
        {
            throw new ArgumentException("Invalid WordPress access token format");
        }
        return (parts[0], parts[1]);
    }

    private async Task<SocialAuthResult> TestConnectionAsync(
        string websiteUrl,
        string username,
        string applicationPassword,
        CancellationToken cancellationToken)
    {
        try
        {
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{applicationPassword}"));

            var request = new HttpRequestMessage(HttpMethod.Get, $"{websiteUrl}/wp-json/wp/v2/users/me");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("WordPress connection test failed: {StatusCode} - {Response}",
                    response.StatusCode, json);

                return new SocialAuthResult
                {
                    Success = false,
                    Error = response.StatusCode == System.Net.HttpStatusCode.Unauthorized
                        ? "Invalid username or application password"
                        : $"WordPress API error: {response.StatusCode}"
                };
            }

            var user = JsonSerializer.Deserialize<WordPressUser>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return new SocialAuthResult
            {
                Success = true,
                UserId = user?.Id.ToString() ?? "",
                AccessToken = credentials
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error testing WordPress connection");
            return new SocialAuthResult
            {
                Success = false,
                Error = "Could not connect to WordPress site. Check the URL and ensure the site is accessible."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing WordPress connection");
            return new SocialAuthResult
            {
                Success = false,
                Error = $"Connection test failed: {ex.Message}"
            };
        }
    }

    private async Task<List<SocialPost>> FetchPostsAsync(
        string accessToken,
        SocialImportOptions options,
        CancellationToken cancellationToken)
    {
        var posts = new List<SocialPost>();
        var page = 1;
        var perPage = Math.Min(options.MaxItems, 100);

        // Extract website URL and credentials
        var (websiteUrl, credentials) = ParseAccessToken(accessToken);

        try
        {
            while (posts.Count < options.MaxItems)
            {
                var request = new HttpRequestMessage(
                    HttpMethod.Get,
                    $"{websiteUrl}/wp-json/wp/v2/posts?per_page={perPage}&page={page}&_embed");
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

                var response = await _httpClient.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    break;
                }

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var wpPosts = JsonSerializer.Deserialize<List<WordPressPost>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (wpPosts == null || wpPosts.Count == 0)
                {
                    break;
                }

                foreach (var wpPost in wpPosts)
                {
                    posts.Add(new SocialPost
                    {
                        Id = wpPost.Id.ToString(),
                        Content = StripHtml(wpPost.Content?.Rendered ?? wpPost.Excerpt?.Rendered ?? ""),
                        CreatedAt = wpPost.Date ?? DateTime.UtcNow,
                        Url = wpPost.Link,
                        MediaUrls = ExtractMediaUrls(wpPost),
                        Metadata = new Dictionary<string, string>
                        {
                            ["wordpress_id"] = wpPost.Id.ToString(),
                            ["title"] = StripHtml(wpPost.Title?.Rendered ?? ""),
                            ["slug"] = wpPost.Slug ?? "",
                            ["status"] = wpPost.Status ?? "",
                            ["type"] = "post",
                            ["full_content"] = wpPost.Content?.Rendered ?? ""
                        }
                    });

                    if (posts.Count >= options.MaxItems)
                    {
                        break;
                    }
                }

                page++;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching WordPress posts");
        }

        return posts;
    }

    private async Task<List<SocialArticle>> FetchPagesAsync(
        string accessToken,
        SocialImportOptions options,
        CancellationToken cancellationToken)
    {
        var articles = new List<SocialArticle>();
        var page = 1;
        var perPage = 100;

        // Extract website URL and credentials
        var (websiteUrl, credentials) = ParseAccessToken(accessToken);

        try
        {
            while (articles.Count < options.MaxItems)
            {
                var request = new HttpRequestMessage(
                    HttpMethod.Get,
                    $"{websiteUrl}/wp-json/wp/v2/pages?per_page={perPage}&page={page}&_embed");
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

                var response = await _httpClient.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    break;
                }

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var wpPages = JsonSerializer.Deserialize<List<WordPressPage>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (wpPages == null || wpPages.Count == 0)
                {
                    break;
                }

                foreach (var wpPage in wpPages)
                {
                    articles.Add(new SocialArticle
                    {
                        Id = wpPage.Id.ToString(),
                        Title = StripHtml(wpPage.Title?.Rendered ?? ""),
                        Content = wpPage.Content?.Rendered ?? "",
                        Summary = StripHtml(wpPage.Excerpt?.Rendered ?? ""),
                        CreatedAt = wpPage.Date ?? DateTime.UtcNow,
                        UpdatedAt = wpPage.Modified,
                        Url = wpPage.Link,
                        CoverImageUrl = ExtractFeaturedImage(wpPage),
                        Metadata = new Dictionary<string, string>
                        {
                            ["wordpress_id"] = wpPage.Id.ToString(),
                            ["slug"] = wpPage.Slug ?? "",
                            ["status"] = wpPage.Status ?? "",
                            ["type"] = "page",
                            ["parent_id"] = wpPage.Parent?.ToString() ?? "0"
                        }
                    });

                    if (articles.Count >= options.MaxItems)
                    {
                        break;
                    }
                }

                page++;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching WordPress pages");
        }

        return articles;
    }

    private async Task<List<SocialArticle>> FetchProductsAsync(
        string accessToken,
        SocialImportOptions options,
        CancellationToken cancellationToken)
    {
        var products = new List<SocialArticle>();
        var page = 1;
        var perPage = 100;

        // Extract website URL and credentials
        var (websiteUrl, credentials) = ParseAccessToken(accessToken);

        try
        {
            while (products.Count < options.MaxItems)
            {
                // WooCommerce REST API v3
                var request = new HttpRequestMessage(
                    HttpMethod.Get,
                    $"{websiteUrl}/wp-json/wc/v3/products?per_page={perPage}&page={page}");
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

                var response = await _httpClient.SendAsync(request, cancellationToken);

                // If WooCommerce is not installed, skip silently
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogInformation("WooCommerce not installed, skipping products import");
                    break;
                }

                if (!response.IsSuccessStatusCode)
                {
                    break;
                }

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var wcProducts = JsonSerializer.Deserialize<List<WooCommerceProduct>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (wcProducts == null || wcProducts.Count == 0)
                {
                    break;
                }

                foreach (var wcProduct in wcProducts)
                {
                    products.Add(new SocialArticle
                    {
                        Id = wcProduct.Id.ToString(),
                        Title = wcProduct.Name ?? "",
                        Content = wcProduct.Description ?? "",
                        Summary = wcProduct.ShortDescription ?? "",
                        CreatedAt = wcProduct.DateCreated ?? DateTime.UtcNow,
                        UpdatedAt = wcProduct.DateModified,
                        Url = wcProduct.Permalink,
                        CoverImageUrl = wcProduct.Images?.FirstOrDefault()?.Src,
                        Tags = wcProduct.Tags?.Select(t => t.Name ?? "").ToList() ?? new List<string>(),
                        Metadata = new Dictionary<string, string>
                        {
                            ["wordpress_id"] = wcProduct.Id.ToString(),
                            ["sku"] = wcProduct.Sku ?? "",
                            ["price"] = wcProduct.Price ?? "",
                            ["regular_price"] = wcProduct.RegularPrice ?? "",
                            ["status"] = wcProduct.Status ?? "",
                            ["type"] = "product",
                            ["stock_status"] = wcProduct.StockStatus ?? "",
                            ["categories"] = string.Join(",", wcProduct.Categories?.Select(c => c.Name ?? "") ?? new List<string>())
                        }
                    });

                    if (products.Count >= options.MaxItems)
                    {
                        break;
                    }
                }

                page++;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error fetching WooCommerce products (might not be installed)");
        }

        return products;
    }

    private static string StripHtml(string html)
    {
        if (string.IsNullOrEmpty(html))
            return string.Empty;

        // Basic HTML stripping - for production use HtmlAgilityPack or similar
        var result = System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", "");
        result = System.Web.HttpUtility.HtmlDecode(result);
        return result.Trim();
    }

    private static List<string> ExtractMediaUrls(WordPressPost post)
    {
        var urls = new List<string>();

        if (post.FeaturedMedia > 0 && post.Embedded?.WpFeaturedmedia != null)
        {
            var media = post.Embedded.WpFeaturedmedia.FirstOrDefault();
            if (media?.SourceUrl != null)
            {
                urls.Add(media.SourceUrl);
            }
        }

        return urls;
    }

    private static string? ExtractFeaturedImage(WordPressPage page)
    {
        if (page.FeaturedMedia > 0 && page.Embedded?.WpFeaturedmedia != null)
        {
            var media = page.Embedded.WpFeaturedmedia.FirstOrDefault();
            return media?.SourceUrl;
        }

        return null;
    }

    // ============================================
    // WordPress API Response Models
    // ============================================

    private class WordPressUser
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Slug { get; set; }
        public string? Email { get; set; }
        public string? Link { get; set; }
        public string? Description { get; set; }
        public Dictionary<string, string>? AvatarUrls { get; set; }
        public List<string>? Roles { get; set; }
        public DateTime? RegisteredDate { get; set; }
    }

    private class WordPressPost
    {
        public int Id { get; set; }
        public DateTime? Date { get; set; }
        public DateTime? Modified { get; set; }
        public string? Slug { get; set; }
        public string? Status { get; set; }
        public string? Link { get; set; }
        public WordPressRendered? Title { get; set; }
        public WordPressRendered? Content { get; set; }
        public WordPressRendered? Excerpt { get; set; }
        public int FeaturedMedia { get; set; }
        public WordPressEmbedded? Embedded { get; set; }
    }

    private class WordPressPage
    {
        public int Id { get; set; }
        public DateTime? Date { get; set; }
        public DateTime? Modified { get; set; }
        public string? Slug { get; set; }
        public string? Status { get; set; }
        public string? Link { get; set; }
        public WordPressRendered? Title { get; set; }
        public WordPressRendered? Content { get; set; }
        public WordPressRendered? Excerpt { get; set; }
        public int FeaturedMedia { get; set; }
        public int? Parent { get; set; }
        public WordPressEmbedded? Embedded { get; set; }
    }

    private class WooCommerceProduct
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Slug { get; set; }
        public string? Permalink { get; set; }
        public DateTime? DateCreated { get; set; }
        public DateTime? DateModified { get; set; }
        public string? Status { get; set; }
        public string? Sku { get; set; }
        public string? Price { get; set; }
        public string? RegularPrice { get; set; }
        public string? Description { get; set; }
        public string? ShortDescription { get; set; }
        public string? StockStatus { get; set; }
        public List<WooCommerceCategory>? Categories { get; set; }
        public List<WooCommerceTag>? Tags { get; set; }
        public List<WooCommerceImage>? Images { get; set; }
    }

    private class WooCommerceCategory
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Slug { get; set; }
    }

    private class WooCommerceTag
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Slug { get; set; }
    }

    private class WooCommerceImage
    {
        public int Id { get; set; }
        public string? Src { get; set; }
        public string? Name { get; set; }
        public string? Alt { get; set; }
    }

    private class WordPressRendered
    {
        public string? Rendered { get; set; }
    }

    private class WordPressEmbedded
    {
        public List<WordPressMedia>? WpFeaturedmedia { get; set; }
    }

    private class WordPressMedia
    {
        public int Id { get; set; }
        public string? SourceUrl { get; set; }
    }
}
