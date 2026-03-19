namespace Hazina.LLMs.Resilience;

/// <summary>
/// Token bucket rate limiter implementation.
/// </summary>
/// <remarks>
/// Implements the token bucket algorithm:
/// - Tokens are added to the bucket at a fixed rate (tokensPerSecond)
/// - Each request consumes one token
/// - If bucket is empty, requests wait until tokens are available
/// - Bucket has a maximum capacity to handle bursts
/// </remarks>
public class TokenBucketRateLimiter : IRateLimiter
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly int _tokensPerSecond;
    private readonly int _bucketCapacity;
    private double _availableTokens;
    private DateTime _lastRefillTime;
    private DateTime? _rateLimitResetTime;

    /// <summary>
    /// Creates a new token bucket rate limiter.
    /// </summary>
    /// <param name="tokensPerSecond">Number of tokens added per second (requests/second).</param>
    /// <param name="bucketCapacity">Maximum bucket capacity for handling bursts.</param>
    public TokenBucketRateLimiter(int tokensPerSecond, int? bucketCapacity = null)
    {
        if (tokensPerSecond <= 0)
            throw new ArgumentOutOfRangeException(nameof(tokensPerSecond), "Tokens per second must be positive.");

        _tokensPerSecond = tokensPerSecond;
        _bucketCapacity = bucketCapacity ?? tokensPerSecond * 2; // Default: allow 2-second burst
        _availableTokens = _bucketCapacity;
        _lastRefillTime = DateTime.UtcNow;
    }

    /// <inheritdoc/>
    public async Task WaitAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            // If we're in a rate limit period, wait until reset
            if (_rateLimitResetTime.HasValue)
            {
                var waitTime = _rateLimitResetTime.Value - DateTime.UtcNow;
                if (waitTime > TimeSpan.Zero)
                {
                    await Task.Delay(waitTime, cancellationToken);
                    _rateLimitResetTime = null; // Reset the rate limit period
                }
            }

            while (true)
            {
                RefillTokens();

                if (_availableTokens >= 1)
                {
                    _availableTokens -= 1;
                    return;
                }

                // Calculate wait time for next token
                var tokensNeeded = 1 - _availableTokens;
                var waitMs = (int)Math.Ceiling(tokensNeeded / _tokensPerSecond * 1000);
                var waitTime = TimeSpan.FromMilliseconds(Math.Min(waitMs, 1000));

                _semaphore.Release();
                await Task.Delay(waitTime, cancellationToken);
                await _semaphore.WaitAsync(cancellationToken);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <inheritdoc/>
    public void RecordSuccess()
    {
        // Token already consumed in WaitAsync
    }

    /// <inheritdoc/>
    public void RecordRateLimit(TimeSpan? retryAfter = null)
    {
        _semaphore.Wait();
        try
        {
            // Set rate limit reset time
            _rateLimitResetTime = DateTime.UtcNow + (retryAfter ?? TimeSpan.FromSeconds(60));

            // Empty the bucket to force waiting
            _availableTokens = 0;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private void RefillTokens()
    {
        var now = DateTime.UtcNow;
        var timeSinceLastRefill = now - _lastRefillTime;
        var tokensToAdd = timeSinceLastRefill.TotalSeconds * _tokensPerSecond;

        _availableTokens = Math.Min(_availableTokens + tokensToAdd, _bucketCapacity);
        _lastRefillTime = now;
    }
}
