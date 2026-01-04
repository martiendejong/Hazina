using Microsoft.Extensions.Logging;

namespace Hazina.Enterprise.Core.Resilience;

/// <summary>
/// Configuration for retry policy
/// </summary>
public class RetryPolicyOptions
{
    /// <summary>
    /// Maximum number of retry attempts
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Initial delay between retries
    /// </summary>
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Maximum delay between retries
    /// </summary>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Retry strategy to use
    /// </summary>
    public RetryStrategy Strategy { get; set; } = RetryStrategy.ExponentialBackoff;

    /// <summary>
    /// Whether to add jitter to exponential backoff
    /// </summary>
    public bool UseJitter { get; set; } = true;

    /// <summary>
    /// Predicate to determine if an exception should trigger a retry
    /// </summary>
    public Func<Exception, bool>? ShouldRetry { get; set; }
}

/// <summary>
/// Retry policy implementation for transient fault handling
/// </summary>
public class RetryPolicy : IRetryPolicy
{
    private readonly RetryPolicyOptions _options;
    private readonly ILogger<RetryPolicy> _logger;
    private static readonly Random _random = new();

    public RetryPolicy(RetryPolicyOptions options, ILogger<RetryPolicy> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Default retry predicate if not specified
        _options.ShouldRetry ??= DefaultShouldRetry;

        _logger.LogInformation("[RETRY POLICY] Initialized with MaxAttempts: {MaxAttempts}, Strategy: {Strategy}",
            _options.MaxRetryAttempts, _options.Strategy);
    }

    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        if (operation == null)
            throw new ArgumentNullException(nameof(operation));

        int attempt = 0;
        Exception? lastException = null;

        while (attempt <= _options.MaxRetryAttempts)
        {
            try
            {
                if (attempt > 0)
                {
                    _logger.LogInformation("[RETRY POLICY] Retry attempt {Attempt}/{MaxAttempts}",
                        attempt, _options.MaxRetryAttempts);
                }

                var result = await operation(cancellationToken);

                if (attempt > 0)
                {
                    _logger.LogInformation("[RETRY POLICY] Operation succeeded on attempt {Attempt}", attempt + 1);
                }

                return result;
            }
            catch (Exception ex) when (attempt < _options.MaxRetryAttempts && _options.ShouldRetry(ex))
            {
                lastException = ex;
                attempt++;

                var delay = CalculateDelay(attempt);

                _logger.LogWarning(ex,
                    "[RETRY POLICY] Operation failed on attempt {Attempt}. Retrying in {Delay}ms",
                    attempt, delay.TotalMilliseconds);

                await Task.Delay(delay, cancellationToken);
            }
        }

        _logger.LogError(lastException,
            "[RETRY POLICY] Operation failed after {Attempts} attempts", _options.MaxRetryAttempts + 1);

        throw new RetryPolicyExhaustedException(
            $"Operation failed after {_options.MaxRetryAttempts + 1} attempts",
            lastException);
    }

    public async Task ExecuteAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        await ExecuteAsync(async ct =>
        {
            await operation(ct);
            return 0; // Dummy return value
        }, cancellationToken);
    }

    private TimeSpan CalculateDelay(int attemptNumber)
    {
        TimeSpan delay = _options.Strategy switch
        {
            RetryStrategy.FixedDelay => _options.InitialDelay,
            RetryStrategy.LinearBackoff => TimeSpan.FromMilliseconds(
                _options.InitialDelay.TotalMilliseconds * attemptNumber),
            RetryStrategy.ExponentialBackoff => TimeSpan.FromMilliseconds(
                _options.InitialDelay.TotalMilliseconds * Math.Pow(2, attemptNumber - 1)),
            _ => _options.InitialDelay
        };

        // Apply jitter if enabled (for exponential backoff)
        if (_options.Strategy == RetryStrategy.ExponentialBackoff && _options.UseJitter)
        {
            var jitter = _random.NextDouble() * 0.3; // Up to 30% jitter
            delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * (1 + jitter));
        }

        // Cap at max delay
        if (delay > _options.MaxDelay)
        {
            delay = _options.MaxDelay;
        }

        return delay;
    }

    private static bool DefaultShouldRetry(Exception ex)
    {
        // Retry on common transient exceptions
        return ex is TimeoutException ||
               ex is HttpRequestException ||
               ex is System.Net.Sockets.SocketException ||
               (ex.InnerException != null && DefaultShouldRetry(ex.InnerException));
    }
}

/// <summary>
/// Exception thrown when retry policy is exhausted
/// </summary>
public class RetryPolicyExhaustedException : Exception
{
    public RetryPolicyExhaustedException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
