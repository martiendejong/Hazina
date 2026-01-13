using System.Collections.Generic;

namespace Hazina.Tools.Services.Web.Models
{
    /// <summary>
    /// Configuration for FireCrawl service
    /// </summary>
    public class FireCrawlConfig
    {
        public string? ApiKey { get; set; }
        public string BaseUrl { get; set; } = "https://api.firecrawl.dev/v1";
    }

    /// <summary>
    /// Base result class for all FireCrawl operations
    /// </summary>
    public class FireCrawlResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
    }

    /// <summary>
    /// Result of a scrape operation - fetches webpage content and metadata
    /// </summary>
    public class FireCrawlScrapeResult : FireCrawlResult
    {
        public string Url { get; set; } = "";
        public string? Content { get; set; }
        public string? Markdown { get; set; }
        public string? Html { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Result of a map operation - discovers website structure
    /// </summary>
    public class FireCrawlMapResult : FireCrawlResult
    {
        public string BaseUrl { get; set; } = "";
        public List<string> Links { get; set; } = new();
        public Dictionary<string, List<string>> Structure { get; set; } = new();
    }

    /// <summary>
    /// Request for crawl operation - traverse multiple pages
    /// </summary>
    public class FireCrawlCrawlRequest
    {
        public string Url { get; set; } = "";
        public int MaxDepth { get; set; } = 2;
        public int MaxPages { get; set; } = 10;
        public bool IncludeScreenshots { get; set; } = false;
        public List<string>? AllowedDomains { get; set; }
    }

    /// <summary>
    /// Single page result from crawl operation
    /// </summary>
    public class FireCrawlPageResult
    {
        public string Url { get; set; } = "";
        public string? Title { get; set; }
        public string? Content { get; set; }
        public string? Markdown { get; set; }
        public string? Screenshot { get; set; } // Base64 encoded
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Result of crawl operation - multiple pages crawled
    /// </summary>
    public class FireCrawlCrawlResult : FireCrawlResult
    {
        public string StartUrl { get; set; } = "";
        public int PagesCrawled { get; set; }
        public List<FireCrawlPageResult> Pages { get; set; } = new();
    }

    /// <summary>
    /// Request for extract operation - structured data extraction
    /// </summary>
    public class FireCrawlExtractRequest
    {
        public string Url { get; set; } = "";
        public string? ExtractionPrompt { get; set; }
        public Dictionary<string, string>? Schema { get; set; }
    }

    /// <summary>
    /// Branding data extracted from website
    /// </summary>
    public class BrandingData
    {
        public List<string> Colors { get; set; } = new();
        public List<string> Fonts { get; set; } = new();
        public string? PrimaryColor { get; set; }
        public string? SecondaryColor { get; set; }
        public string? LogoUrl { get; set; }
        public Dictionary<string, string> Typography { get; set; } = new();
    }

    /// <summary>
    /// Result of extract operation - structured data
    /// </summary>
    public class FireCrawlExtractResult : FireCrawlResult
    {
        public string Url { get; set; } = "";
        public Dictionary<string, object> ExtractedData { get; set; } = new();
        public BrandingData? Branding { get; set; }
    }

    /// <summary>
    /// Request for screenshot operation
    /// </summary>
    public class FireCrawlScreenshotRequest
    {
        public string Url { get; set; } = "";
        public bool FullPage { get; set; } = true;
        public int Width { get; set; } = 1280;
        public int Height { get; set; } = 720;
    }

    /// <summary>
    /// Result of screenshot operation
    /// </summary>
    public class FireCrawlScreenshotResult : FireCrawlResult
    {
        public string Url { get; set; } = "";
        public string? Screenshot { get; set; } // Base64 encoded image
        public string? MimeType { get; set; } = "image/png";
    }

    /// <summary>
    /// Request for search operation - query site content
    /// </summary>
    public class FireCrawlSearchRequest
    {
        public string Query { get; set; } = "";
        public string? Domain { get; set; }
        public int MaxResults { get; set; } = 5;
    }

    /// <summary>
    /// Single search result item
    /// </summary>
    public class FireCrawlSearchResultItem
    {
        public string Url { get; set; } = "";
        public string? Title { get; set; }
        public string? Snippet { get; set; }
        public double Relevance { get; set; }
    }

    /// <summary>
    /// Result of search operation
    /// </summary>
    public class FireCrawlSearchResult : FireCrawlResult
    {
        public string Query { get; set; } = "";
        public int ResultCount { get; set; }
        public List<FireCrawlSearchResultItem> Results { get; set; } = new();
    }
}
