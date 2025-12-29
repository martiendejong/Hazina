namespace Hazina.AI.Providers.Resilience;

/// <summary>
/// Retry policy for provider operations
/// </summary>
public class RetryPolicy
{
    private readonly int _maxRetries;
    private readonly TimeSpan _initialDelay;
    private readonly double _backoffMultiplier;
    private readonly TimeSpan _maxDelay;

    public RetryPolicy(
        int maxRetries = 3,
        TimeSpan? initialDelay = null,
        double backoffMultiplier = 2.0,
        TimeSpan? maxDelay = null)
    {
        _maxRetries = maxRetries;
        _initialDelay = initialDelay ?? TimeSpan.FromSeconds(1);
        _backoffMultiplier = backoffMultiplier;
        _maxDelay = maxDelay ?? TimeSpan.FromMinutes(1);
    }

    /// <summary>
    /// Execute action with retry logic
    /// </summary>
    public async Task<T> ExecuteAsync<T>(
        Func<Task<T>> action,
        Func<Exception, bool>? shouldRetry = null,
        Action<int, Exception>? onRetry = null,
        CancellationToken cancellationToken = default)
    {
        shouldRetry ??= _ => true; // Retry on all exceptions by default
        int attempt = 0;
        Exception? lastException = null;

        while (attempt <= _maxRetries)
        {
            try
            {
                return await action();
            }
            catch (Exception ex) when (shouldRetry(ex) && attempt < _maxRetries)
            {
                lastException = ex;
                attempt++;

                onRetry?.Invoke(attempt, ex);

                var delay = CalculateDelay(attempt);
                await Task.Delay(delay, cancellationToken);
            }
        }

        // If we get here, all retries failed
        throw lastException ?? new Exception("Retry failed with unknown error");
    }

    /// <summary>
    /// Calculate delay using exponential backoff
    /// </summary>
    private TimeSpan CalculateDelay(int attempt)
    {
        var delay = TimeSpan.FromMilliseconds(
            _initialDelay.TotalMilliseconds * Math.Pow(_backoffMultiplier, attempt - 1)
        );

        return delay > _maxDelay ? _maxDelay : delay;
    }
}
