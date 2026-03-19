using System.Collections.Concurrent;

namespace WebSearch.Infrastructure;

/// <summary>
/// Token bucket rate limiter implementation
/// </summary>
public class TokenBucketRateLimiter : IRateLimiter
{
    private readonly ConcurrentDictionary<string, TokenBucket> _buckets = new();
    private readonly int _requestsPerMinute;

    public TokenBucketRateLimiter(int requestsPerMinute = 10)
    {
        if (requestsPerMinute <= 0)
            throw new ArgumentException("Requests per minute must be positive", nameof(requestsPerMinute));

        _requestsPerMinute = requestsPerMinute;
    }

    public async Task WaitAsync(string providerName, CancellationToken cancellationToken = default)
    {
        var bucket = _buckets.GetOrAdd(providerName, _ => new TokenBucket(_requestsPerMinute));
        await bucket.WaitForTokenAsync(cancellationToken);
    }

    public bool IsAllowed(string providerName)
    {
        var bucket = _buckets.GetOrAdd(providerName, _ => new TokenBucket(_requestsPerMinute));
        return bucket.TryConsume();
    }

    private class TokenBucket
    {
        private readonly int _capacity;
        private readonly TimeSpan _refillInterval;
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private double _tokens;
        private DateTime _lastRefill;

        public TokenBucket(int requestsPerMinute)
        {
            _capacity = requestsPerMinute;
            _tokens = requestsPerMinute;
            _refillInterval = TimeSpan.FromMinutes(1.0 / requestsPerMinute);
            _lastRefill = DateTime.UtcNow;
        }

        public async Task WaitForTokenAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                await _semaphore.WaitAsync(cancellationToken);
                try
                {
                    Refill();

                    if (_tokens >= 1.0)
                    {
                        _tokens -= 1.0;
                        return;
                    }

                    // Calculate wait time for next token
                    var waitTime = _refillInterval;
                    await Task.Delay(waitTime, cancellationToken);
                }
                finally
                {
                    _semaphore.Release();
                }
            }
        }

        public bool TryConsume()
        {
            _semaphore.Wait();
            try
            {
                Refill();

                if (_tokens >= 1.0)
                {
                    _tokens -= 1.0;
                    return true;
                }

                return false;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private void Refill()
        {
            var now = DateTime.UtcNow;
            var elapsed = now - _lastRefill;
            var tokensToAdd = elapsed.TotalMinutes * _capacity;

            _tokens = Math.Min(_capacity, _tokens + tokensToAdd);
            _lastRefill = now;
        }
    }
}
