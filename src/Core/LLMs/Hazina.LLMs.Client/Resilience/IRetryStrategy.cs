namespace Hazina.LLMs.Resilience;

/// <summary>
/// Defines a strategy for retrying failed operations with configurable backoff behavior.
/// </summary>
public interface IRetryStrategy
{
    /// <summary>
    /// Gets the maximum number of retry attempts.
    /// </summary>
    int MaxRetries { get; }

    /// <summary>
    /// Calculates the delay before the next retry attempt.
    /// </summary>
    /// <param name="attemptNumber">The current attempt number (1-based).</param>
    /// <returns>The delay duration before the next retry.</returns>
    TimeSpan GetDelay(int attemptNumber);

    /// <summary>
    /// Determines whether the exception should trigger a retry.
    /// </summary>
    /// <param name="exception">The exception that occurred.</param>
    /// <returns>True if the operation should be retried, false otherwise.</returns>
    bool ShouldRetry(Exception exception);
}
