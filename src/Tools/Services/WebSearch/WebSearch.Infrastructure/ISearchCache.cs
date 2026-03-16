using WebSearch.Core;

namespace WebSearch.Infrastructure;

/// <summary>
/// Interface for caching search results
/// </summary>
public interface ISearchCache
{
    /// <summary>
    /// Try to get cached results
    /// </summary>
    /// <param name="cacheKey">Cache key</param>
    /// <returns>Cached results if found, null otherwise</returns>
    SearchResult[]? TryGet(string cacheKey);

    /// <summary>
    /// Set cached results with TTL
    /// </summary>
    /// <param name="cacheKey">Cache key</param>
    /// <param name="results">Results to cache</param>
    /// <param name="ttl">Time to live</param>
    void Set(string cacheKey, SearchResult[] results, TimeSpan ttl);

    /// <summary>
    /// Generate cache key from query and options
    /// </summary>
    /// <param name="query">Search query</param>
    /// <param name="options">Search options</param>
    /// <param name="providerName">Provider name</param>
    /// <returns>Cache key string</returns>
    string GenerateCacheKey(string query, SearchOptions? options, string providerName);

    /// <summary>
    /// Get cache statistics
    /// </summary>
    CacheStatistics GetStatistics();

    /// <summary>
    /// Clear all cached entries
    /// </summary>
    void Clear();
}

/// <summary>
/// Cache statistics
/// </summary>
public class CacheStatistics
{
    public long Hits { get; set; }
    public long Misses { get; set; }
    public double HitRate => Hits + Misses > 0 ? (double)Hits / (Hits + Misses) : 0;
}
