using Hazina.LLMs.GoogleADK.Tools.Mcp.Models;

namespace Hazina.LLMs.GoogleADK.Tools.Mcp.Transport;

/// <summary>
/// Transport layer for MCP communication
/// </summary>
public interface IMcpTransport : IAsyncDisposable
{
    /// <summary>
    /// Send a request and wait for a response
    /// </summary>
    Task<McpResponse> SendRequestAsync(McpRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Send a notification (no response expected)
    /// </summary>
    Task SendNotificationAsync(McpNotification notification, CancellationToken cancellationToken = default);

    /// <summary>
    /// Event raised when a message is received
    /// </summary>
    event EventHandler<McpMessage>? MessageReceived;

    /// <summary>
    /// Connect to the transport
    /// </summary>
    Task ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Close the transport
    /// </summary>
    Task CloseAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether the transport is connected
    /// </summary>
    bool IsConnected { get; }
}
