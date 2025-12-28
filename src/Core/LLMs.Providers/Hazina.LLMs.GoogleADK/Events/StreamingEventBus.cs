using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace Hazina.LLMs.GoogleADK.Events;

/// <summary>
/// Enhanced event bus with streaming and bidirectional support
/// </summary>
public class StreamingEventBus : EventBus
{
    private readonly ConcurrentDictionary<string, Channel<AgentEvent>> _eventStreams = new();
    private readonly ConcurrentDictionary<string, EventSubscription> _subscriptions = new();
    private readonly ILogger? _logger;

    public StreamingEventBus(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Create a streaming subscription for events
    /// </summary>
    public EventSubscription CreateStream<TEvent>(
        string subscriptionId,
        Func<TEvent, bool>? filter = null,
        int bufferSize = 100) where TEvent : AgentEvent
    {
        var channel = Channel.CreateBounded<AgentEvent>(new BoundedChannelOptions(bufferSize)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });

        _eventStreams[subscriptionId] = channel;

        var subscription = new EventSubscription
        {
            SubscriptionId = subscriptionId,
            EventType = typeof(TEvent),
            Filter = filter != null ? (e => filter((TEvent)e)) : null,
            CreatedAt = DateTime.UtcNow
        };

        _subscriptions[subscriptionId] = subscription;

        // Subscribe to events with filter
        Subscribe<TEvent>(evt =>
        {
            if (filter == null || filter(evt))
            {
                channel.Writer.TryWrite(evt);
            }
        });

        _logger?.LogInformation("Created event stream subscription: {SubscriptionId}", subscriptionId);
        return subscription;
    }

    /// <summary>
    /// Get event stream for a subscription
    /// </summary>
    public IAsyncEnumerable<AgentEvent> GetEventStream(string subscriptionId, CancellationToken cancellationToken = default)
    {
        if (!_eventStreams.TryGetValue(subscriptionId, out var channel))
        {
            throw new InvalidOperationException($"Subscription {subscriptionId} not found");
        }

        return ReadEventsAsync(channel.Reader, cancellationToken);
    }

    /// <summary>
    /// Read events from channel
    /// </summary>
    private async IAsyncEnumerable<AgentEvent> ReadEventsAsync(
        ChannelReader<AgentEvent> reader,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var evt in reader.ReadAllAsync(cancellationToken))
        {
            yield return evt;
        }
    }

    /// <summary>
    /// Unsubscribe and close stream
    /// </summary>
    public void CloseStream(string subscriptionId)
    {
        if (_eventStreams.TryRemove(subscriptionId, out var channel))
        {
            channel.Writer.Complete();
            _subscriptions.TryRemove(subscriptionId, out _);
            _logger?.LogInformation("Closed event stream: {SubscriptionId}", subscriptionId);
        }
    }

    /// <summary>
    /// Get all active subscriptions
    /// </summary>
    public List<EventSubscription> GetActiveSubscriptions()
    {
        return _subscriptions.Values.ToList();
    }

    /// <summary>
    /// Publish event to all streams and subscribers
    /// </summary>
    public new void Publish<TEvent>(TEvent evt) where TEvent : AgentEvent
    {
        // Publish to regular subscribers
        base.Publish(evt);

        // Publish to streaming subscriptions
        foreach (var kvp in _eventStreams)
        {
            var subscriptionId = kvp.Key;
            var channel = kvp.Value;

            if (_subscriptions.TryGetValue(subscriptionId, out var subscription))
            {
                // Check if event type matches
                if (subscription.EventType.IsAssignableFrom(typeof(TEvent)))
                {
                    // Apply filter if present
                    if (subscription.Filter == null || subscription.Filter(evt))
                    {
                        channel.Writer.TryWrite(evt);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Clear all streams
    /// </summary>
    public new void Clear()
    {
        base.Clear();

        foreach (var channel in _eventStreams.Values)
        {
            channel.Writer.Complete();
        }

        _eventStreams.Clear();
        _subscriptions.Clear();
    }
}

/// <summary>
/// Event subscription metadata
/// </summary>
public class EventSubscription
{
    public string SubscriptionId { get; set; } = string.Empty;
    public Type EventType { get; set; } = typeof(AgentEvent);
    public Func<AgentEvent, bool>? Filter { get; set; }
    public DateTime CreatedAt { get; set; }
    public int EventCount { get; set; }
}

/// <summary>
/// Event filter builder
/// </summary>
public class EventFilterBuilder<TEvent> where TEvent : AgentEvent
{
    private readonly List<Func<TEvent, bool>> _filters = new();

    public EventFilterBuilder<TEvent> WithEventType(string eventType)
    {
        _filters.Add(e => e.EventType == eventType);
        return this;
    }

    public EventFilterBuilder<TEvent> WithAgentId(string agentId)
    {
        _filters.Add(e => e.AgentId == agentId);
        return this;
    }

    public EventFilterBuilder<TEvent> WithSessionId(string sessionId)
    {
        _filters.Add(e => e.SessionId == sessionId);
        return this;
    }

    public EventFilterBuilder<TEvent> WithTimestamp(DateTime start, DateTime end)
    {
        _filters.Add(e => e.Timestamp >= start && e.Timestamp <= end);
        return this;
    }

    public EventFilterBuilder<TEvent> WithCustomFilter(Func<TEvent, bool> filter)
    {
        _filters.Add(filter);
        return this;
    }

    public Func<TEvent, bool> Build()
    {
        return evt => _filters.All(f => f(evt));
    }
}
