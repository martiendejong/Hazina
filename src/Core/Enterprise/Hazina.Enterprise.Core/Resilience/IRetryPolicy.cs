namespace Hazina.Enterprise.Core.Resilience;

/// <summary>
/// Interface for retry policy implementation
/// </summary>
public interface IRetryPolicy
{
    /// <summary>
    /// Execute an operation with retry logic
    /// </summary>
    /// <typeparam name="T">The return type</typeparam>
    /// <param name="operation">The operation to execute</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The result of the operation</returns>
    Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Execute an operation with retry logic (void return)
    /// </summary>
    /// <param name="operation">The operation to execute</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ExecuteAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Strategy for calculating retry delays
/// </summary>
public enum RetryStrategy
{
    /// <summary>
    /// Fixed delay between retries
    /// </summary>
    FixedDelay,

    /// <summary>
    /// Exponential backoff with optional jitter
    /// </summary>
    ExponentialBackoff,

    /// <summary>
    /// Linear backoff (delay increases linearly)
    /// </summary>
    LinearBackoff
}
