namespace Hazina.AI.Providers.Resilience;

/// <summary>
/// Circuit breaker pattern implementation for provider resilience
/// </summary>
public class CircuitBreaker
{
    private readonly int _failureThreshold;
    private readonly TimeSpan _timeout;
    private readonly TimeSpan _resetTimeout;

    private int _failureCount = 0;
    private DateTime? _lastFailureTime;
    private CircuitBreakerState _state = CircuitBreakerState.Closed;
    private readonly object _lock = new();

    public CircuitBreaker(int failureThreshold = 5, TimeSpan? timeout = null, TimeSpan? resetTimeout = null)
    {
        _failureThreshold = failureThreshold;
        _timeout = timeout ?? TimeSpan.FromSeconds(30);
        _resetTimeout = resetTimeout ?? TimeSpan.FromMinutes(1);
    }

    /// <summary>
    /// Current state of the circuit breaker
    /// </summary>
    public CircuitBreakerState State
    {
        get
        {
            lock (_lock)
            {
                return _state;
            }
        }
    }

    /// <summary>
    /// Execute action with circuit breaker protection
    /// </summary>
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_state == CircuitBreakerState.Open)
            {
                // Check if we should attempt to reset
                if (_lastFailureTime.HasValue && DateTime.UtcNow - _lastFailureTime.Value >= _resetTimeout)
                {
                    _state = CircuitBreakerState.HalfOpen;
                }
                else
                {
                    throw new CircuitBreakerOpenException("Circuit breaker is open");
                }
            }
        }

        try
        {
            var result = await action();
            OnSuccess();
            return result;
        }
        catch (Exception)
        {
            OnFailure();
            throw;
        }
    }

    /// <summary>
    /// Record a successful operation
    /// </summary>
    private void OnSuccess()
    {
        lock (_lock)
        {
            _failureCount = 0;
            _lastFailureTime = null;

            if (_state == CircuitBreakerState.HalfOpen)
            {
                _state = CircuitBreakerState.Closed;
            }
        }
    }

    /// <summary>
    /// Record a failed operation
    /// </summary>
    private void OnFailure()
    {
        lock (_lock)
        {
            _failureCount++;
            _lastFailureTime = DateTime.UtcNow;

            if (_state == CircuitBreakerState.HalfOpen)
            {
                // If we fail in half-open state, go back to open
                _state = CircuitBreakerState.Open;
            }
            else if (_failureCount >= _failureThreshold)
            {
                _state = CircuitBreakerState.Open;
            }
        }
    }

    /// <summary>
    /// Reset the circuit breaker
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _failureCount = 0;
            _lastFailureTime = null;
            _state = CircuitBreakerState.Closed;
        }
    }

    /// <summary>
    /// Get current metrics
    /// </summary>
    public CircuitBreakerMetrics GetMetrics()
    {
        lock (_lock)
        {
            return new CircuitBreakerMetrics
            {
                State = _state,
                FailureCount = _failureCount,
                LastFailureTime = _lastFailureTime
            };
        }
    }
}

/// <summary>
/// Circuit breaker state
/// </summary>
public enum CircuitBreakerState
{
    Closed,    // Normal operation
    Open,      // Failing, rejecting requests
    HalfOpen   // Testing if recovered
}

/// <summary>
/// Circuit breaker metrics
/// </summary>
public class CircuitBreakerMetrics
{
    public CircuitBreakerState State { get; set; }
    public int FailureCount { get; set; }
    public DateTime? LastFailureTime { get; set; }
}

/// <summary>
/// Exception thrown when circuit breaker is open
/// </summary>
public class CircuitBreakerOpenException : Exception
{
    public CircuitBreakerOpenException(string message) : base(message) { }
}
