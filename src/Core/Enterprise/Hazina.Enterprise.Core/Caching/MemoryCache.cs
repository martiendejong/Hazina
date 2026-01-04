using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Hazina.Enterprise.Core.Caching;

/// <summary>
/// In-memory cache implementation
/// </summary>
public class MemoryCache : ICache, IDisposable
{
    private readonly ILogger<MemoryCache> _logger;
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    private readonly Timer _cleanupTimer;
    private bool _disposed;

    public MemoryCache(ILogger<MemoryCache> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Cleanup timer runs every minute
        _cleanupTimer = new Timer(CleanupExpiredEntries, null,
            TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));

        _logger.LogInformation("[MEMORY CACHE] Initialized");
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentNullException(nameof(key));

        if (_cache.TryGetValue(key, out var entry))
        {
            if (entry.IsExpired())
            {
                _cache.TryRemove(key, out _);
                _logger.LogDebug("[MEMORY CACHE] Key '{Key}' expired", key);
                return Task.FromResult<T?>(default);
            }

            // Update sliding expiration
            entry.UpdateAccess();

            try
            {
                var value = JsonSerializer.Deserialize<T>(entry.Value);
                _logger.LogDebug("[MEMORY CACHE] Cache hit for key '{Key}'", key);
                return Task.FromResult(value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MEMORY CACHE] Failed to deserialize cached value for key '{Key}'", key);
                _cache.TryRemove(key, out _);
                return Task.FromResult<T?>(default);
            }
        }

        _logger.LogDebug("[MEMORY CACHE] Cache miss for key '{Key}'", key);
        return Task.FromResult<T?>(default);
    }

    public Task SetAsync<T>(
        string key,
        T value,
        CacheEntryOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentNullException(nameof(key));

        try
        {
            var serialized = JsonSerializer.Serialize(value);
            var entry = new CacheEntry
            {
                Value = serialized,
                Options = options ?? new CacheEntryOptions(),
                CreatedAt = DateTimeOffset.UtcNow,
                LastAccessedAt = DateTimeOffset.UtcNow
            };

            _cache[key] = entry;
            _logger.LogDebug("[MEMORY CACHE] Set key '{Key}' with expiration: {Expiration}",
                key, options?.AbsoluteExpirationRelativeToNow);

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MEMORY CACHE] Failed to serialize value for key '{Key}'", key);
            throw;
        }
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentNullException(nameof(key));

        _cache.TryRemove(key, out _);
        _logger.LogDebug("[MEMORY CACHE] Removed key '{Key}'", key);

        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentNullException(nameof(key));

        if (_cache.TryGetValue(key, out var entry))
        {
            if (entry.IsExpired())
            {
                _cache.TryRemove(key, out _);
                return Task.FromResult(false);
            }

            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    public async Task<T> GetOrSetAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        CacheEntryOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentNullException(nameof(key));
        if (factory == null)
            throw new ArgumentNullException(nameof(factory));

        var cachedValue = await GetAsync<T>(key, cancellationToken);
        if (cachedValue != null)
        {
            return cachedValue;
        }

        _logger.LogDebug("[MEMORY CACHE] Cache miss for key '{Key}'. Executing factory", key);
        var value = await factory(cancellationToken);

        await SetAsync(key, value, options, cancellationToken);

        return value;
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        var count = _cache.Count;
        _cache.Clear();
        _logger.LogInformation("[MEMORY CACHE] Cleared {Count} entries", count);

        return Task.CompletedTask;
    }

    private void CleanupExpiredEntries(object? state)
    {
        try
        {
            var expiredKeys = _cache
                .Where(kvp => kvp.Value.IsExpired())
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                _cache.TryRemove(key, out _);
            }

            if (expiredKeys.Count > 0)
            {
                _logger.LogDebug("[MEMORY CACHE] Cleanup removed {Count} expired entries", expiredKeys.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MEMORY CACHE] Error during cleanup");
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _cleanupTimer?.Dispose();
        _cache.Clear();
        _disposed = true;

        _logger.LogInformation("[MEMORY CACHE] Disposed");
    }

    private class CacheEntry
    {
        public string Value { get; set; } = string.Empty;
        public CacheEntryOptions Options { get; set; } = new();
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset LastAccessedAt { get; set; }

        public bool IsExpired()
        {
            var now = DateTimeOffset.UtcNow;

            // Check absolute expiration
            if (Options.AbsoluteExpiration.HasValue && now >= Options.AbsoluteExpiration.Value)
            {
                return true;
            }

            // Check absolute expiration relative to now
            if (Options.AbsoluteExpirationRelativeToNow.HasValue &&
                now >= CreatedAt + Options.AbsoluteExpirationRelativeToNow.Value)
            {
                return true;
            }

            // Check sliding expiration
            if (Options.SlidingExpiration.HasValue &&
                now >= LastAccessedAt + Options.SlidingExpiration.Value)
            {
                return true;
            }

            return false;
        }

        public void UpdateAccess()
        {
            LastAccessedAt = DateTimeOffset.UtcNow;
        }
    }
}
