namespace Hazina.Enterprise.Core.Caching;

/// <summary>
/// Interface for cache operations
/// </summary>
public interface ICache
{
    /// <summary>
    /// Get a value from cache
    /// </summary>
    /// <typeparam name="T">The type of the value</typeparam>
    /// <param name="key">The cache key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The cached value or null if not found</returns>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Set a value in cache
    /// </summary>
    /// <typeparam name="T">The type of the value</typeparam>
    /// <param name="key">The cache key</param>
    /// <param name="value">The value to cache</param>
    /// <param name="options">Cache entry options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SetAsync<T>(
        string key,
        T value,
        CacheEntryOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove a value from cache
    /// </summary>
    /// <param name="key">The cache key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if a key exists in cache
    /// </summary>
    /// <param name="key">The cache key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the key exists</returns>
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get or set a value in cache
    /// </summary>
    /// <typeparam name="T">The type of the value</typeparam>
    /// <param name="key">The cache key</param>
    /// <param name="factory">Factory function to create the value if not cached</param>
    /// <param name="options">Cache entry options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The cached or newly created value</returns>
    Task<T> GetOrSetAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        CacheEntryOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clear all entries from cache
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ClearAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Options for cache entries
/// </summary>
public class CacheEntryOptions
{
    /// <summary>
    /// Absolute expiration time
    /// </summary>
    public DateTimeOffset? AbsoluteExpiration { get; set; }

    /// <summary>
    /// Absolute expiration relative to now
    /// </summary>
    public TimeSpan? AbsoluteExpirationRelativeToNow { get; set; }

    /// <summary>
    /// Sliding expiration (reset on access)
    /// </summary>
    public TimeSpan? SlidingExpiration { get; set; }

    /// <summary>
    /// Priority for eviction
    /// </summary>
    public CachePriority Priority { get; set; } = CachePriority.Normal;
}

/// <summary>
/// Cache entry priority
/// </summary>
public enum CachePriority
{
    Low,
    Normal,
    High,
    NeverRemove
}
