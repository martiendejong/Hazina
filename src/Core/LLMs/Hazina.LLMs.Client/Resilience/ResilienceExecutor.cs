using System.Net;

namespace Hazina.LLMs.Resilience;

/// <summary>
/// Executes operations with retry and rate limiting capabilities.
/// </summary>
/// <remarks>
/// Combines retry strategies and rate limiting to provide resilient execution:
/// 1. Rate limiter controls request rate
/// 2. Retry strategy handles transient failures
/// 3. Automatic rate limit detection and backoff
/// </remarks>
public class ResilienceExecutor
{
    private readonly IRetryStrategy _retryStrategy;
    private readonly IRateLimiter? _rateLimiter;

    /// <summary>
    /// Creates a new resilience executor.
    /// </summary>
    /// <param name="retryStrategy">The retry strategy to use.</param>
    /// <param name="rateLimiter">Optional rate limiter.</param>
    public ResilienceExecutor(IRetryStrategy retryStrategy, IRateLimiter? rateLimiter = null)
    {
        _retryStrategy = retryStrategy ?? throw new ArgumentNullException(nameof(retryStrategy));
        _rateLimiter = rateLimiter;
    }

    /// <summary>
    /// Executes an operation with retry and rate limiting.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="operation">The operation to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the operation.</returns>
    /// <exception cref="Exception">Throws the last exception if all retries fail.</exception>
    public async Task<T> ExecuteAsync<T>(
        Func<Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        Exception? lastException = null;
        int attemptNumber = 0;

        while (attemptNumber <= _retryStrategy.MaxRetries)
        {
            attemptNumber++;

            try
            {
                // Apply rate limiting before the request
                if (_rateLimiter != null)
                {
                    await _rateLimiter.WaitAsync(cancellationToken);
                }

                // Execute the operation
                var result = await operation();

                // Record success for rate limiter
                _rateLimiter?.RecordSuccess();

                return result;
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                lastException = ex;

                // Check if this is a rate limit error
                if (IsRateLimitError(ex))
                {
                    var retryAfter = ExtractRetryAfter(ex);
                    _rateLimiter?.RecordRateLimit(retryAfter);
                }

                // Check if we should retry
                if (!_retryStrategy.ShouldRetry(ex, attemptNumber))
                {
                    throw;
                }

                // Don't delay on the last attempt
                if (attemptNumber <= _retryStrategy.MaxRetries)
                {
                    var delay = _retryStrategy.GetDelay(attemptNumber);
                    await Task.Delay(delay, cancellationToken);
                }
            }
        }

        // All retries exhausted
        throw lastException ?? new InvalidOperationException("Operation failed without exception");
    }

    /// <summary>
    /// Executes an operation with retry and rate limiting (void return).
    /// </summary>
    /// <param name="operation">The operation to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task ExecuteAsync(
        Func<Task> operation,
        CancellationToken cancellationToken = default)
    {
        await ExecuteAsync(async () =>
        {
            await operation();
            return true; // Dummy return value
        }, cancellationToken);
    }

    private static bool IsRateLimitError(Exception exception)
    {
        return exception is HttpRequestException httpEx
            && httpEx.StatusCode == HttpStatusCode.TooManyRequests;
    }

    private static TimeSpan? ExtractRetryAfter(Exception exception)
    {
        // Try to extract Retry-After header from HttpRequestException
        if (exception is HttpRequestException httpEx)
        {
            // Note: Retry-After extraction would require access to response headers
            // This is a placeholder for more sophisticated extraction logic
            // In practice, this would need to be done at the HTTP client level
            return null;
        }

        return null;
    }
}
