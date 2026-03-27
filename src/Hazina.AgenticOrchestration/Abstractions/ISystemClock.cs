namespace Hazina.AgenticOrchestration.Abstractions;

/// <summary>
/// Abstraction for time operations to enable testability.
/// Inject this instead of calling DateTime.Now / DateTime.UtcNow directly.
/// </summary>
public interface ISystemClock
{
    /// <summary>Gets the current UTC date and time.</summary>
    DateTime UtcNow { get; }

    /// <summary>Gets the current local date and time.</summary>
    DateTime Now { get; }

    /// <summary>Gets the current date (no time component).</summary>
    DateTime Today { get; }
}

/// <summary>
/// Default implementation of ISystemClock using system time.
/// </summary>
public class SystemClock : ISystemClock
{
    public DateTime UtcNow => DateTime.UtcNow;
    public DateTime Now => DateTime.Now;
    public DateTime Today => DateTime.Today;
}
