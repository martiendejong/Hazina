namespace WebSearch.Infrastructure;

/// <summary>
/// Interface for rate limiting search requests
/// </summary>
public interface IRateLimiter
{
    /// <summary>
    /// Wait until a request is allowed under the rate limit
    /// </summary>
    /// <param name="providerName">Provider name for per-provider limits</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task WaitAsync(string providerName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if a request is currently allowed without waiting
    /// </summary>
    /// <param name="providerName">Provider name</param>
    /// <returns>True if request is allowed, false if rate limited</returns>
    bool IsAllowed(string providerName);
}
