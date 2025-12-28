using System.Collections.Concurrent;
using System.Threading.Channels;
using Hazina.LLMs.GoogleADK.A2A.Models;
using Microsoft.Extensions.Logging;

namespace Hazina.LLMs.GoogleADK.A2A.Transport;

/// <summary>
/// In-process A2A transport for local agent communication
/// </summary>
public class InProcessA2ATransport : IA2ATransport, IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, Func<A2ARequest, Task<A2AResponse>>> _requestHandlers = new();
    private readonly ConcurrentDictionary<string, Func<A2ANotification, Task>> _notificationHandlers = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<A2AResponse>> _pendingRequests = new();
    private readonly Channel<A2AMessage> _messageChannel;
    private readonly ILogger? _logger;
    private CancellationTokenSource? _cts;
    private Task? _processingTask;

    public InProcessA2ATransport(ILogger? logger = null)
    {
        _logger = logger;
        _messageChannel = Channel.CreateUnbounded<A2AMessage>();
    }

    public async Task<A2AResponse> SendRequestAsync(
        A2ARequest request,
        CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource<A2AResponse>();
        _pendingRequests[request.MessageId] = tcs;

        try
        {
            await _messageChannel.Writer.WriteAsync(request, cancellationToken);

            // Wait for response with timeout
            var timeout = request.Timeout ?? TimeSpan.FromSeconds(30);
            using var timeoutCts = new CancellationTokenSource(timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            var responseTask = tcs.Task;
            var completedTask = await Task.WhenAny(responseTask, Task.Delay(timeout, linkedCts.Token));

            if (completedTask == responseTask)
            {
                return await responseTask;
            }
            else
            {
                throw new TimeoutException($"Request {request.MessageId} timed out after {timeout}");
            }
        }
        finally
        {
            _pendingRequests.TryRemove(request.MessageId, out _);
        }
    }

    public async Task SendNotificationAsync(
        A2ANotification notification,
        CancellationToken cancellationToken = default)
    {
        await _messageChannel.Writer.WriteAsync(notification, cancellationToken);
    }

    public async Task<A2AHandshake?> SendHandshakeAsync(
        A2AHandshake handshake,
        CancellationToken cancellationToken = default)
    {
        await _messageChannel.Writer.WriteAsync(handshake, cancellationToken);
        // In a real implementation, this would wait for handshake response
        return handshake;
    }

    public void RegisterRequestHandler(
        string requestType,
        Func<A2ARequest, Task<A2AResponse>> handler)
    {
        _requestHandlers[requestType] = handler;
        _logger?.LogDebug("Registered request handler for: {RequestType}", requestType);
    }

    public void RegisterNotificationHandler(
        string notificationType,
        Func<A2ANotification, Task> handler)
    {
        _notificationHandlers[notificationType] = handler;
        _logger?.LogDebug("Registered notification handler for: {NotificationType}", notificationType);
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        _cts = new CancellationTokenSource();
        _processingTask = ProcessMessagesAsync(_cts.Token);
        _logger?.LogInformation("A2A transport started");
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_cts != null)
        {
            _cts.Cancel();
            if (_processingTask != null)
            {
                await _processingTask;
            }
            _logger?.LogInformation("A2A transport stopped");
        }
    }

    private async Task ProcessMessagesAsync(CancellationToken cancellationToken)
    {
        await foreach (var message in _messageChannel.Reader.ReadAllAsync(cancellationToken))
        {
            try
            {
                await ProcessMessageAsync(message, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error processing A2A message: {MessageId}", message.MessageId);
            }
        }
    }

    private async Task ProcessMessageAsync(A2AMessage message, CancellationToken cancellationToken)
    {
        switch (message)
        {
            case A2ARequest request:
                await ProcessRequestAsync(request, cancellationToken);
                break;

            case A2AResponse response:
                ProcessResponse(response);
                break;

            case A2ANotification notification:
                await ProcessNotificationAsync(notification, cancellationToken);
                break;

            case A2AHandshake handshake:
                _logger?.LogInformation("Received handshake from agent: {AgentId}", handshake.AgentId);
                break;
        }
    }

    private async Task ProcessRequestAsync(A2ARequest request, CancellationToken cancellationToken)
    {
        if (_requestHandlers.TryGetValue(request.RequestType, out var handler))
        {
            try
            {
                var response = await handler(request);
                response.RequestId = request.MessageId;

                // If original sender is waiting for response, deliver it
                if (request.RequiresResponse)
                {
                    await _messageChannel.Writer.WriteAsync(response, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error handling request: {RequestType}", request.RequestType);

                var errorResponse = new A2AResponse
                {
                    RequestId = request.MessageId,
                    SourceAgentId = request.TargetAgentId,
                    TargetAgentId = request.SourceAgentId,
                    Success = false,
                    Error = ex.Message
                };

                if (request.RequiresResponse)
                {
                    await _messageChannel.Writer.WriteAsync(errorResponse, cancellationToken);
                }
            }
        }
        else
        {
            _logger?.LogWarning("No handler registered for request type: {RequestType}", request.RequestType);
        }
    }

    private void ProcessResponse(A2AResponse response)
    {
        if (_pendingRequests.TryGetValue(response.RequestId, out var tcs))
        {
            tcs.TrySetResult(response);
        }
    }

    private async Task ProcessNotificationAsync(A2ANotification notification, CancellationToken cancellationToken)
    {
        if (_notificationHandlers.TryGetValue(notification.NotificationType, out var handler))
        {
            try
            {
                await handler(notification);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error handling notification: {NotificationType}", notification.NotificationType);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _cts?.Dispose();
        _pendingRequests.Clear();
        _requestHandlers.Clear();
        _notificationHandlers.Clear();
    }
}
