using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Hazina.LLMs.GoogleADK.Events;

/// <summary>
/// Event replay system for debugging and recovery
/// </summary>
public class EventReplay
{
    private readonly ConcurrentQueue<AgentEvent> _eventHistory = new();
    private readonly int _maxHistorySize;
    private readonly ILogger? _logger;

    public EventReplay(int maxHistorySize = 1000, ILogger? logger = null)
    {
        _maxHistorySize = maxHistorySize;
        _logger = logger;
    }

    /// <summary>
    /// Record an event
    /// </summary>
    public void RecordEvent(AgentEvent evt)
    {
        _eventHistory.Enqueue(evt);

        // Trim if exceeds max size
        while (_eventHistory.Count > _maxHistorySize)
        {
            _eventHistory.TryDequeue(out _);
        }
    }

    /// <summary>
    /// Get event history
    /// </summary>
    public List<AgentEvent> GetHistory(
        DateTime? startTime = null,
        DateTime? endTime = null,
        string? eventType = null,
        string? agentId = null)
    {
        var query = _eventHistory.AsEnumerable();

        if (startTime.HasValue)
        {
            query = query.Where(e => e.Timestamp >= startTime.Value);
        }

        if (endTime.HasValue)
        {
            query = query.Where(e => e.Timestamp <= endTime.Value);
        }

        if (!string.IsNullOrEmpty(eventType))
        {
            query = query.Where(e => e.EventType == eventType);
        }

        if (!string.IsNullOrEmpty(agentId))
        {
            query = query.Where(e => e.AgentId == agentId);
        }

        return query.OrderBy(e => e.Timestamp).ToList();
    }

    /// <summary>
    /// Replay events to an event bus
    /// </summary>
    public async Task ReplayEventsAsync(
        EventBus targetBus,
        DateTime? startTime = null,
        DateTime? endTime = null,
        TimeSpan? replaySpeed = null,
        CancellationToken cancellationToken = default)
    {
        var events = GetHistory(startTime, endTime);

        _logger?.LogInformation("Replaying {Count} events", events.Count);

        DateTime? lastTimestamp = null;

        foreach (var evt in events)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Simulate timing if replay speed is specified
            if (replaySpeed.HasValue && lastTimestamp.HasValue)
            {
                var actualDelay = evt.Timestamp - lastTimestamp.Value;
                var replayDelay = TimeSpan.FromTicks((long)(actualDelay.Ticks * replaySpeed.Value.TotalSeconds));

                if (replayDelay > TimeSpan.Zero)
                {
                    await Task.Delay(replayDelay, cancellationToken);
                }
            }

            targetBus.Publish(evt);
            lastTimestamp = evt.Timestamp;
        }

        _logger?.LogInformation("Event replay completed");
    }

    /// <summary>
    /// Export events to JSON
    /// </summary>
    public string ExportToJson(
        DateTime? startTime = null,
        DateTime? endTime = null)
    {
        var events = GetHistory(startTime, endTime);
        return System.Text.Json.JsonSerializer.Serialize(events, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        });
    }

    /// <summary>
    /// Clear event history
    /// </summary>
    public void Clear()
    {
        _eventHistory.Clear();
        _logger?.LogInformation("Event history cleared");
    }

    /// <summary>
    /// Get statistics
    /// </summary>
    public EventReplayStatistics GetStatistics()
    {
        var events = _eventHistory.ToList();

        return new EventReplayStatistics
        {
            TotalEvents = events.Count,
            EventsByType = events.GroupBy(e => e.EventType).ToDictionary(g => g.Key, g => g.Count()),
            OldestEvent = events.OrderBy(e => e.Timestamp).FirstOrDefault()?.Timestamp,
            NewestEvent = events.OrderByDescending(e => e.Timestamp).FirstOrDefault()?.Timestamp
        };
    }
}

/// <summary>
/// Event replay statistics
/// </summary>
public class EventReplayStatistics
{
    public int TotalEvents { get; set; }
    public Dictionary<string, int> EventsByType { get; set; } = new();
    public DateTime? OldestEvent { get; set; }
    public DateTime? NewestEvent { get; set; }
}
