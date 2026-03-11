using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hazina.EventSourcing;

/// <summary>
/// SQLite-based event store implementation.
/// Provides durable, append-only event persistence with full audit trail.
/// </summary>
public class SQLiteEventStore : IEventStore, IDisposable
{
    private readonly string _connectionString;
    private readonly ILogger<SQLiteEventStore> _logger;
    private readonly List<Action<StoredEvent>> _subscribers = new();
    private readonly object _subscriberLock = new();

    public SQLiteEventStore(string dbPath, ILogger<SQLiteEventStore>? logger = null)
    {
        _connectionString = $"Data Source={dbPath}";
        _logger = logger ?? NullLogger<SQLiteEventStore>.Instance;
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        _logger.LogInformation("Initializing event store database at {ConnectionString}", _connectionString);

        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS events (
                sequence_id INTEGER PRIMARY KEY AUTOINCREMENT,
                event_id TEXT NOT NULL UNIQUE,
                event_type TEXT NOT NULL,
                aggregate_id TEXT,
                correlation_id TEXT,
                timestamp INTEGER NOT NULL,
                payload TEXT NOT NULL,
                metadata TEXT,
                CHECK (length(event_id) = 36)
            );

            CREATE INDEX IF NOT EXISTS idx_event_type ON events(event_type);
            CREATE INDEX IF NOT EXISTS idx_aggregate_id ON events(aggregate_id) WHERE aggregate_id IS NOT NULL;
            CREATE INDEX IF NOT EXISTS idx_correlation_id ON events(correlation_id) WHERE correlation_id IS NOT NULL;
            CREATE INDEX IF NOT EXISTS idx_timestamp ON events(timestamp);
        ";
        cmd.ExecuteNonQuery();

        _logger.LogInformation("Event store database initialized successfully");
    }

    public async Task<long> AppendAsync<T>(T @event, string? aggregateId = null, string? correlationId = null) where T : class
    {
        var eventType = typeof(T).Name;
        var eventId = Guid.NewGuid().ToString();
        var payload = JsonSerializer.Serialize(@event, new JsonSerializerOptions { WriteIndented = false });
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        _logger.LogDebug("Appending event {EventType} with ID {EventId}", eventType, eventId);

        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO events (event_id, event_type, aggregate_id, correlation_id, timestamp, payload, metadata)
            VALUES (@id, @type, @aggregate, @correlation, @timestamp, @payload, @metadata);
            SELECT last_insert_rowid();
        ";

        cmd.Parameters.AddWithValue("@id", eventId);
        cmd.Parameters.AddWithValue("@type", eventType);
        cmd.Parameters.AddWithValue("@aggregate", aggregateId ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@correlation", correlationId ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@timestamp", timestamp);
        cmd.Parameters.AddWithValue("@payload", payload);
        cmd.Parameters.AddWithValue("@metadata", DBNull.Value);

        var sequenceId = (long)(await cmd.ExecuteScalarAsync() ?? 0L);

        _logger.LogInformation("Event appended: {EventType} (sequence {SequenceId}, aggregate {AggregateId})",
            eventType, sequenceId, aggregateId ?? "none");

        // Notify subscribers
        var storedEvent = new StoredEvent
        {
            SequenceId = sequenceId,
            EventId = eventId,
            EventType = eventType,
            AggregateId = aggregateId,
            CorrelationId = correlationId,
            Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).UtcDateTime,
            Payload = payload
        };

        NotifySubscribers(storedEvent);

        return sequenceId;
    }

    public async Task<List<T>> GetEventsAsync<T>(DateTime? since = null, DateTime? until = null) where T : class
    {
        var eventType = typeof(T).Name;
        var sinceTimestamp = since?.ToUniversalTime().Subtract(DateTime.UnixEpoch).TotalMilliseconds ?? 0;
        var untilTimestamp = until?.ToUniversalTime().Subtract(DateTime.UnixEpoch).TotalMilliseconds ?? long.MaxValue;

        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT payload FROM events
            WHERE event_type = @type AND timestamp >= @since AND timestamp <= @until
            ORDER BY sequence_id ASC
        ";
        cmd.Parameters.AddWithValue("@type", eventType);
        cmd.Parameters.AddWithValue("@since", sinceTimestamp);
        cmd.Parameters.AddWithValue("@until", untilTimestamp);

        var events = new List<T>();
        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var payload = reader.GetString(0);
            var evt = JsonSerializer.Deserialize<T>(payload);
            if (evt != null) events.Add(evt);
        }

        _logger.LogDebug("Retrieved {Count} events of type {EventType}", events.Count, eventType);
        return events;
    }

    public async Task<List<StoredEvent>> GetAggregateEventsAsync(string aggregateId, DateTime? since = null)
    {
        var sinceTimestamp = since?.ToUniversalTime().Subtract(DateTime.UnixEpoch).TotalMilliseconds ?? 0;

        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT sequence_id, event_id, event_type, aggregate_id, correlation_id, timestamp, payload, metadata
            FROM events
            WHERE aggregate_id = @aggregateId AND timestamp >= @since
            ORDER BY sequence_id ASC
        ";
        cmd.Parameters.AddWithValue("@aggregateId", aggregateId);
        cmd.Parameters.AddWithValue("@since", sinceTimestamp);

        return await ReadStoredEventsAsync(cmd);
    }

    public async Task<List<StoredEvent>> GetAllEventsAsync(DateTime? since = null, int? limit = null)
    {
        var sinceTimestamp = since?.ToUniversalTime().Subtract(DateTime.UnixEpoch).TotalMilliseconds ?? 0;

        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
            SELECT sequence_id, event_id, event_type, aggregate_id, correlation_id, timestamp, payload, metadata
            FROM events
            WHERE timestamp >= @since
            ORDER BY sequence_id ASC
            {(limit.HasValue ? "LIMIT @limit" : "")}
        ";
        cmd.Parameters.AddWithValue("@since", sinceTimestamp);
        if (limit.HasValue) cmd.Parameters.AddWithValue("@limit", limit.Value);

        return await ReadStoredEventsAsync(cmd);
    }

    private async Task<List<StoredEvent>> ReadStoredEventsAsync(SqliteCommand cmd)
    {
        var events = new List<StoredEvent>();
        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            events.Add(new StoredEvent
            {
                SequenceId = reader.GetInt64(0),
                EventId = reader.GetString(1),
                EventType = reader.GetString(2),
                AggregateId = reader.IsDBNull(3) ? null : reader.GetString(3),
                CorrelationId = reader.IsDBNull(4) ? null : reader.GetString(4),
                Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(5)).UtcDateTime,
                Payload = reader.GetString(6),
                Metadata = reader.IsDBNull(7) ? null : reader.GetString(7)
            });
        }

        return events;
    }

    public async Task<long> GetEventCountAsync()
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM events";

        return (long)(await cmd.ExecuteScalarAsync() ?? 0L);
    }

    public IDisposable Subscribe(Action<StoredEvent> callback)
    {
        lock (_subscriberLock)
        {
            _subscribers.Add(callback);
        }

        _logger.LogDebug("New subscriber added (total: {Count})", _subscribers.Count);

        return new Subscription(() =>
        {
            lock (_subscriberLock)
            {
                _subscribers.Remove(callback);
            }
            _logger.LogDebug("Subscriber removed (total: {Count})", _subscribers.Count);
        });
    }

    private void NotifySubscribers(StoredEvent @event)
    {
        List<Action<StoredEvent>> subscribersCopy;

        lock (_subscriberLock)
        {
            subscribersCopy = new List<Action<StoredEvent>>(_subscribers);
        }

        foreach (var callback in subscribersCopy)
        {
            try
            {
                callback(@event);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Subscriber callback failed for event {EventType}", @event.EventType);
            }
        }
    }

    public void Dispose()
    {
        _logger.LogInformation("Disposing event store");
        SqliteConnection.ClearAllPools(); // Close all connections
    }

    private class Subscription : IDisposable
    {
        private readonly Action _unsubscribe;
        private bool _disposed;

        public Subscription(Action unsubscribe)
        {
            _unsubscribe = unsubscribe;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _unsubscribe();
                _disposed = true;
            }
        }
    }
}
