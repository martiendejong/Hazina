namespace Hazina.AgentFactory.Abstractions;

/// <summary>
/// Abstraction for time operations to enable testability.
/// Allows mocking time in tests for deterministic behavior.
/// </summary>
public interface IClock
{
    /// <summary>
    /// Gets the current UTC date and time.
    /// </summary>
    DateTime UtcNow { get; }

    /// <summary>
    /// Gets the current local date and time.
    /// </summary>
    DateTime Now { get; }

    /// <summary>
    /// Gets the current date (no time component).
    /// </summary>
    DateTime Today { get; }
}

/// <summary>
/// Default implementation using system time.
/// </summary>
public class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
    public DateTime Now => DateTime.Now;
    public DateTime Today => DateTime.Today;
}

/// <summary>
/// Fixed time implementation for testing.
/// </summary>
public class FixedClock : IClock
{
    private readonly DateTime _fixedTime;

    public FixedClock(DateTime fixedTime)
    {
        _fixedTime = fixedTime;
    }

    public DateTime UtcNow => _fixedTime.ToUniversalTime();
    public DateTime Now => _fixedTime.ToLocalTime();
    public DateTime Today => _fixedTime.Date;

    /// <summary>
    /// Creates a fixed clock at the current time (useful for test snapshots).
    /// </summary>
    public static FixedClock AtNow() => new FixedClock(DateTime.UtcNow);

    /// <summary>
    /// Creates a fixed clock at a specific time.
    /// </summary>
    public static FixedClock At(DateTime time) => new FixedClock(time);
}
