using System.Threading.Tasks;
using Hazina.Tools.Services.Web.Models;

namespace Hazina.Tools.Services.Web.Abstractions
{
    /// <summary>
    /// Interface for FireCrawl MCP web scraping operations.
    /// Enables autonomous web scraping, branding extraction, site mapping, and structured data extraction.
    /// </summary>
    public interface IFireCrawlService
    {
        /// <summary>
        /// Scrape a single webpage - pulls content + metadata.
        /// Converts HTML to clean markdown suitable for LLM ingestion.
        /// </summary>
        /// <param name="url">URL to scrape</param>
        /// <param name="includeHtml">Include raw HTML in response (default: false)</param>
        /// <returns>Scraped content with markdown and metadata</returns>
        Task<FireCrawlScrapeResult> ScrapeAsync(string url, bool includeHtml = false);

        /// <summary>
        /// Map website structure - discovers all subpages and internal links.
        /// Useful for understanding site architecture before crawling.
        /// </summary>
        /// <param name="baseUrl">Base URL to map</param>
        /// <param name="maxDepth">Maximum depth to traverse (default: 2)</param>
        /// <returns>Site structure with all discovered links</returns>
        Task<FireCrawlMapResult> MapAsync(string baseUrl, int maxDepth = 2);

        /// <summary>
        /// Crawl multiple pages - traverses linked pages and collects content.
        /// Use after Map to selectively crawl discovered pages.
        /// </summary>
        /// <param name="request">Crawl configuration (URL, depth, max pages, etc.)</param>
        /// <returns>Collection of crawled pages with content</returns>
        Task<FireCrawlCrawlResult> CrawlAsync(FireCrawlCrawlRequest request);

        /// <summary>
        /// Extract structured data from webpage using AI.
        /// Can extract branding (colors, fonts, logos), product data, contact info, etc.
        /// </summary>
        /// <param name="request">Extraction configuration with URL and optional schema</param>
        /// <returns>Extracted structured data</returns>
        Task<FireCrawlExtractResult> ExtractAsync(FireCrawlExtractRequest request);

        /// <summary>
        /// Extract branding data specifically (colors, fonts, typography, logo).
        /// Convenience method for common use case of branding audits.
        /// </summary>
        /// <param name="url">URL to extract branding from</param>
        /// <returns>Branding data (colors, fonts, logo)</returns>
        Task<FireCrawlExtractResult> ExtractBrandingAsync(string url);

        /// <summary>
        /// Capture full-page screenshot of a webpage.
        /// Returns base64-encoded PNG image.
        /// </summary>
        /// <param name="request">Screenshot configuration (URL, dimensions, full page)</param>
        /// <returns>Base64-encoded screenshot image</returns>
        Task<FireCrawlScreenshotResult> ScreenshotAsync(FireCrawlScreenshotRequest request);

        /// <summary>
        /// Search within a website or domain for specific content.
        /// Uses AI to find relevant pages matching the query.
        /// </summary>
        /// <param name="request">Search configuration (query, domain, max results)</param>
        /// <returns>Relevant search results with snippets</returns>
        Task<FireCrawlSearchResult> SearchAsync(FireCrawlSearchRequest request);
    }
}
