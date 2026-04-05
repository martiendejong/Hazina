using System.Net;

namespace Hazina.LLMs.Resilience;

/// <summary>
/// Retry strategy using exponential backoff with jitter.
/// </summary>
/// <remarks>
/// Implements exponential backoff: delay = baseDelay * 2^attemptNumber + random jitter.
/// Prevents thundering herd problem when multiple clients retry simultaneously.
/// </remarks>
public class ExponentialBackoffStrategy : IRetryStrategy
{
    private readonly int _maxRetries;
    private readonly TimeSpan _baseDelay;
    private readonly TimeSpan _maxDelay;
    private readonly double _jitterFactor;
    private readonly Random _random = new();

    /// <summary>
    /// Creates a new exponential backoff strategy.
    /// </summary>
    /// <param name="maxRetries">Maximum number of retry attempts (default: 3).</param>
    /// <param name="baseDelay">Base delay for the first retry (default: 1 second).</param>
    /// <param name="maxDelay">Maximum delay cap (default: 30 seconds).</param>
    /// <param name="jitterFactor">Jitter factor for randomization (0.0 - 1.0, default: 0.2).</param>
    public ExponentialBackoffStrategy(
        int maxRetries = 3,
        TimeSpan? baseDelay = null,
        TimeSpan? maxDelay = null,
        double jitterFactor = 0.2)
    {
        if (maxRetries < 0)
            throw new ArgumentOutOfRangeException(nameof(maxRetries), "Max retries must be non-negative.");
        if (jitterFactor < 0 || jitterFactor > 1)
            throw new ArgumentOutOfRangeException(nameof(jitterFactor), "Jitter factor must be between 0 and 1.");

        _maxRetries = maxRetries;
        _baseDelay = baseDelay ?? TimeSpan.FromSeconds(1);
        _maxDelay = maxDelay ?? TimeSpan.FromSeconds(30);
        _jitterFactor = jitterFactor;
    }

    /// <inheritdoc/>
    public int MaxRetries => _maxRetries;

    /// <inheritdoc/>
    public bool ShouldRetry(Exception exception, int attemptNumber)
    {
        if (attemptNumber > _maxRetries)
            return false;

        // Retry on rate limits (429) and server errors (5xx)
        if (exception is HttpRequestException httpEx)
        {
            return httpEx.StatusCode is
                HttpStatusCode.TooManyRequests or
                HttpStatusCode.InternalServerError or
                HttpStatusCode.BadGateway or
                HttpStatusCode.ServiceUnavailable or
                HttpStatusCode.GatewayTimeout;
        }

        // Retry on timeout exceptions
        if (exception is TaskCanceledException or TimeoutException)
            return true;

        // Retry on specific I/O exceptions
        if (exception is IOException)
            return true;

        return false;
    }

    /// <inheritdoc/>
    public TimeSpan GetDelay(int attemptNumber)
    {
        // Calculate exponential delay: baseDelay * 2^(attemptNumber - 1)
        var exponentialDelay = _baseDelay.TotalMilliseconds * Math.Pow(2, attemptNumber - 1);

        // Cap at max delay
        exponentialDelay = Math.Min(exponentialDelay, _maxDelay.TotalMilliseconds);

        // Add jitter: random variation of ±jitterFactor
        var jitter = exponentialDelay * _jitterFactor * (2 * _random.NextDouble() - 1);
        var finalDelay = exponentialDelay + jitter;

        // Ensure non-negative
        finalDelay = Math.Max(0, finalDelay);

        return TimeSpan.FromMilliseconds(finalDelay);
    }
}
