using System.Net.Http.Json;
using System.Text.Json;
using Hazina.LLMs.GoogleADK.Tools.Mcp.Models;
using Microsoft.Extensions.Logging;

namespace Hazina.LLMs.GoogleADK.Tools.Mcp.Transport;

/// <summary>
/// MCP transport using HTTP for remote server communication
/// </summary>
public class HttpMcpTransport : IMcpTransport
{
    private readonly HttpClient _httpClient;
    private readonly string _serverUrl;
    private readonly ILogger? _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private bool _isConnected;

    public event EventHandler<McpMessage>? MessageReceived;
    public bool IsConnected => _isConnected;

    public HttpMcpTransport(
        string serverUrl,
        HttpClient? httpClient = null,
        ILogger? logger = null)
    {
        _serverUrl = serverUrl.TrimEnd('/');
        _httpClient = httpClient ?? new HttpClient();
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Test connection with a ping/health check
            var response = await _httpClient.GetAsync($"{_serverUrl}/health", cancellationToken);
            _isConnected = response.IsSuccessStatusCode;

            if (_isConnected)
            {
                _logger?.LogInformation("Connected to MCP server at {Url}", _serverUrl);
            }
            else
            {
                _logger?.LogWarning("Failed to connect to MCP server at {Url}: {Status}", _serverUrl, response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error connecting to MCP server at {Url}", _serverUrl);
            _isConnected = false;
            throw;
        }
    }

    public async Task<McpResponse> SendRequestAsync(McpRequest request, CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("Transport is not connected");
        }

        try
        {
            _logger?.LogDebug("Sending MCP request: {Method}", request.Method);

            var response = await _httpClient.PostAsJsonAsync(
                $"{_serverUrl}/rpc",
                request,
                _jsonOptions,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var mcpResponse = await response.Content.ReadFromJsonAsync<McpResponse>(_jsonOptions, cancellationToken);

            if (mcpResponse == null)
            {
                throw new InvalidOperationException("Received null response from server");
            }

            MessageReceived?.Invoke(this, mcpResponse);
            return mcpResponse;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error sending MCP request");
            throw;
        }
    }

    public async Task SendNotificationAsync(McpNotification notification, CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("Transport is not connected");
        }

        try
        {
            _logger?.LogDebug("Sending MCP notification: {Method}", notification.Method);

            await _httpClient.PostAsJsonAsync(
                $"{_serverUrl}/notify",
                notification,
                _jsonOptions,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error sending MCP notification");
            throw;
        }
    }

    public Task CloseAsync(CancellationToken cancellationToken = default)
    {
        _isConnected = false;
        _logger?.LogInformation("Disconnected from MCP server");
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await CloseAsync();
        _httpClient.Dispose();
    }
}
