namespace WebSearch.Core;

/// <summary>
/// Supported search engine providers
/// </summary>
public enum ProviderType
{
    /// <summary>
    /// Google Search (scraping-based)
    /// </summary>
    Google,

    /// <summary>
    /// Bing Search (scraping-based)
    /// </summary>
    Bing,

    /// <summary>
    /// DuckDuckGo Search
    /// </summary>
    DuckDuckGo,

    /// <summary>
    /// Yahoo Search (scraping-based)
    /// </summary>
    Yahoo,

    /// <summary>
    /// Google Custom Search API (requires API key)
    /// </summary>
    GoogleCustomSearch,

    /// <summary>
    /// Bing Web Search API (requires API key)
    /// </summary>
    BingApi,

    /// <summary>
    /// SerpApi (third-party unified SERP API)
    /// </summary>
    SerpApi,

    /// <summary>
    /// Playwright-based provider for JavaScript-heavy sites
    /// </summary>
    Playwright
}
