using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Hazina.Enterprise.Core.RateLimiting;

/// <summary>
/// Configuration for token bucket rate limiter
/// </summary>
public class TokenBucketRateLimiterOptions
{
    /// <summary>
    /// Maximum number of tokens in the bucket
    /// </summary>
    public int MaxTokens { get; set; } = 100;

    /// <summary>
    /// Number of tokens to add per refill interval
    /// </summary>
    public int TokensPerRefill { get; set; } = 10;

    /// <summary>
    /// Interval for refilling tokens
    /// </summary>
    public TimeSpan RefillInterval { get; set; } = TimeSpan.FromSeconds(1);
}

/// <summary>
/// Token bucket rate limiter implementation
/// </summary>
public class TokenBucketRateLimiter : IRateLimiter, IDisposable
{
    private readonly TokenBucketRateLimiterOptions _options;
    private readonly ILogger<TokenBucketRateLimiter> _logger;
    private readonly ConcurrentDictionary<string, TokenBucket> _buckets = new();
    private readonly Timer _refillTimer;
    private bool _disposed;

    public TokenBucketRateLimiter(
        TokenBucketRateLimiterOptions options,
        ILogger<TokenBucketRateLimiter> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Start refill timer
        _refillTimer = new Timer(RefillBuckets, null, _options.RefillInterval, _options.RefillInterval);

        _logger.LogInformation("[RATE LIMITER] Initialized with MaxTokens: {MaxTokens}, RefillRate: {TokensPerRefill}/{Interval}",
            _options.MaxTokens, _options.TokensPerRefill, _options.RefillInterval);
    }

    public Task<bool> TryAcquireAsync(
        string key,
        int cost = 1,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentNullException(nameof(key));
        if (cost <= 0)
            throw new ArgumentOutOfRangeException(nameof(cost), "Cost must be positive");

        var bucket = GetOrCreateBucket(key);

        lock (bucket.Lock)
        {
            if (bucket.AvailableTokens >= cost)
            {
                bucket.AvailableTokens -= cost;
                _logger.LogDebug("[RATE LIMITER] Acquired {Cost} tokens for key '{Key}'. Remaining: {Remaining}",
                    cost, key, bucket.AvailableTokens);
                return Task.FromResult(true);
            }

            _logger.LogWarning("[RATE LIMITER] Rate limit exceeded for key '{Key}'. Requested: {Cost}, Available: {Available}",
                key, cost, bucket.AvailableTokens);
            return Task.FromResult(false);
        }
    }

    public async Task AcquireAsync(
        string key,
        int cost = 1,
        CancellationToken cancellationToken = default)
    {
        while (true)
        {
            if (await TryAcquireAsync(key, cost, cancellationToken))
            {
                return;
            }

            // Wait for next refill
            await Task.Delay(_options.RefillInterval, cancellationToken);
        }
    }

    public Task<RateLimitStatus> GetStatusAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentNullException(nameof(key));

        var bucket = GetOrCreateBucket(key);

        lock (bucket.Lock)
        {
            var nextRefillAt = bucket.LastRefillTime + _options.RefillInterval;

            return Task.FromResult(new RateLimitStatus
            {
                AvailableTokens = bucket.AvailableTokens,
                MaxTokens = _options.MaxTokens,
                NextTokenAvailableAt = bucket.AvailableTokens <= 0 ? nextRefillAt : null
            });
        }
    }

    public Task ResetAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentNullException(nameof(key));

        _buckets.TryRemove(key, out _);
        _logger.LogInformation("[RATE LIMITER] Reset rate limit for key '{Key}'", key);

        return Task.CompletedTask;
    }

    private TokenBucket GetOrCreateBucket(string key)
    {
        return _buckets.GetOrAdd(key, _ => new TokenBucket
        {
            AvailableTokens = _options.MaxTokens,
            LastRefillTime = DateTimeOffset.UtcNow,
            Lock = new object()
        });
    }

    private void RefillBuckets(object? state)
    {
        try
        {
            var now = DateTimeOffset.UtcNow;
            var refillCount = 0;

            foreach (var bucket in _buckets.Values)
            {
                lock (bucket.Lock)
                {
                    var elapsed = now - bucket.LastRefillTime;
                    if (elapsed >= _options.RefillInterval)
                    {
                        var tokensToAdd = _options.TokensPerRefill;
                        bucket.AvailableTokens = Math.Min(
                            bucket.AvailableTokens + tokensToAdd,
                            _options.MaxTokens);
                        bucket.LastRefillTime = now;
                        refillCount++;
                    }
                }
            }

            if (refillCount > 0)
            {
                _logger.LogDebug("[RATE LIMITER] Refilled {Count} buckets", refillCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RATE LIMITER] Error during bucket refill");
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _refillTimer?.Dispose();
        _buckets.Clear();
        _disposed = true;

        _logger.LogInformation("[RATE LIMITER] Disposed");
    }

    private class TokenBucket
    {
        public int AvailableTokens { get; set; }
        public DateTimeOffset LastRefillTime { get; set; }
        public object Lock { get; set; } = new();
    }
}

/// <summary>
/// Exception thrown when rate limit is exceeded
/// </summary>
public class RateLimitExceededException : Exception
{
    public RateLimitStatus Status { get; }

    public RateLimitExceededException(string message, RateLimitStatus status)
        : base(message)
    {
        Status = status;
    }
}
