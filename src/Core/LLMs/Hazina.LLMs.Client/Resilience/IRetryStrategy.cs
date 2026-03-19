namespace Hazina.LLMs.Resilience;

/// <summary>
/// Defines a pluggable retry strategy for handling transient failures.
/// </summary>
public interface IRetryStrategy
{
    /// <summary>
    /// Determines if the operation should be retried based on the exception.
    /// </summary>
    /// <param name="exception">The exception that occurred.</param>
    /// <param name="attemptNumber">The current attempt number (1-indexed).</param>
    /// <returns>True if the operation should be retried, false otherwise.</returns>
    bool ShouldRetry(Exception exception, int attemptNumber);

    /// <summary>
    /// Calculates the delay before the next retry attempt.
    /// </summary>
    /// <param name="attemptNumber">The current attempt number (1-indexed).</param>
    /// <returns>The delay duration before retrying.</returns>
    TimeSpan GetDelay(int attemptNumber);

    /// <summary>
    /// Maximum number of retry attempts allowed.
    /// </summary>
    int MaxRetries { get; }
}
