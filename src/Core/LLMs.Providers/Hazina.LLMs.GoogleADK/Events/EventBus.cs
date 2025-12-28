using Microsoft.Extensions.Logging;

namespace Hazina.LLMs.GoogleADK.Events;

/// <summary>
/// Event bus for publishing and subscribing to agent events.
/// Implements pub/sub pattern for agent event handling.
/// </summary>
public class EventBus
{
    private readonly Dictionary<Type, List<Delegate>> _handlers = new();
    private readonly ILogger<EventBus>? _logger;
    private readonly object _lock = new();

    public EventBus(ILogger<EventBus>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Subscribe to a specific event type
    /// </summary>
    public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : AgentEvent
    {
        lock (_lock)
        {
            var eventType = typeof(TEvent);
            if (!_handlers.ContainsKey(eventType))
            {
                _handlers[eventType] = new List<Delegate>();
            }
            _handlers[eventType].Add(handler);
        }
    }

    /// <summary>
    /// Unsubscribe from a specific event type
    /// </summary>
    public void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : AgentEvent
    {
        lock (_lock)
        {
            var eventType = typeof(TEvent);
            if (_handlers.ContainsKey(eventType))
            {
                _handlers[eventType].Remove(handler);
            }
        }
    }

    /// <summary>
    /// Publish an event to all subscribers
    /// </summary>
    public void Publish<TEvent>(TEvent evt) where TEvent : AgentEvent
    {
        List<Delegate> handlers;
        lock (_lock)
        {
            var eventType = typeof(TEvent);
            if (!_handlers.ContainsKey(eventType))
            {
                return;
            }
            handlers = new List<Delegate>(_handlers[eventType]);
        }

        foreach (var handler in handlers)
        {
            try
            {
                ((Action<TEvent>)handler)(evt);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error executing event handler for {EventType}", evt.EventType);
            }
        }
    }

    /// <summary>
    /// Publish an event asynchronously
    /// </summary>
    public async Task PublishAsync<TEvent>(TEvent evt) where TEvent : AgentEvent
    {
        List<Delegate> handlers;
        lock (_lock)
        {
            var eventType = typeof(TEvent);
            if (!_handlers.ContainsKey(eventType))
            {
                return;
            }
            handlers = new List<Delegate>(_handlers[eventType]);
        }

        var tasks = handlers.Select(async handler =>
        {
            try
            {
                await Task.Run(() => ((Action<TEvent>)handler)(evt));
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error executing async event handler for {EventType}", evt.EventType);
            }
        });

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Clear all event handlers
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _handlers.Clear();
        }
    }

    /// <summary>
    /// Get count of subscribers for a specific event type
    /// </summary>
    public int GetSubscriberCount<TEvent>() where TEvent : AgentEvent
    {
        lock (_lock)
        {
            var eventType = typeof(TEvent);
            return _handlers.ContainsKey(eventType) ? _handlers[eventType].Count : 0;
        }
    }
}
