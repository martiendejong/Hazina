using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Hazina.AgenticOrchestration.Integration.EventBroker;

/// <summary>
/// Adapter for DataDrivenAI EventBroker integration
/// Converts ClickUp/GitHub events into EventBroker messages for distributed processing
/// </summary>
public class EventBrokerAdapter
{
    private readonly IEventBroker _eventBroker;
    private readonly Dictionary<string, Func<object, Task>> _handlers = new();

    public EventBrokerAdapter(IEventBroker eventBroker)
    {
        _eventBroker = eventBroker;
        RegisterDefaultHandlers();
    }

    /// <summary>
    /// Publish event to EventBroker
    /// Events are distributed to all subscribed agents
    /// </summary>
    public async Task PublishAsync<T>(string eventType, T eventData, CancellationToken cancellationToken = default)
    {
        var envelope = new EventEnvelope
        {
            EventId = Guid.NewGuid().ToString(),
            EventType = eventType,
            Timestamp = DateTime.UtcNow,
            Source = "Jengo.AutonomousWorker",
            Data = eventData
        };

        await _eventBroker.PublishAsync(eventType, envelope, cancellationToken);

        Console.WriteLine($"[EventBroker] Published {eventType} - {envelope.EventId}");
    }

    /// <summary>
    /// Subscribe to event type with handler
    /// Handler is invoked whenever event of this type is received
    /// </summary>
    public async Task SubscribeAsync<T>(string eventType, Func<T, Task> handler, CancellationToken cancellationToken = default)
    {
        _handlers[eventType] = async (data) =>
        {
            if (data is EventEnvelope envelope && envelope.Data is T typedData)
            {
                await handler(typedData);
            }
        };

        await _eventBroker.SubscribeAsync(eventType, _handlers[eventType], cancellationToken);

        Console.WriteLine($"[EventBroker] Subscribed to {eventType}");
    }

    /// <summary>
    /// Register default handlers for common events
    /// </summary>
    private void RegisterDefaultHandlers()
    {
        // Default handlers are registered by consumers
    }

    /// <summary>
    /// Start event processing loop
    /// Listens for events and dispatches to handlers
    /// </summary>
    public async Task StartProcessingAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("[EventBroker] Starting event processing...");

        await _eventBroker.StartAsync(cancellationToken);

        Console.WriteLine("[EventBroker] Event processing started");
    }

    /// <summary>
    /// Stop event processing
    /// </summary>
    public async Task StopProcessingAsync()
    {
        Console.WriteLine("[EventBroker] Stopping event processing...");

        await _eventBroker.StopAsync();

        Console.WriteLine("[EventBroker] Event processing stopped");
    }
}

/// <summary>
/// Event envelope with metadata
/// Wraps all events published through EventBroker
/// </summary>
public class EventEnvelope
{
    public string EventId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Source { get; set; } = string.Empty;
    public object? Data { get; set; }
}

/// <summary>
/// EventBroker interface (implemented by DataDrivenAI)
/// This is the contract for the underlying event system
/// </summary>
public interface IEventBroker
{
    Task PublishAsync<T>(string eventType, T data, CancellationToken cancellationToken = default);
    Task SubscribeAsync(string eventType, Func<object, Task> handler, CancellationToken cancellationToken = default);
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync();
}
