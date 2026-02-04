using System.Collections.Concurrent;
using System.Reactive.Subjects;
using System.Reactive.Linq;

namespace Hazina.App.HazinaCoder.Core.Events;

/// <summary>
/// Event bus for pub/sub messaging throughout HazinaCoder
/// Foundation for reactive, event-driven architecture
/// </summary>
public class EventBus : IEventBus, IDisposable
{
    private readonly ConcurrentDictionary<Type, object> _subjects = new();
    private readonly List<IDisposable> _subscriptions = new();
    private readonly object _lock = new();

    /// <summary>
    /// Publish an event to all subscribers
    /// </summary>
    public void Publish<T>(T @event) where T : IEvent
    {
        var subject = GetOrCreateSubject<T>();
        subject.OnNext(@event);
    }

    /// <summary>
    /// Subscribe to events of type T
    /// </summary>
    public IDisposable Subscribe<T>(Action<T> handler) where T : IEvent
    {
        var subject = GetOrCreateSubject<T>();
        var subscription = subject.Subscribe(handler);

        lock (_lock)
        {
            _subscriptions.Add(subscription);
        }

        return subscription;
    }

    /// <summary>
    /// Subscribe to events with async handler
    /// </summary>
    public IDisposable SubscribeAsync<T>(Func<T, Task> handler) where T : IEvent
    {
        return Subscribe<T>(@event =>
        {
            // Fire and forget async handler
            Task.Run(async () =>
            {
                try
                {
                    await handler(@event);
                }
                catch (Exception ex)
                {
                    // Log error but don't crash the event bus
                    Console.WriteLine($"Error in async event handler: {ex.Message}");
                }
            });
        });
    }

    /// <summary>
    /// Subscribe with filtering
    /// </summary>
    public IDisposable SubscribeWhere<T>(Func<T, bool> predicate, Action<T> handler) where T : IEvent
    {
        var subject = GetOrCreateSubject<T>();
        var subscription = subject
            .Where(predicate)
            .Subscribe(handler);

        lock (_lock)
        {
            _subscriptions.Add(subscription);
        }

        return subscription;
    }

    /// <summary>
    /// Get observable stream for event type
    /// </summary>
    public IObservable<T> GetObservable<T>() where T : IEvent
    {
        return GetOrCreateSubject<T>().AsObservable();
    }

    /// <summary>
    /// Clear all subscriptions
    /// </summary>
    public void ClearSubscriptions()
    {
        lock (_lock)
        {
            foreach (var subscription in _subscriptions)
            {
                subscription.Dispose();
            }
            _subscriptions.Clear();
        }
    }

    /// <summary>
    /// Get or create subject for event type
    /// </summary>
    private Subject<T> GetOrCreateSubject<T>() where T : IEvent
    {
        return (Subject<T>)_subjects.GetOrAdd(typeof(T), _ => new Subject<T>());
    }

    public void Dispose()
    {
        ClearSubscriptions();

        foreach (var subject in _subjects.Values)
        {
            if (subject is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        _subjects.Clear();
    }
}

/// <summary>
/// Event bus interface
/// </summary>
public interface IEventBus
{
    void Publish<T>(T @event) where T : IEvent;
    IDisposable Subscribe<T>(Action<T> handler) where T : IEvent;
    IDisposable SubscribeAsync<T>(Func<T, Task> handler) where T : IEvent;
    IDisposable SubscribeWhere<T>(Func<T, bool> predicate, Action<T> handler) where T : IEvent;
    IObservable<T> GetObservable<T>() where T : IEvent;
}

/// <summary>
/// Base interface for all events
/// </summary>
public interface IEvent
{
    DateTime Timestamp { get; }
    Guid EventId { get; }
}

/// <summary>
/// Base event class
/// </summary>
public abstract record BaseEvent : IEvent
{
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public Guid EventId { get; init; } = Guid.NewGuid();
}

// ===== Core Events =====

/// <summary>
/// Tool execution started
/// </summary>
public record ToolExecutionStartedEvent(string ToolName, Dictionary<string, object> Arguments) : BaseEvent;

/// <summary>
/// Tool execution completed
/// </summary>
public record ToolExecutionCompletedEvent(string ToolName, bool Success, string Result, TimeSpan Duration) : BaseEvent;

/// <summary>
/// Tool execution failed
/// </summary>
public record ToolExecutionFailedEvent(string ToolName, Exception Error) : BaseEvent;

/// <summary>
/// Agent iteration started
/// </summary>
public record AgentIterationStartedEvent(int Iteration, string Task) : BaseEvent;

/// <summary>
/// Agent iteration completed
/// </summary>
public record AgentIterationCompletedEvent(int Iteration, string Summary, int ActionsExecuted) : BaseEvent;

/// <summary>
/// File changed event
/// </summary>
public record FileChangedEvent(string FilePath, FileChangeType ChangeType) : BaseEvent;

public enum FileChangeType
{
    Created,
    Modified,
    Deleted,
    Renamed
}

/// <summary>
/// Context loaded event
/// </summary>
public record ContextLoadedEvent(int FileCount, int TokenCount, TimeSpan LoadTime) : BaseEvent;

/// <summary>
/// LLM request started
/// </summary>
public record LLMRequestStartedEvent(string Provider, string Model, int InputTokens) : BaseEvent;

/// <summary>
/// LLM request completed
/// </summary>
public record LLMRequestCompletedEvent(string Provider, string Model, int InputTokens, int OutputTokens, TimeSpan Duration, double Cost) : BaseEvent;

/// <summary>
/// Secret detected event
/// </summary>
public record SecretDetectedEvent(string FilePath, string SecretType, string Severity) : BaseEvent;

/// <summary>
/// Snapshot saved event
/// </summary>
public record SnapshotSavedEvent(string SnapshotId, string Name, long SizeBytes) : BaseEvent;

/// <summary>
/// Snapshot restored event
/// </summary>
public record SnapshotRestoredEvent(string SnapshotId, string Name) : BaseEvent;

/// <summary>
/// Cache hit event
/// </summary>
public record CacheHitEvent(string CacheType, string Key) : BaseEvent;

/// <summary>
/// Cache miss event
/// </summary>
public record CacheMissEvent(string CacheType, string Key) : BaseEvent;

/// <summary>
/// Vision analysis started event
/// </summary>
public record VisionAnalysisStartedEvent(string ImagePath, string Query) : BaseEvent;

/// <summary>
/// Vision analysis completed event
/// </summary>
public record VisionAnalysisCompletedEvent(string ImagePath, string Query, bool Success, TimeSpan Duration) : BaseEvent;

/// <summary>
/// Vision analysis failed event
/// </summary>
public record VisionAnalysisFailedEvent(string ImagePath, string Query, Exception Error) : BaseEvent;

/// <summary>
/// Vision analysis cached event
/// </summary>
public record VisionAnalysisCachedEvent(string ImagePath, string Query) : BaseEvent;

/// <summary>
/// Provider selected event
/// </summary>
public record ProviderSelectedEvent(string ProviderName, string Model, string Strategy) : BaseEvent;

/// <summary>
/// Provider success event
/// </summary>
public record ProviderSuccessEvent(string ProviderName, string Model, TimeSpan Duration, double Cost) : BaseEvent;

/// <summary>
/// Provider failure event
/// </summary>
public record ProviderFailureEvent(string ProviderName, string Model, Exception Error) : BaseEvent;

/// <summary>
/// Test generation started event
/// </summary>
public record TestGenerationStartedEvent(string ClassName) : BaseEvent;

/// <summary>
/// Test generation completed event
/// </summary>
public record TestGenerationCompletedEvent(string ClassName, int TestCount, TimeSpan Duration) : BaseEvent;

/// <summary>
/// Test generation failed event
/// </summary>
public record TestGenerationFailedEvent(string ClassName, Exception Error) : BaseEvent;
