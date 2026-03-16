using Microsoft.Extensions.Caching.Memory;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WebSearch.Core;

namespace WebSearch.Infrastructure;

/// <summary>
/// In-memory implementation of search cache
/// </summary>
public class InMemorySearchCache : ISearchCache
{
    private readonly IMemoryCache _cache;
    private readonly CacheStatistics _stats = new();
    private readonly object _statsLock = new();

    public InMemorySearchCache(IMemoryCache cache)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    public SearchResult[]? TryGet(string cacheKey)
    {
        if (_cache.TryGetValue(cacheKey, out SearchResult[]? results))
        {
            lock (_statsLock)
            {
                _stats.Hits++;
            }
            return results;
        }

        lock (_statsLock)
        {
            _stats.Misses++;
        }
        return null;
    }

    public void Set(string cacheKey, SearchResult[] results, TimeSpan ttl)
    {
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl
        };

        _cache.Set(cacheKey, results, options);
    }

    public string GenerateCacheKey(string query, SearchOptions? options, string providerName)
    {
        // Create deterministic cache key from query, options, and provider
        var keyObject = new
        {
            Provider = providerName,
            Query = query.ToLowerInvariant().Trim(),
            MaxResults = options?.MaxResults ?? 10,
            Language = options?.Language,
            Region = options?.Region,
            SafeSearch = options?.SafeSearch ?? true,
            TimeRange = options?.TimeRange,
            Page = options?.Page ?? 0
        };

        var json = JsonSerializer.Serialize(keyObject);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToBase64String(hash);
    }

    public CacheStatistics GetStatistics()
    {
        lock (_statsLock)
        {
            return new CacheStatistics
            {
                Hits = _stats.Hits,
                Misses = _stats.Misses
            };
        }
    }

    public void Clear()
    {
        if (_cache is MemoryCache memoryCache)
        {
            memoryCache.Compact(1.0);
        }

        lock (_statsLock)
        {
            _stats.Hits = 0;
            _stats.Misses = 0;
        }
    }
}
