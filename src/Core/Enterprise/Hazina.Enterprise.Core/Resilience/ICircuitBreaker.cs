namespace Hazina.Enterprise.Core.Resilience;

/// <summary>
/// Interface for circuit breaker pattern implementation
/// </summary>
public interface ICircuitBreaker
{
    /// <summary>
    /// Current state of the circuit breaker
    /// </summary>
    CircuitBreakerState State { get; }

    /// <summary>
    /// Event fired when the circuit breaker state changes
    /// </summary>
    event EventHandler<CircuitBreakerStateChangedEventArgs>? StateChanged;

    /// <summary>
    /// Execute an operation through the circuit breaker
    /// </summary>
    /// <typeparam name="T">The return type</typeparam>
    /// <param name="operation">The operation to execute</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The result of the operation</returns>
    Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Execute an operation through the circuit breaker (void return)
    /// </summary>
    /// <param name="operation">The operation to execute</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ExecuteAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Manually reset the circuit breaker to closed state
    /// </summary>
    void Reset();

    /// <summary>
    /// Get circuit breaker statistics
    /// </summary>
    /// <returns>Circuit breaker statistics</returns>
    CircuitBreakerStatistics GetStatistics();
}

/// <summary>
/// Statistics for circuit breaker monitoring
/// </summary>
public class CircuitBreakerStatistics
{
    /// <summary>
    /// Total number of successful calls
    /// </summary>
    public long SuccessfulCalls { get; set; }

    /// <summary>
    /// Total number of failed calls
    /// </summary>
    public long FailedCalls { get; set; }

    /// <summary>
    /// Total number of rejected calls (when circuit is open)
    /// </summary>
    public long RejectedCalls { get; set; }

    /// <summary>
    /// Failure rate (0.0 to 1.0)
    /// </summary>
    public double FailureRate { get; set; }

    /// <summary>
    /// Time when the circuit was last opened
    /// </summary>
    public DateTime? LastOpenedAt { get; set; }

    /// <summary>
    /// Time when the circuit was last closed
    /// </summary>
    public DateTime? LastClosedAt { get; set; }
}
