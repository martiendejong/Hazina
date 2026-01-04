using Microsoft.Extensions.Logging;

namespace Hazina.Enterprise.Core.Resilience;

/// <summary>
/// Configuration for circuit breaker
/// </summary>
public class CircuitBreakerOptions
{
    /// <summary>
    /// Number of failures before opening the circuit
    /// </summary>
    public int FailureThreshold { get; set; } = 5;

    /// <summary>
    /// Minimum number of calls before calculating failure rate
    /// </summary>
    public int MinimumThroughput { get; set; } = 10;

    /// <summary>
    /// Failure rate threshold (0.0 to 1.0) before opening circuit
    /// </summary>
    public double FailureRateThreshold { get; set; } = 0.5;

    /// <summary>
    /// Duration to wait before attempting to close the circuit
    /// </summary>
    public TimeSpan OpenDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Number of successful calls in half-open state before closing
    /// </summary>
    public int SuccessThreshold { get; set; } = 2;
}

/// <summary>
/// Circuit breaker implementation for fault tolerance
/// </summary>
public class CircuitBreaker : ICircuitBreaker
{
    private readonly CircuitBreakerOptions _options;
    private readonly ILogger<CircuitBreaker> _logger;
    private readonly object _lock = new();

    private CircuitBreakerState _state = CircuitBreakerState.Closed;
    private DateTime? _lastStateChangeTime;
    private long _successfulCalls;
    private long _failedCalls;
    private long _rejectedCalls;
    private int _halfOpenSuccesses;

    public CircuitBreakerState State
    {
        get
        {
            lock (_lock)
            {
                // Check if we should transition from Open to HalfOpen
                if (_state == CircuitBreakerState.Open &&
                    _lastStateChangeTime.HasValue &&
                    DateTime.UtcNow - _lastStateChangeTime.Value >= _options.OpenDuration)
                {
                    TransitionTo(CircuitBreakerState.HalfOpen, "Open duration elapsed");
                }

                return _state;
            }
        }
    }

    public event EventHandler<CircuitBreakerStateChangedEventArgs>? StateChanged;

    public CircuitBreaker(CircuitBreakerOptions options, ILogger<CircuitBreaker> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _logger.LogInformation("[CIRCUIT BREAKER] Initialized with FailureThreshold: {Threshold}, OpenDuration: {Duration}",
            _options.FailureThreshold, _options.OpenDuration);
    }

    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        if (operation == null)
            throw new ArgumentNullException(nameof(operation));

        // Check if circuit allows execution
        if (!CanExecute())
        {
            Interlocked.Increment(ref _rejectedCalls);
            _logger.LogWarning("[CIRCUIT BREAKER] Request rejected - circuit is OPEN");
            throw new CircuitBreakerOpenException("Circuit breaker is open");
        }

        try
        {
            var result = await operation(cancellationToken);
            OnSuccess();
            return result;
        }
        catch (Exception ex)
        {
            OnFailure(ex);
            throw;
        }
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

    public void Reset()
    {
        lock (_lock)
        {
            _logger.LogInformation("[CIRCUIT BREAKER] Manual reset triggered");
            TransitionTo(CircuitBreakerState.Closed, "Manual reset");
            ResetCounters();
        }
    }

    public CircuitBreakerStatistics GetStatistics()
    {
        lock (_lock)
        {
            var totalCalls = _successfulCalls + _failedCalls;
            var failureRate = totalCalls > 0 ? (double)_failedCalls / totalCalls : 0.0;

            return new CircuitBreakerStatistics
            {
                SuccessfulCalls = _successfulCalls,
                FailedCalls = _failedCalls,
                RejectedCalls = _rejectedCalls,
                FailureRate = failureRate,
                LastOpenedAt = _state == CircuitBreakerState.Open ? _lastStateChangeTime : null,
                LastClosedAt = _state == CircuitBreakerState.Closed ? _lastStateChangeTime : null
            };
        }
    }

    private bool CanExecute()
    {
        var currentState = State; // This property getter may update state
        return currentState != CircuitBreakerState.Open;
    }

    private void OnSuccess()
    {
        lock (_lock)
        {
            Interlocked.Increment(ref _successfulCalls);

            if (_state == CircuitBreakerState.HalfOpen)
            {
                _halfOpenSuccesses++;
                _logger.LogInformation("[CIRCUIT BREAKER] Half-open success {Count}/{Threshold}",
                    _halfOpenSuccesses, _options.SuccessThreshold);

                if (_halfOpenSuccesses >= _options.SuccessThreshold)
                {
                    TransitionTo(CircuitBreakerState.Closed, "Success threshold reached in half-open state");
                    ResetCounters();
                }
            }
        }
    }

    private void OnFailure(Exception ex)
    {
        lock (_lock)
        {
            Interlocked.Increment(ref _failedCalls);

            _logger.LogWarning(ex, "[CIRCUIT BREAKER] Operation failed");

            if (_state == CircuitBreakerState.HalfOpen)
            {
                TransitionTo(CircuitBreakerState.Open, "Failure in half-open state");
                return;
            }

            if (_state == CircuitBreakerState.Closed)
            {
                var totalCalls = _successfulCalls + _failedCalls;

                // Check if we have enough calls to evaluate
                if (totalCalls < _options.MinimumThroughput)
                    return;

                var failureRate = (double)_failedCalls / totalCalls;

                // Check failure threshold
                if (_failedCalls >= _options.FailureThreshold ||
                    failureRate >= _options.FailureRateThreshold)
                {
                    TransitionTo(CircuitBreakerState.Open,
                        $"Failure threshold exceeded: {_failedCalls} failures, {failureRate:P} failure rate");
                }
            }
        }
    }

    private void TransitionTo(CircuitBreakerState newState, string reason)
    {
        var previousState = _state;

        if (previousState == newState)
            return;

        _state = newState;
        _lastStateChangeTime = DateTime.UtcNow;

        if (newState == CircuitBreakerState.HalfOpen)
        {
            _halfOpenSuccesses = 0;
        }

        _logger.LogWarning("[CIRCUIT BREAKER] State transition: {PreviousState} → {NewState}. Reason: {Reason}",
            previousState, newState, reason);

        StateChanged?.Invoke(this, new CircuitBreakerStateChangedEventArgs
        {
            PreviousState = previousState,
            NewState = newState,
            Timestamp = DateTime.UtcNow,
            Reason = reason
        });
    }

    private void ResetCounters()
    {
        _successfulCalls = 0;
        _failedCalls = 0;
        _halfOpenSuccesses = 0;
    }
}

/// <summary>
/// Exception thrown when circuit breaker is open
/// </summary>
public class CircuitBreakerOpenException : Exception
{
    public CircuitBreakerOpenException(string message) : base(message)
    {
    }
}
