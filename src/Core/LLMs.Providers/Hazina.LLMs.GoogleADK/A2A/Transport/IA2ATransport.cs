using Hazina.LLMs.GoogleADK.A2A.Models;

namespace Hazina.LLMs.GoogleADK.A2A.Transport;

/// <summary>
/// Transport layer for A2A message delivery
/// </summary>
public interface IA2ATransport
{
    /// <summary>
    /// Send a request and wait for response
    /// </summary>
    Task<A2AResponse> SendRequestAsync(
        A2ARequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Send a notification (fire and forget)
    /// </summary>
    Task SendNotificationAsync(
        A2ANotification notification,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Send a handshake
    /// </summary>
    Task<A2AHandshake?> SendHandshakeAsync(
        A2AHandshake handshake,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Register a handler for incoming requests
    /// </summary>
    void RegisterRequestHandler(
        string requestType,
        Func<A2ARequest, Task<A2AResponse>> handler);

    /// <summary>
    /// Register a handler for incoming notifications
    /// </summary>
    void RegisterNotificationHandler(
        string notificationType,
        Func<A2ANotification, Task> handler);

    /// <summary>
    /// Start listening for incoming messages
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stop listening
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);
}
