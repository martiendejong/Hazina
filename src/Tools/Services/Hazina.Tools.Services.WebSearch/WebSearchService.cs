using Microsoft.Extensions.Caching.Memory;
using WebSearch;
using WebSearch.Core;
using WebSearch.Infrastructure;

namespace Hazina.Tools.Services.WebSearch;

/// <summary>
/// Hazina integration service for web search functionality.
/// Provides multi-engine search capabilities (Google, Bing, DuckDuckGo) with intelligent caching.
/// </summary>
public class WebSearchService
{
    private readonly SearchProviderFactory _factory;
    private readonly ISearchCache _cache;
    private readonly IRateLimiter _rateLimiter;

    public WebSearchService()
    {
        // Initialize cache and rate limiting
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        _cache = new InMemorySearchCache(memoryCache);
        _rateLimiter = new TokenBucketRateLimiter(requestsPerMinute: 30);
        _factory = new SearchProviderFactory(_cache, _rateLimiter);
    }

    /// <summary>
    /// Performs a web search using the specified provider.
    /// </summary>
    /// <param name="query">The search query.</param>
    /// <param name="provider">The search provider (defaults to DuckDuckGo for reliability).</param>
    /// <param name="maxResults">Maximum number of results (default: 10).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Array of search results.</returns>
    public async Task<SearchResult[]> SearchAsync(
        string query,
        ProviderType provider = ProviderType.DuckDuckGo,
        int maxResults = 10,
        CancellationToken cancellationToken = default)
    {
        var options = new SearchOptions { MaxResults = maxResults };
        var service = _factory.Create(provider);

        return await service.SearchAsync(query, options, cancellationToken);
    }

    /// <summary>
    /// Gets cache statistics for monitoring performance.
    /// </summary>
    public CacheStatistics GetCacheStats() => _cache.GetStatistics();
}
