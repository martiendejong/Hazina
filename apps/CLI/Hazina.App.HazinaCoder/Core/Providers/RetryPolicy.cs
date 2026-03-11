namespace Hazina.App.HazinaCoder.Core.Providers;

/// <summary>
/// Retry policy with exponential backoff
/// Iteration 28: Resilience patterns
/// </summary>
public class RetryPolicy
{
    private readonly int _maxRetries;
    private readonly TimeSpan _initialDelay;
    private readonly double _backoffMultiplier;

    public RetryPolicy(int maxRetries = 3, TimeSpan? initialDelay = null, double backoffMultiplier = 2.0)
    {
        _maxRetries = maxRetries;
        _initialDelay = initialDelay ?? TimeSpan.FromSeconds(1);
        _backoffMultiplier = backoffMultiplier;
    }

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> action, Func<Exception, bool>? shouldRetry = null)
    {
        var attempt = 0;
        Exception? lastException = null;

        while (attempt < _maxRetries)
        {
            try
            {
                return await action();
            }
            catch (Exception ex)
            {
                lastException = ex;

                if (shouldRetry != null && !shouldRetry(ex))
                    throw;

                attempt++;
                if (attempt >= _maxRetries)
                    throw;

                var delay = _initialDelay * Math.Pow(_backoffMultiplier, attempt - 1);
                var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 1000));

                await Task.Delay(delay + jitter);
            }
        }

        throw lastException ?? new Exception("Retry failed");
    }
}

/// <summary>
/// Rate limiter for API calls
/// Iteration 29: Rate limiting
/// </summary>
public class RateLimiter
{
    private readonly int _maxRequests;
    private readonly TimeSpan _timeWindow;
    private readonly Queue<DateTime> _requestTimes = new();
    private readonly SemaphoreSlim _semaphore = new(1);

    public RateLimiter(int maxRequests, TimeSpan timeWindow)
    {
        _maxRequests = maxRequests;
        _timeWindow = timeWindow;
    }

    public async Task WaitAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            var now = DateTime.UtcNow;
            var cutoff = now - _timeWindow;

            while (_requestTimes.Count > 0 && _requestTimes.Peek() < cutoff)
                _requestTimes.Dequeue();

            if (_requestTimes.Count >= _maxRequests)
            {
                var oldestRequest = _requestTimes.Peek();
                var waitTime = oldestRequest.Add(_timeWindow) - now;
                if (waitTime > TimeSpan.Zero)
                    await Task.Delay(waitTime, cancellationToken);

                _requestTimes.Dequeue();
            }

            _requestTimes.Enqueue(now);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}

/// <summary>
/// Connection pool for LLM clients
/// Iteration 30: Connection pooling
/// </summary>
public class ConnectionPool<T> where T : class
{
    private readonly Func<T> _factory;
    private readonly int _maxSize;
    private readonly Queue<T> _available = new();
    private readonly HashSet<T> _inUse = new();
    private readonly SemaphoreSlim _semaphore;

    public ConnectionPool(Func<T> factory, int maxSize = 10)
    {
        _factory = factory;
        _maxSize = maxSize;
        _semaphore = new SemaphoreSlim(maxSize);
    }

    public async Task<PooledConnection> AcquireAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);

        lock (_available)
        {
            T connection;
            if (_available.Count > 0)
                connection = _available.Dequeue();
            else
                connection = _factory();

            _inUse.Add(connection);
            return new PooledConnection(this, connection);
        }
    }

    private void Release(T connection)
    {
        lock (_available)
        {
            _inUse.Remove(connection);
            _available.Enqueue(connection);
        }
        _semaphore.Release();
    }

    public class PooledConnection : IDisposable
    {
        private readonly ConnectionPool<T> _pool;
        public T Connection { get; }

        public PooledConnection(ConnectionPool<T> pool, T connection)
        {
            _pool = pool;
            Connection = connection;
        }

        public void Dispose() => _pool.Release(Connection);
    }
}
