namespace Hazina.LLMs.Resilience;

/// <summary>
/// Executes operations with retry logic based on a configurable strategy.
/// </summary>
public class RetryExecutor
{
    private readonly IRetryStrategy _strategy;
    private readonly Action<string>? _logger;

    public RetryExecutor(IRetryStrategy strategy, Action<string>? logger = null)
    {
        _strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
        _logger = logger;
    }

    /// <summary>
    /// Executes an asynchronous operation with retry logic.
    /// </summary>
    public async Task<T> ExecuteAsync<T>(
        Func<Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        int attempt = 0;
        Exception? lastException = null;

        while (attempt <= _strategy.MaxRetries)
        {
            attempt++;

            try
            {
                return await operation();
            }
            catch (Exception ex) when (ShouldRetryAttempt(ex, attempt))
            {
                lastException = ex;
                var delay = _strategy.GetDelay(attempt);

                _logger?.Invoke($"Attempt {attempt}/{_strategy.MaxRetries + 1} failed: {ex.Message}. Retrying in {delay.TotalMilliseconds}ms...");

                await Task.Delay(delay, cancellationToken);
            }
        }

        // If we exhausted all retries, throw the last exception
        throw new RetryExhaustedException(
            $"Operation failed after {attempt} attempts.",
            lastException ?? new Exception("Unknown error"));
    }

    /// <summary>
    /// Executes a synchronous operation with retry logic.
    /// </summary>
    public T Execute<T>(Func<T> operation)
    {
        int attempt = 0;
        Exception? lastException = null;

        while (attempt <= _strategy.MaxRetries)
        {
            attempt++;

            try
            {
                return operation();
            }
            catch (Exception ex) when (ShouldRetryAttempt(ex, attempt))
            {
                lastException = ex;
                var delay = _strategy.GetDelay(attempt);

                _logger?.Invoke($"Attempt {attempt}/{_strategy.MaxRetries + 1} failed: {ex.Message}. Retrying in {delay.TotalMilliseconds}ms...");

                Thread.Sleep(delay);
            }
        }

        // If we exhausted all retries, throw the last exception
        throw new RetryExhaustedException(
            $"Operation failed after {attempt} attempts.",
            lastException ?? new Exception("Unknown error"));
    }

    private bool ShouldRetryAttempt(Exception exception, int attempt)
    {
        // Don't retry if we've exceeded max retries
        if (attempt > _strategy.MaxRetries)
            return false;

        // Check if the exception type is retryable
        return _strategy.ShouldRetry(exception);
    }
}

/// <summary>
/// Exception thrown when all retry attempts have been exhausted.
/// </summary>
public class RetryExhaustedException : Exception
{
    public RetryExhaustedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
