using WebSearch.Core;
using WebSearch.Infrastructure;

namespace WebSearch;

/// <summary>
/// High-level search service with caching and rate limiting
/// </summary>
public class SearchService
{
    private readonly ISearchProvider _provider;
    private readonly ISearchCache? _cache;
    private readonly IRateLimiter? _rateLimiter;
    private readonly TimeSpan _cacheTtl;

    public SearchService(
        ISearchProvider provider,
        ISearchCache? cache = null,
        IRateLimiter? rateLimiter = null,
        TimeSpan? cacheTtl = null)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _cache = cache;
        _rateLimiter = rateLimiter;
        _cacheTtl = cacheTtl ?? TimeSpan.FromHours(1);
    }

    public async Task<SearchResult[]> SearchAsync(
        string query,
        SearchOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new SearchOptions();

        // Try cache first
        if (_cache != null)
        {
            var cacheKey = _cache.GenerateCacheKey(query, options, _provider.GetProviderName());
            var cachedResults = _cache.TryGet(cacheKey);

            if (cachedResults != null)
            {
                return cachedResults;
            }

            // Cache miss - proceed with search
            if (_rateLimiter != null)
            {
                await _rateLimiter.WaitAsync(_provider.GetProviderName(), cancellationToken);
            }

            var results = await _provider.SearchAsync(query, options, cancellationToken);

            // Cache the results
            _cache.Set(cacheKey, results, _cacheTtl);

            return results;
        }

        // No caching - just rate limit and search
        if (_rateLimiter != null)
        {
            await _rateLimiter.WaitAsync(_provider.GetProviderName(), cancellationToken);
        }

        return await _provider.SearchAsync(query, options, cancellationToken);
    }

    public async Task<bool> IsProviderAvailableAsync(CancellationToken cancellationToken = default)
    {
        return await _provider.IsAvailableAsync(cancellationToken);
    }

    public string GetProviderName() => _provider.GetProviderName();

    public CacheStatistics? GetCacheStatistics() => _cache?.GetStatistics();
}
