using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Hazina.Tools.Services.Social.Abstractions;
using Hazina.Tools.Services.Social.Models;
using Hazina.Tools.Services.Web.Abstractions;
using Hazina.Tools.Services.Web.Models;
using Microsoft.Extensions.Logging;

namespace Hazina.Tools.Services.Social.Providers;

/// <summary>
/// Generic website provider - works with any website (with or without auth).
/// Scrapes content from public pages using FireCrawl.
/// Supports NoAuth mode for public content.
/// </summary>
public class GenericWebsiteProvider : ISocialProvider
{
    public string ProviderId => "generic-website";
    public string DisplayName => "Generic Website";

    private readonly IFireCrawlService _fireCrawl;
    private readonly ILogger<GenericWebsiteProvider> _logger;

    public GenericWebsiteProvider(
        IFireCrawlService fireCrawl,
        ILogger<GenericWebsiteProvider> logger)
    {
        _fireCrawl = fireCrawl;
        _logger = logger;
    }

    /// <summary>
    /// Generic websites don't use OAuth.
    /// </summary>
    public string GetAuthorizationUrl(string redirectUri, string state)
    {
        throw new NotSupportedException("Generic website provider does not support OAuth. Use AuthType.NoAuth.");
    }

    /// <summary>
    /// Generic websites don't use OAuth.
    /// </summary>
    public Task<SocialAuthResult> ExchangeCodeAsync(
        string code,
        string redirectUri,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Generic website provider does not support OAuth. Use AuthType.NoAuth.");
    }

    /// <summary>
    /// Generic websites don't use refresh tokens.
    /// </summary>
    public Task<SocialAuthResult> RefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Generic website provider does not support OAuth. Use AuthType.NoAuth.");
    }

    /// <summary>
    /// Generic websites don't have user profiles.
    /// Returns a placeholder profile with the website URL.
    /// </summary>
    public Task<SocialProfile> GetProfileAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        // For generic websites, there's no "profile"
        // Return a placeholder with the website name
        return Task.FromResult(new SocialProfile
        {
            Id = "website",
            Name = "Website Content",
            Email = null,
            ProfileUrl = null,
            AvatarUrl = null,
            Metadata = new Dictionary<string, string>()
        });
    }

    /// <summary>
    /// Imports content from website (crawls sitemap, discovers blog posts and pages).
    /// For NoAuth mode: accessToken format is "baseUrl|||projectId|||accountId"
    /// </summary>
    public async Task<SocialImportResult> ImportContentAsync(
        string accessToken,
        SocialImportOptions options,
        CancellationToken cancellationToken = default)
    {
        // 1. Parse access token to extract base URL and IDs
        // Format: "baseUrl|||projectId|||accountId" (for NoAuth mode)
        var parts = accessToken.Split("|||");
        if (parts.Length < 1)
        {
            throw new InvalidOperationException("Invalid access token format for generic website. Expected: baseUrl|||projectId|||accountId");
        }

        var baseUrl = parts[0].Trim();
        var projectId = parts.Length > 1 ? parts[1] : "";
        var accountId = parts.Length > 2 ? parts[2] : "";

        _logger.LogInformation("Starting generic website import from {BaseUrl}", baseUrl);

        // 2. Crawl website using FireCrawl
        var maxItems = options.MaxItems > 0 ? options.MaxItems : 100;
        var crawlRequest = new FireCrawlCrawlRequest
        {
            Url = baseUrl,
            MaxDepth = 3, // Traverse 3 levels deep
            MaxPages = maxItems,
            IncludeScreenshots = false
        };

        var crawlResult = await _fireCrawl.CrawlAsync(crawlRequest);

        if (!crawlResult.Success || crawlResult.Pages == null || !crawlResult.Pages.Any())
        {
            _logger.LogWarning("Website crawl failed: {Error}", crawlResult.Error);
            return new SocialImportResult
            {
                Success = false,
                Error = crawlResult.Error ?? "Failed to crawl website"
            };
        }

        _logger.LogInformation("Crawled {Count} pages from {BaseUrl}", crawlResult.Pages.Count, baseUrl);

        // 3. Filter pages by content type
        var blogPosts = new List<FireCrawlPageResult>();
        var staticPages = new List<FireCrawlPageResult>();

        foreach (var page in crawlResult.Pages)
        {
            if (IsBlogPost(page.Url))
            {
                blogPosts.Add(page);
            }
            else if (IsStaticPage(page.Url))
            {
                staticPages.Add(page);
            }
        }

        _logger.LogInformation("Categorized: {BlogCount} blog posts, {PageCount} static pages",
            blogPosts.Count, staticPages.Count);

        // 4. Convert to UnifiedContent
        var unifiedContent = new List<UnifiedContent>();

        // Import blog posts if requested
        if (options.ContentTypes.Contains("posts"))
        {
            foreach (var post in blogPosts)
            {
                try
                {
                    var unified = await ConvertToUnifiedContentAsync(
                        post,
                        "post",
                        baseUrl,
                        projectId,
                        accountId,
                        includeImages: true); // Always include images for now
                    unifiedContent.Add(unified);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to convert blog post {Url}", post.Url);
                }
            }
        }

        // Import pages if requested
        if (options.ContentTypes.Contains("pages"))
        {
            foreach (var page in staticPages)
            {
                try
                {
                    var unified = await ConvertToUnifiedContentAsync(
                        page,
                        "page",
                        baseUrl,
                        projectId,
                        accountId,
                        includeImages: true); // Always include images for now
                    unifiedContent.Add(unified);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to convert page {Url}", page.Url);
                }
            }
        }

        _logger.LogInformation("Successfully converted {Count} items to UnifiedContent", unifiedContent.Count);

        return new SocialImportResult
        {
            Success = true,
            TotalImported = unifiedContent.Count,
            Posts = new List<SocialPost>(), // Not used for website imports
            Articles = new List<SocialArticle>() // Not used for website imports
            // Note: UnifiedContent should be stored separately by the calling code
            // We return empty Posts/Articles for backward compatibility
        };
    }

    /// <summary>
    /// Determines if a URL is a blog post based on common patterns.
    /// </summary>
    private bool IsBlogPost(string url)
    {
        var lowerUrl = url.ToLowerInvariant();

        // Common blog post URL patterns
        if (lowerUrl.Contains("/blog/") ||
            lowerUrl.Contains("/post/") ||
            lowerUrl.Contains("/posts/") ||
            lowerUrl.Contains("/article/") ||
            lowerUrl.Contains("/articles/") ||
            lowerUrl.Contains("/news/") ||
            lowerUrl.Contains("/stories/"))
        {
            return true;
        }

        // Date-based URL pattern (e.g., /2024/01/15/post-title)
        if (Regex.IsMatch(url, @"/\d{4}/\d{1,2}/"))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Determines if a URL is a static page.
    /// </summary>
    private bool IsStaticPage(string url)
    {
        var lowerUrl = url.ToLowerInvariant();

        // Common static page URL patterns
        var staticPagePatterns = new[]
        {
            "/about", "/contact", "/services", "/products",
            "/privacy", "/terms", "/legal", "/faq",
            "/team", "/portfolio", "/work", "/pricing"
        };

        return staticPagePatterns.Any(pattern => lowerUrl.Contains(pattern));
    }

    /// <summary>
    /// Converts a FireCrawl page to UnifiedContent format.
    /// </summary>
    private Task<UnifiedContent> ConvertToUnifiedContentAsync(
        FireCrawlPageResult page,
        string contentType,
        string baseUrl,
        string projectId,
        string accountId,
        bool includeImages)
    {
        // Extract metadata
        var title = page.Title ?? ExtractTitleFromUrl(page.Url);
        var description = page.Metadata.TryGetValue("description", out var desc) && desc is string descStr
            ? descStr
            : "";

        // Extract publish date
        var publishedDate = ExtractPublishDate(page);

        // Extract images if enabled
        var media = new List<ContentMedia>();
        if (includeImages && page.Metadata.TryGetValue("image", out var imgObj) && imgObj is string imgUrl)
        {
            media.Add(new ContentMedia
            {
                Id = Guid.NewGuid().ToString(),
                Type = MediaType.Image,
                Url = imgUrl,
                DisplayOrder = 0,
                ImportedAt = DateTime.UtcNow
            });
        }

        // Generate unique source ID from URL
        var sourceId = GenerateSourceId(page.Url);

        var content = new UnifiedContent
        {
            Id = $"generic-website-{sourceId}",
            ProjectId = projectId,
            AccountId = accountId,
            SourceType = "generic-website",
            SourceId = sourceId,
            SourceUrl = page.Url,
            ContentType = contentType,
            Title = title,
            Content = page.Content ?? page.Markdown ?? "",
            ContentHtml = page.Content, // FireCrawl returns markdown in Content
            ContentPlainText = page.Markdown,
            Summary = description,
            FeaturedImageUrl = media.FirstOrDefault()?.Url,
            Media = media,
            PublishedAt = publishedDate,
            ImportedAt = DateTime.UtcNow,
            LastSyncedAt = DateTime.UtcNow,
            Tags = ExtractTags(page),
            Categories = ExtractCategories(page),
            Status = "published",
            IsHistorical = true,
            DisplayOnCalendar = contentType == "post" // Blog posts on calendar, pages not
        };

        return Task.FromResult(content);
    }

    /// <summary>
    /// Extracts title from URL if no title metadata available.
    /// </summary>
    private string ExtractTitleFromUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            var lastSegment = uri.Segments.LastOrDefault()?.Trim('/') ?? "";

            // Convert URL slug to title (e.g., "hello-world" -> "Hello World")
            return string.Join(" ", lastSegment.Split('-', '_'))
                .Replace("%20", " ")
                .Trim();
        }
        catch
        {
            return "Untitled";
        }
    }

    /// <summary>
    /// Extracts publish date from page metadata or URL.
    /// </summary>
    private DateTime ExtractPublishDate(FireCrawlPageResult page)
    {
        // Try meta tags first
        if (page.Metadata.TryGetValue("article:published_time", out var publishedObj) &&
            publishedObj is string published &&
            DateTime.TryParse(published, out var date))
        {
            return date;
        }

        if (page.Metadata.TryGetValue("og:published_time", out var ogPublished) &&
            ogPublished is string ogPublishedStr &&
            DateTime.TryParse(ogPublishedStr, out var ogDate))
        {
            return ogDate;
        }

        // Try URL date pattern (/2024/01/15/)
        var match = Regex.Match(page.Url, @"/(\d{4})/(\d{1,2})/(\d{1,2})/");
        if (match.Success)
        {
            try
            {
                return new DateTime(
                    int.Parse(match.Groups[1].Value),
                    int.Parse(match.Groups[2].Value),
                    int.Parse(match.Groups[3].Value));
            }
            catch
            {
                // Invalid date, fall through to default
            }
        }

        // Default to now
        return DateTime.UtcNow;
    }

    /// <summary>
    /// Generates a stable source ID from URL using hash.
    /// </summary>
    private string GenerateSourceId(string url)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(url));
        return Convert.ToBase64String(hash).Substring(0, 16).Replace("/", "-").Replace("+", "_");
    }

    /// <summary>
    /// Extracts tags from page metadata.
    /// </summary>
    private List<string> ExtractTags(FireCrawlPageResult page)
    {
        var tags = new List<string>();

        if (page.Metadata.TryGetValue("keywords", out var keywordsObj) && keywordsObj is string keywords)
        {
            tags.AddRange(keywords.Split(',').Select(t => t.Trim()).Where(t => !string.IsNullOrEmpty(t)));
        }

        if (page.Metadata.TryGetValue("article:tag", out var tagObj) && tagObj is string tag)
        {
            tags.Add(tag);
        }

        return tags.Distinct().ToList();
    }

    /// <summary>
    /// Extracts categories from page metadata.
    /// </summary>
    private List<string> ExtractCategories(FireCrawlPageResult page)
    {
        var categories = new List<string>();

        if (page.Metadata.TryGetValue("article:section", out var sectionObj) && sectionObj is string section)
        {
            categories.Add(section);
        }

        return categories;
    }

    /// <summary>
    /// Fetches content as UnifiedContent format (new pattern for website imports).
    /// This is the recommended method for importing website content.
    /// </summary>
    /// <param name="accessToken">Format: "baseUrl|||projectId|||accountId"</param>
    /// <param name="contentType">Type of content: "post", "page"</param>
    /// <param name="projectId">Project ID</param>
    /// <param name="accountId">Account ID</param>
    /// <param name="maxItems">Maximum number of items to fetch</param>
    /// <param name="modifiedAfter">Only fetch content modified after this date (not supported for generic websites)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task<List<UnifiedContent>> FetchContentAsUnifiedAsync(
        string accessToken,
        string contentType,
        string projectId,
        string accountId,
        int maxItems = 1000,
        DateTime? modifiedAfter = null,
        CancellationToken cancellationToken = default)
    {
        // Parse access token to get base URL
        var parts = accessToken.Split("|||");
        var baseUrl = parts[0].Trim();

        _logger.LogInformation("Fetching {ContentType} from {BaseUrl} (max {MaxItems})",
            contentType, baseUrl, maxItems);

        // Crawl website
        var crawlRequest = new FireCrawlCrawlRequest
        {
            Url = baseUrl,
            MaxDepth = 3,
            MaxPages = maxItems,
            IncludeScreenshots = false
        };

        var crawlResult = await _fireCrawl.CrawlAsync(crawlRequest);

        if (!crawlResult.Success || crawlResult.Pages == null || !crawlResult.Pages.Any())
        {
            _logger.LogWarning("Website crawl failed: {Error}", crawlResult.Error);
            return new List<UnifiedContent>();
        }

        // Filter and convert pages based on content type
        var unifiedContent = new List<UnifiedContent>();

        foreach (var page in crawlResult.Pages)
        {
            bool shouldInclude = contentType.ToLower() switch
            {
                "post" or "posts" => IsBlogPost(page.Url),
                "page" or "pages" => IsStaticPage(page.Url),
                _ => false
            };

            if (shouldInclude)
            {
                try
                {
                    var unified = await ConvertToUnifiedContentAsync(
                        page,
                        contentType == "post" || contentType == "posts" ? "post" : "page",
                        baseUrl,
                        projectId,
                        accountId,
                        includeImages: true);
                    unifiedContent.Add(unified);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to convert {Url} to UnifiedContent", page.Url);
                }
            }
        }

        _logger.LogInformation("Fetched {Count} {ContentType} items from {BaseUrl}",
            unifiedContent.Count, contentType, baseUrl);

        return unifiedContent;
    }

    /// <summary>
    /// Generic websites don't support revoking access.
    /// </summary>
    public Task<bool> RevokeAccessAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        // No-op for generic websites (no auth to revoke)
        return Task.FromResult(true);
    }

    /// <summary>
    /// Generic websites don't support fetching comments.
    /// </summary>
    public Task<List<SocialComment>> FetchCommentsAsync(
        string accessToken,
        string contentId,
        CancellationToken cancellationToken = default)
    {
        // Generic websites don't have engagement data
        return Task.FromResult(new List<SocialComment>());
    }

    /// <summary>
    /// Generic websites don't support fetching engagement metrics.
    /// </summary>
    public Task<SocialEngagement> FetchEngagementAsync(
        string accessToken,
        string contentId,
        CancellationToken cancellationToken = default)
    {
        // Generic websites don't have engagement data
        return Task.FromResult(new SocialEngagement
        {
            LikeCount = 0,
            CommentCount = 0,
            ShareCount = 0,
            ViewCount = 0
        });
    }
}
