namespace Hazina.LLMs.Resilience;

/// <summary>
/// Defines rate limiting behavior for LLM API calls.
/// </summary>
public interface IRateLimiter
{
    /// <summary>
    /// Waits until the operation is permitted according to rate limits.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the operation is permitted.</returns>
    Task WaitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that an operation completed successfully.
    /// </summary>
    void RecordSuccess();

    /// <summary>
    /// Records that a rate limit error occurred.
    /// </summary>
    /// <param name="retryAfter">Optional retry-after duration from the API response.</param>
    void RecordRateLimit(TimeSpan? retryAfter = null);
}
