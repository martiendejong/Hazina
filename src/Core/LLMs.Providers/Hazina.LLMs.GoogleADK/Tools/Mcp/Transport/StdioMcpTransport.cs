using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Hazina.LLMs.GoogleADK.Tools.Mcp.Models;
using Microsoft.Extensions.Logging;

namespace Hazina.LLMs.GoogleADK.Tools.Mcp.Transport;

/// <summary>
/// MCP transport using stdio (standard input/output) for process communication
/// </summary>
public class StdioMcpTransport : IMcpTransport
{
    private readonly string _serverCommand;
    private readonly string[] _serverArgs;
    private readonly ILogger? _logger;
    private Process? _process;
    private readonly Dictionary<string, TaskCompletionSource<McpResponse>> _pendingRequests = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions;

    public event EventHandler<McpMessage>? MessageReceived;
    public bool IsConnected => _process?.HasExited == false;

    public StdioMcpTransport(
        string serverCommand,
        string[] serverArgs,
        ILogger? logger = null)
    {
        _serverCommand = serverCommand;
        _serverArgs = serverArgs;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (IsConnected)
        {
            return;
        }

        _process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _serverCommand,
                Arguments = string.Join(" ", _serverArgs),
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        _process.Start();
        _logger?.LogInformation("MCP server process started: {Command} {Args}", _serverCommand, string.Join(" ", _serverArgs));

        // Start reading output
        _ = Task.Run(() => ReadOutputAsync(cancellationToken), cancellationToken);
        _ = Task.Run(() => ReadErrorAsync(cancellationToken), cancellationToken);

        await Task.CompletedTask;
    }

    public async Task<McpResponse> SendRequestAsync(McpRequest request, CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("Transport is not connected");
        }

        var tcs = new TaskCompletionSource<McpResponse>();
        _pendingRequests[request.Id] = tcs;

        try
        {
            await SendMessageAsync(request, cancellationToken);

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linkedCts.CancelAfter(TimeSpan.FromSeconds(30)); // 30 second timeout

            return await tcs.Task.WaitAsync(linkedCts.Token);
        }
        catch (OperationCanceledException)
        {
            _pendingRequests.Remove(request.Id);
            throw;
        }
    }

    public async Task SendNotificationAsync(McpNotification notification, CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("Transport is not connected");
        }

        await SendMessageAsync(notification, cancellationToken);
    }

    private async Task SendMessageAsync(object message, CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var json = JsonSerializer.Serialize(message, _jsonOptions);
            var bytes = Encoding.UTF8.GetBytes(json + "\n");

            await _process!.StandardInput.BaseStream.WriteAsync(bytes, cancellationToken);
            await _process.StandardInput.BaseStream.FlushAsync(cancellationToken);

            _logger?.LogDebug("Sent MCP message: {Json}", json);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task ReadOutputAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && _process != null && !_process.HasExited)
            {
                var line = await _process.StandardOutput.ReadLineAsync(cancellationToken);
                if (string.IsNullOrEmpty(line))
                {
                    continue;
                }

                _logger?.LogDebug("Received MCP message: {Line}", line);

                try
                {
                    var message = JsonSerializer.Deserialize<McpResponse>(line, _jsonOptions);
                    if (message != null)
                    {
                        HandleResponse(message);
                    }
                }
                catch (JsonException ex)
                {
                    _logger?.LogError(ex, "Failed to parse MCP message: {Line}", line);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error reading MCP output");
        }
    }

    private async Task ReadErrorAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && _process != null && !_process.HasExited)
            {
                var line = await _process.StandardError.ReadLineAsync(cancellationToken);
                if (!string.IsNullOrEmpty(line))
                {
                    _logger?.LogWarning("MCP server error: {Error}", line);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error reading MCP error stream");
        }
    }

    private void HandleResponse(McpResponse response)
    {
        if (_pendingRequests.TryGetValue(response.Id, out var tcs))
        {
            _pendingRequests.Remove(response.Id);
            tcs.SetResult(response);
        }

        MessageReceived?.Invoke(this, response);
    }

    public async Task CloseAsync(CancellationToken cancellationToken = default)
    {
        if (_process != null && !_process.HasExited)
        {
            _process.Kill();
            await _process.WaitForExitAsync(cancellationToken);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await CloseAsync();
        _process?.Dispose();
        _writeLock.Dispose();
    }
}
