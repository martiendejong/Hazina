using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Hazina.LLMs.GoogleADK.Events;

/// <summary>
/// Server-Sent Events (SSE) stream for real-time event delivery
/// </summary>
public class ServerSentEventStream : IAsyncDisposable
{
    private readonly StreamingEventBus _eventBus;
    private readonly string _subscriptionId;
    private readonly ILogger? _logger;
    private readonly CancellationTokenSource _cts = new();

    public ServerSentEventStream(
        StreamingEventBus eventBus,
        string subscriptionId,
        ILogger? logger = null)
    {
        _eventBus = eventBus;
        _subscriptionId = subscriptionId;
        _logger = logger;
    }

    /// <summary>
    /// Stream events as Server-Sent Events format
    /// </summary>
    public async IAsyncEnumerable<string> StreamEventsAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cts.Token);

        await foreach (var evt in _eventBus.GetEventStream(_subscriptionId, linkedCts.Token))
        {
            yield return FormatAsSSE(evt);
        }
    }

    /// <summary>
    /// Stream events to a TextWriter (e.g., HTTP response)
    /// </summary>
    public async Task StreamToWriterAsync(
        TextWriter writer,
        CancellationToken cancellationToken = default)
    {
        await foreach (var sseMessage in StreamEventsAsync(cancellationToken))
        {
            await writer.WriteAsync(sseMessage);
            await writer.FlushAsync();
        }
    }

    /// <summary>
    /// Format event as SSE message
    /// </summary>
    private string FormatAsSSE(AgentEvent evt)
    {
        var builder = new StringBuilder();

        // Event ID (timestamp-based)
        builder.AppendLine($"id: {evt.Timestamp.Ticks}");

        // Event type
        builder.AppendLine($"event: {evt.EventType}");

        // Event data (JSON)
        var json = JsonSerializer.Serialize(evt, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        builder.AppendLine($"data: {json}");

        // Empty line to indicate end of event
        builder.AppendLine();

        return builder.ToString();
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _eventBus.CloseStream(_subscriptionId);
        _cts.Dispose();

        await Task.CompletedTask;
    }
}
