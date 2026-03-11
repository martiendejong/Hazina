namespace Hazina.EventSourcing;

/// <summary>
/// Event store interface for append-only event persistence.
/// Provides complete audit trail and enables event replay for transparency.
/// </summary>
public interface IEventStore
{
    /// <summary>
    /// Appends a new event to the event store.
    /// Events are immutable once appended (append-only).
    /// </summary>
    /// <typeparam name="T">Event type</typeparam>
    /// <param name="event">Event to append</param>
    /// <param name="aggregateId">Optional aggregate identifier (e.g., task ID, session ID)</param>
    /// <param name="correlationId">Optional correlation ID for tracking related events</param>
    /// <returns>Sequence ID of the appended event</returns>
    Task<long> AppendAsync<T>(T @event, string? aggregateId = null, string? correlationId = null) where T : class;

    /// <summary>
    /// Retrieves all events of a specific type, optionally filtered by time range.
    /// </summary>
    /// <typeparam name="T">Event type to retrieve</typeparam>
    /// <param name="since">Optional minimum timestamp (UTC)</param>
    /// <param name="until">Optional maximum timestamp (UTC)</param>
    /// <returns>List of events in chronological order</returns>
    Task<List<T>> GetEventsAsync<T>(DateTime? since = null, DateTime? until = null) where T : class;

    /// <summary>
    /// Retrieves all events for a specific aggregate, regardless of type.
    /// </summary>
    /// <param name="aggregateId">Aggregate identifier</param>
    /// <param name="since">Optional minimum timestamp (UTC)</param>
    /// <returns>List of events in chronological order</returns>
    Task<List<StoredEvent>> GetAggregateEventsAsync(string aggregateId, DateTime? since = null);

    /// <summary>
    /// Retrieves all events (any type) in chronological order.
    /// Useful for rebuilding projections or auditing.
    /// </summary>
    /// <param name="since">Optional minimum timestamp (UTC)</param>
    /// <param name="limit">Optional maximum number of events to return</param>
    /// <returns>List of stored events with metadata</returns>
    Task<List<StoredEvent>> GetAllEventsAsync(DateTime? since = null, int? limit = null);

    /// <summary>
    /// Gets the total count of events in the store.
    /// </summary>
    Task<long> GetEventCountAsync();

    /// <summary>
    /// Subscribes to real-time event notifications.
    /// Callback is invoked whenever a new event is appended.
    /// </summary>
    /// <param name="callback">Callback to invoke on new events</param>
    /// <returns>Subscription handle (dispose to unsubscribe)</returns>
    IDisposable Subscribe(Action<StoredEvent> callback);
}

/// <summary>
/// Stored event with metadata.
/// Immutable record of what happened in the system.
/// </summary>
public record StoredEvent
{
    /// <summary>
    /// Auto-incrementing sequence ID (primary key).
    /// Guarantees chronological ordering.
    /// </summary>
    public required long SequenceId { get; init; }

    /// <summary>
    /// Unique event identifier (UUID).
    /// </summary>
    public required string EventId { get; init; }

    /// <summary>
    /// Event type name (e.g., "IntentReceivedEvent", "ViewOpenedEvent").
    /// </summary>
    public required string EventType { get; init; }

    /// <summary>
    /// Optional aggregate identifier (e.g., task ID, session ID).
    /// Groups related events together.
    /// </summary>
    public string? AggregateId { get; init; }

    /// <summary>
    /// Optional correlation ID for tracking related events across aggregates.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Timestamp when event was appended (UTC).
    /// </summary>
    public required DateTime Timestamp { get; init; }

    /// <summary>
    /// Event payload (JSON serialized).
    /// </summary>
    public required string Payload { get; init; }

    /// <summary>
    /// Optional metadata (user ID, source, etc.).
    /// </summary>
    public string? Metadata { get; init; }
}
