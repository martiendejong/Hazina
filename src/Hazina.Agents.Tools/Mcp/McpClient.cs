using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hazina.Agents.Tools.Mcp;

/// <summary>
/// Client for connecting to MCP (Model Context Protocol) servers.
/// Supports stdio-based servers that communicate via JSON-RPC.
/// </summary>
public class McpClient : IDisposable
{
    private Process? _process;
    private readonly string _command;
    private readonly string[] _args;
    private readonly Dictionary<string, string>? _env;
    private int _requestId = 0;
    private readonly object _lock = new();
    private bool _initialized = false;

    public string ServerName { get; }
    public List<McpTool> Tools { get; private set; } = new();
    public bool IsConnected => _process != null && !_process.HasExited;

    public McpClient(string serverName, string command, string[]? args = null, Dictionary<string, string>? env = null)
    {
        ServerName = serverName;
        _command = command;
        _args = args ?? Array.Empty<string>();
        _env = env;
    }

    public async Task<bool> ConnectAsync(CancellationToken cancel = default)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = _command,
                Arguments = string.Join(" ", _args),
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            // Add environment variables
            if (_env != null)
            {
                foreach (var (key, value) in _env)
                {
                    startInfo.Environment[key] = value;
                }
            }

            _process = Process.Start(startInfo);
            if (_process == null)
            {
                return false;
            }

            // Initialize the connection
            var initResult = await SendRequestAsync<McpInitializeResult>("initialize", new
            {
                protocolVersion = "2024-11-05",
                capabilities = new { },
                clientInfo = new
                {
                    name = "HazinaCoder",
                    version = "1.0.0"
                }
            }, cancel);

            if (initResult != null)
            {
                _initialized = true;

                // Send initialized notification
                await SendNotificationAsync("notifications/initialized", new { }, cancel);

                // Get available tools
                await RefreshToolsAsync(cancel);
                return true;
            }

            return false;
        }
        catch
        {
            Disconnect();
            return false;
        }
    }

    public async Task RefreshToolsAsync(CancellationToken cancel = default)
    {
        if (!_initialized) return;

        try
        {
            var result = await SendRequestAsync<McpToolsListResult>("tools/list", new { }, cancel);
            if (result?.Tools != null)
            {
                Tools = result.Tools;
            }
        }
        catch
        {
            // Failed to get tools
        }
    }

    public async Task<string> CallToolAsync(string toolName, Dictionary<string, object>? arguments, CancellationToken cancel = default)
    {
        if (!_initialized)
            return "Error: MCP server not initialized";

        try
        {
            var result = await SendRequestAsync<McpToolCallResult>("tools/call", new
            {
                name = toolName,
                arguments = arguments ?? new Dictionary<string, object>()
            }, cancel);

            if (result?.Content != null && result.Content.Count > 0)
            {
                return string.Join("\n", result.Content.Select(c => c.Text ?? c.ToString()));
            }

            return result?.IsError == true ? "Error: Tool execution failed" : "Success (no output)";
        }
        catch (Exception ex)
        {
            return $"Error calling MCP tool: {ex.Message}";
        }
    }

    private async Task<T?> SendRequestAsync<T>(string method, object parameters, CancellationToken cancel) where T : class
    {
        if (_process == null || _process.HasExited)
            return null;

        int id;
        lock (_lock)
        {
            id = ++_requestId;
        }

        var request = new
        {
            jsonrpc = "2.0",
            id = id,
            method = method,
            @params = parameters
        };

        var json = JsonSerializer.Serialize(request);
        await _process.StandardInput.WriteLineAsync(json);
        await _process.StandardInput.FlushAsync();

        // Read response
        var responseLine = await ReadLineWithTimeoutAsync(_process.StandardOutput, TimeSpan.FromSeconds(30), cancel);
        if (string.IsNullOrEmpty(responseLine))
            return null;

        var response = JsonSerializer.Deserialize<McpResponse<T>>(responseLine);
        return response?.Result;
    }

    private async Task SendNotificationAsync(string method, object parameters, CancellationToken cancel)
    {
        if (_process == null || _process.HasExited)
            return;

        var notification = new
        {
            jsonrpc = "2.0",
            method = method,
            @params = parameters
        };

        var json = JsonSerializer.Serialize(notification);
        await _process.StandardInput.WriteLineAsync(json);
        await _process.StandardInput.FlushAsync();
    }

    private static async Task<string?> ReadLineWithTimeoutAsync(StreamReader reader, TimeSpan timeout, CancellationToken cancel)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        cts.CancelAfter(timeout);

        try
        {
            return await reader.ReadLineAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    public void Disconnect()
    {
        if (_process != null)
        {
            try
            {
                if (!_process.HasExited)
                {
                    _process.Kill();
                }
            }
            catch { }

            _process.Dispose();
            _process = null;
        }
        _initialized = false;
    }

    public void Dispose()
    {
        Disconnect();
    }
}

// MCP Protocol Types

public class McpResponse<T>
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("result")]
    public T? Result { get; set; }

    [JsonPropertyName("error")]
    public McpError? Error { get; set; }
}

public class McpError
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";
}

public class McpInitializeResult
{
    [JsonPropertyName("protocolVersion")]
    public string ProtocolVersion { get; set; } = "";

    [JsonPropertyName("serverInfo")]
    public McpServerInfo? ServerInfo { get; set; }
}

public class McpServerInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("version")]
    public string Version { get; set; } = "";
}

public class McpToolsListResult
{
    [JsonPropertyName("tools")]
    public List<McpTool> Tools { get; set; } = new();
}

public class McpTool
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("inputSchema")]
    public McpInputSchema? InputSchema { get; set; }
}

public class McpInputSchema
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "object";

    [JsonPropertyName("properties")]
    public Dictionary<string, McpPropertySchema>? Properties { get; set; }

    [JsonPropertyName("required")]
    public List<string>? Required { get; set; }
}

public class McpPropertySchema
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "string";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";
}

public class McpToolCallResult
{
    [JsonPropertyName("content")]
    public List<McpContent> Content { get; set; } = new();

    [JsonPropertyName("isError")]
    public bool IsError { get; set; }
}

public class McpContent
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "text";

    [JsonPropertyName("text")]
    public string? Text { get; set; }
}
