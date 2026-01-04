namespace Hazina.Enterprise.Core.Resilience;

/// <summary>
/// Represents the state of a circuit breaker
/// </summary>
public enum CircuitBreakerState
{
    /// <summary>
    /// Circuit is closed, requests are allowed through
    /// </summary>
    Closed,

    /// <summary>
    /// Circuit is open, requests are blocked
    /// </summary>
    Open,

    /// <summary>
    /// Circuit is half-open, limited requests are allowed for testing
    /// </summary>
    HalfOpen
}

/// <summary>
/// Event arguments for circuit breaker state changes
/// </summary>
public class CircuitBreakerStateChangedEventArgs : EventArgs
{
    /// <summary>
    /// The previous state
    /// </summary>
    public CircuitBreakerState PreviousState { get; set; }

    /// <summary>
    /// The new state
    /// </summary>
    public CircuitBreakerState NewState { get; set; }

    /// <summary>
    /// The timestamp of the state change
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// The reason for the state change
    /// </summary>
    public string? Reason { get; set; }
}
