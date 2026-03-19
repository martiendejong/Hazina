using System.Net;

namespace Hazina.LLMs.Resilience;

/// <summary>
/// Exponential backoff retry strategy with jitter to prevent thundering herd.
/// </summary>
public class ExponentialBackoffStrategy : IRetryStrategy
{
    private readonly Random _random = new();

    public int MaxRetries { get; }
    public TimeSpan InitialDelay { get; }
    public double Multiplier { get; }
    public TimeSpan MaxDelay { get; }
    public bool UseJitter { get; }

    public ExponentialBackoffStrategy(
        int maxRetries = 3,
        TimeSpan? initialDelay = null,
        double multiplier = 2.0,
        TimeSpan? maxDelay = null,
        bool useJitter = true)
    {
        MaxRetries = maxRetries;
        InitialDelay = initialDelay ?? TimeSpan.FromMilliseconds(200);
        Multiplier = multiplier;
        MaxDelay = maxDelay ?? TimeSpan.FromMinutes(1);
        UseJitter = useJitter;
    }

    public TimeSpan GetDelay(int attemptNumber)
    {
        var exponentialDelay = InitialDelay.TotalMilliseconds * Math.Pow(Multiplier, attemptNumber - 1);
        var delay = Math.Min(exponentialDelay, MaxDelay.TotalMilliseconds);

        if (UseJitter)
        {
            // Add ±25% jitter to prevent synchronized retries
            var jitterFactor = 0.75 + (_random.NextDouble() * 0.5);
            delay *= jitterFactor;
        }

        return TimeSpan.FromMilliseconds(delay);
    }

    public bool ShouldRetry(Exception exception)
    {
        return exception is HttpRequestException ||
               exception is TaskCanceledException ||
               IsRateLimitException(exception) ||
               IsTransientException(exception);
    }

    private static bool IsRateLimitException(Exception exception)
    {
        if (exception is HttpRequestException httpEx)
        {
            var statusCode = httpEx.StatusCode;
            return statusCode == HttpStatusCode.TooManyRequests ||
                   statusCode == (HttpStatusCode)429;
        }
        return exception.Message.Contains("rate limit", StringComparison.OrdinalIgnoreCase) ||
               exception.Message.Contains("quota exceeded", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTransientException(Exception exception)
    {
        if (exception is HttpRequestException httpEx)
        {
            var statusCode = httpEx.StatusCode;
            return statusCode == HttpStatusCode.RequestTimeout ||
                   statusCode == HttpStatusCode.GatewayTimeout ||
                   statusCode == HttpStatusCode.ServiceUnavailable ||
                   statusCode == HttpStatusCode.BadGateway ||
                   statusCode >= HttpStatusCode.InternalServerError;
        }
        return false;
    }
}

/// <summary>
/// Linear backoff retry strategy with constant delay increments.
/// </summary>
public class LinearBackoffStrategy : IRetryStrategy
{
    public int MaxRetries { get; }
    public TimeSpan DelayIncrement { get; }
    public TimeSpan MaxDelay { get; }

    public LinearBackoffStrategy(
        int maxRetries = 3,
        TimeSpan? delayIncrement = null,
        TimeSpan? maxDelay = null)
    {
        MaxRetries = maxRetries;
        DelayIncrement = delayIncrement ?? TimeSpan.FromSeconds(1);
        MaxDelay = maxDelay ?? TimeSpan.FromMinutes(1);
    }

    public TimeSpan GetDelay(int attemptNumber)
    {
        var delay = DelayIncrement.TotalMilliseconds * attemptNumber;
        return TimeSpan.FromMilliseconds(Math.Min(delay, MaxDelay.TotalMilliseconds));
    }

    public bool ShouldRetry(Exception exception)
    {
        return exception is HttpRequestException ||
               exception is TaskCanceledException;
    }
}

/// <summary>
/// Constant delay retry strategy - same delay between all retries.
/// </summary>
public class ConstantBackoffStrategy : IRetryStrategy
{
    public int MaxRetries { get; }
    public TimeSpan Delay { get; }

    public ConstantBackoffStrategy(
        int maxRetries = 3,
        TimeSpan? delay = null)
    {
        MaxRetries = maxRetries;
        Delay = delay ?? TimeSpan.FromMilliseconds(500);
    }

    public TimeSpan GetDelay(int attemptNumber)
    {
        return Delay;
    }

    public bool ShouldRetry(Exception exception)
    {
        return exception is HttpRequestException ||
               exception is TaskCanceledException;
    }
}

/// <summary>
/// No retry strategy - fail immediately without retries.
/// </summary>
public class NoRetryStrategy : IRetryStrategy
{
    public int MaxRetries => 0;

    public TimeSpan GetDelay(int attemptNumber)
    {
        return TimeSpan.Zero;
    }

    public bool ShouldRetry(Exception exception)
    {
        return false;
    }
}
