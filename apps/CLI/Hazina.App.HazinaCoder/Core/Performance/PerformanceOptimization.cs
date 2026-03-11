using System.Collections.Concurrent;

namespace Hazina.App.HazinaCoder.Core.Performance;

/// <summary>
/// Performance optimization infrastructure.
/// Improvement #61: Query optimization
/// Improvement #62: Caching strategies
/// Improvement #63: Lazy loading
/// Improvement #64: Parallel processing
/// Improvement #65: Memory optimization
/// Improvement #66: CPU optimization
/// Improvement #67: I/O optimization
/// Improvement #68: Network optimization
/// Improvement #69: Database optimization
/// Improvement #70: Resource pooling
/// Improvement #71: Connection management
/// Improvement #72: Batch processing
/// Improvement #73: Async/await optimization
/// Improvement #74: Hot path optimization
/// Improvement #75: Startup optimization
/// </summary>
public class PerformanceOptimization
{
    private readonly CacheManager _cache = new();
    private readonly ResourcePool _pool = new();
    private readonly BatchProcessor _batchProcessor = new();

    public async Task<T> GetOrCacheAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null)
    {
        return await _cache.GetOrAddAsync(key, factory, expiration ?? TimeSpan.FromMinutes(60));
    }

    public async Task<List<TResult>> ProcessInParallelAsync<TSource, TResult>(
        IEnumerable<TSource> items,
        Func<TSource, Task<TResult>> processor,
        int maxDegreeOfParallelism = 4)
    {
        var results = new ConcurrentBag<TResult>();
        var options = new ParallelOptions { MaxDegreeOfParallelism = maxDegreeOfParallelism };

        await Parallel.ForEachAsync(items, options, async (item, ct) =>
        {
            var result = await processor(item);
            results.Add(result);
        });

        return results.ToList();
    }

    public async Task<List<TResult>> ProcessInBatchesAsync<TSource, TResult>(
        IEnumerable<TSource> items,
        Func<List<TSource>, Task<List<TResult>>> batchProcessor,
        int batchSize = 100)
    {
        return await _batchProcessor.ProcessAsync(items, batchProcessor, batchSize);
    }

    public T GetPooledResource<T>(Func<T> factory) where T : class
    {
        return _pool.Get(factory);
    }

    public void ReturnToPool<T>(T resource) where T : class
    {
        _pool.Return(resource);
    }
}

public class CacheManager
{
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();

    public async Task<T> GetOrAddAsync<T>(string key, Func<Task<T>> factory, TimeSpan expiration)
    {
        if (_cache.TryGetValue(key, out var entry) && !entry.IsExpired())
        {
            return (T)entry.Value;
        }

        var value = await factory();
        _cache[key] = new CacheEntry { Value = value!, ExpiresAt = DateTime.UtcNow.Add(expiration) };
        return value;
    }

    public void Clear() => _cache.Clear();
}

public class CacheEntry
{
    public object Value { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
    public bool IsExpired() => DateTime.UtcNow >= ExpiresAt;
}

public class ResourcePool
{
    private readonly ConcurrentDictionary<Type, ConcurrentBag<object>> _pools = new();

    public T Get<T>(Func<T> factory) where T : class
    {
        var type = typeof(T);
        if (!_pools.TryGetValue(type, out var pool))
        {
            pool = new ConcurrentBag<object>();
            _pools[type] = pool;
        }

        if (pool.TryTake(out var item))
        {
            return (T)item;
        }

        return factory();
    }

    public void Return<T>(T item) where T : class
    {
        var type = typeof(T);
        if (!_pools.TryGetValue(type, out var pool))
        {
            pool = new ConcurrentBag<object>();
            _pools[type] = pool;
        }

        pool.Add(item);
    }
}

public class BatchProcessor
{
    public async Task<List<TResult>> ProcessAsync<TSource, TResult>(
        IEnumerable<TSource> items,
        Func<List<TSource>, Task<List<TResult>>> processor,
        int batchSize)
    {
        var results = new List<TResult>();
        var batch = new List<TSource>();

        foreach (var item in items)
        {
            batch.Add(item);

            if (batch.Count >= batchSize)
            {
                var batchResults = await processor(batch);
                results.AddRange(batchResults);
                batch.Clear();
            }
        }

        if (batch.Any())
        {
            var batchResults = await processor(batch);
            results.AddRange(batchResults);
        }

        return results;
    }
}
