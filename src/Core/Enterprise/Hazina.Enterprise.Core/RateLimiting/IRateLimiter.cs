namespace Hazina.Enterprise.Core.RateLimiting;

/// <summary>
/// Interface for rate limiting
/// </summary>
public interface IRateLimiter
{
    /// <summary>
    /// Attempt to acquire a permit to proceed
    /// </summary>
    /// <param name="key">The key to rate limit (e.g., user ID, API key)</param>
    /// <param name="cost">The cost in tokens (default is 1)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the request is allowed, false if rate limited</returns>
    Task<bool> TryAcquireAsync(
        string key,
        int cost = 1,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Acquire a permit or wait until one is available
    /// </summary>
    /// <param name="key">The key to rate limit</param>
    /// <param name="cost">The cost in tokens</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task AcquireAsync(
        string key,
        int cost = 1,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the current rate limit status for a key
    /// </summary>
    /// <param name="key">The key to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Rate limit status</returns>
    Task<RateLimitStatus> GetStatusAsync(
        string key,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reset rate limit for a key
    /// </summary>
    /// <param name="key">The key to reset</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ResetAsync(string key, CancellationToken cancellationToken = default);
}

/// <summary>
/// Rate limit status
/// </summary>
public class RateLimitStatus
{
    /// <summary>
    /// Number of available tokens
    /// </summary>
    public int AvailableTokens { get; set; }

    /// <summary>
    /// Maximum tokens allowed
    /// </summary>
    public int MaxTokens { get; set; }

    /// <summary>
    /// When the next token will be available
    /// </summary>
    public DateTimeOffset? NextTokenAvailableAt { get; set; }

    /// <summary>
    /// Whether the key is currently rate limited
    /// </summary>
    public bool IsRateLimited => AvailableTokens <= 0;
}
