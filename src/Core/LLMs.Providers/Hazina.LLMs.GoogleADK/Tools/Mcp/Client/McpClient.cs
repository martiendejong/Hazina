using System.Text.Json;
using Hazina.LLMs.GoogleADK.Tools.Mcp.Models;
using Hazina.LLMs.GoogleADK.Tools.Mcp.Transport;
using Microsoft.Extensions.Logging;

namespace Hazina.LLMs.GoogleADK.Tools.Mcp.Client;

/// <summary>
/// Client for connecting to MCP servers and invoking their capabilities
/// </summary>
public class McpClient : IAsyncDisposable
{
    private readonly IMcpTransport _transport;
    private readonly ILogger? _logger;
    private readonly string _clientName;
    private readonly string _clientVersion;
    private bool _initialized;

    public bool IsConnected => _transport.IsConnected;

    public McpClient(
        IMcpTransport transport,
        string clientName = "HazinaADK",
        string clientVersion = "1.0.0",
        ILogger? logger = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _clientName = clientName;
        _clientVersion = clientVersion;
        _logger = logger;
    }

    /// <summary>
    /// Initialize the MCP client and perform handshake
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        await _transport.ConnectAsync(cancellationToken);

        // Perform initialization handshake
        var initRequest = new McpRequest
        {
            Method = "initialize",
            Params = new
            {
                protocolVersion = "2024-11-05",
                capabilities = new
                {
                    tools = new { },
                    resources = new { },
                    prompts = new { }
                },
                clientInfo = new
                {
                    name = _clientName,
                    version = _clientVersion
                }
            }
        };

        var response = await _transport.SendRequestAsync(initRequest, cancellationToken);

        if (response.Error != null)
        {
            throw new InvalidOperationException($"MCP initialization failed: {response.Error.Message}");
        }

        // Send initialized notification
        await _transport.SendNotificationAsync(new McpNotification
        {
            Method = "notifications/initialized"
        }, cancellationToken);

        _initialized = true;
        _logger?.LogInformation("MCP client initialized successfully");
    }

    /// <summary>
    /// List all available tools from the server
    /// </summary>
    public async Task<List<McpTool>> ListToolsAsync(CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        var request = new McpRequest
        {
            Method = "tools/list",
            Params = new ListToolsRequest()
        };

        var response = await _transport.SendRequestAsync(request, cancellationToken);

        if (response.Error != null)
        {
            throw new InvalidOperationException($"Failed to list tools: {response.Error.Message}");
        }

        var result = JsonSerializer.Deserialize<ListToolsResponse>(
            JsonSerializer.SerializeToElement(response.Result));

        return result?.Tools ?? new List<McpTool>();
    }

    /// <summary>
    /// Call a tool on the server
    /// </summary>
    public async Task<CallToolResponse> CallToolAsync(
        string toolName,
        Dictionary<string, object> arguments,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        var request = new McpRequest
        {
            Method = "tools/call",
            Params = new CallToolRequest
            {
                Name = toolName,
                Arguments = arguments
            }
        };

        var response = await _transport.SendRequestAsync(request, cancellationToken);

        if (response.Error != null)
        {
            throw new InvalidOperationException($"Tool call failed: {response.Error.Message}");
        }

        var result = JsonSerializer.Deserialize<CallToolResponse>(
            JsonSerializer.SerializeToElement(response.Result));

        if (result == null)
        {
            throw new InvalidOperationException("Received null tool call result");
        }

        return result;
    }

    /// <summary>
    /// List all available resources from the server
    /// </summary>
    public async Task<List<McpResource>> ListResourcesAsync(CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        var request = new McpRequest
        {
            Method = "resources/list",
            Params = new ListResourcesRequest()
        };

        var response = await _transport.SendRequestAsync(request, cancellationToken);

        if (response.Error != null)
        {
            throw new InvalidOperationException($"Failed to list resources: {response.Error.Message}");
        }

        var result = JsonSerializer.Deserialize<ListResourcesResponse>(
            JsonSerializer.SerializeToElement(response.Result));

        return result?.Resources ?? new List<McpResource>();
    }

    /// <summary>
    /// Read a resource from the server
    /// </summary>
    public async Task<ReadResourceResponse> ReadResourceAsync(
        string uri,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        var request = new McpRequest
        {
            Method = "resources/read",
            Params = new ReadResourceRequest { Uri = uri }
        };

        var response = await _transport.SendRequestAsync(request, cancellationToken);

        if (response.Error != null)
        {
            throw new InvalidOperationException($"Failed to read resource: {response.Error.Message}");
        }

        var result = JsonSerializer.Deserialize<ReadResourceResponse>(
            JsonSerializer.SerializeToElement(response.Result));

        if (result == null)
        {
            throw new InvalidOperationException("Received null resource read result");
        }

        return result;
    }

    /// <summary>
    /// List all available prompts from the server
    /// </summary>
    public async Task<List<McpPrompt>> ListPromptsAsync(CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        var request = new McpRequest
        {
            Method = "prompts/list",
            Params = new ListPromptsRequest()
        };

        var response = await _transport.SendRequestAsync(request, cancellationToken);

        if (response.Error != null)
        {
            throw new InvalidOperationException($"Failed to list prompts: {response.Error.Message}");
        }

        var result = JsonSerializer.Deserialize<ListPromptsResponse>(
            JsonSerializer.SerializeToElement(response.Result));

        return result?.Prompts ?? new List<McpPrompt>();
    }

    /// <summary>
    /// Get a prompt from the server
    /// </summary>
    public async Task<GetPromptResponse> GetPromptAsync(
        string promptName,
        Dictionary<string, string>? arguments = null,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        var request = new McpRequest
        {
            Method = "prompts/get",
            Params = new GetPromptRequest
            {
                Name = promptName,
                Arguments = arguments
            }
        };

        var response = await _transport.SendRequestAsync(request, cancellationToken);

        if (response.Error != null)
        {
            throw new InvalidOperationException($"Failed to get prompt: {response.Error.Message}");
        }

        var result = JsonSerializer.Deserialize<GetPromptResponse>(
            JsonSerializer.SerializeToElement(response.Result));

        if (result == null)
        {
            throw new InvalidOperationException("Received null prompt result");
        }

        return result;
    }

    private void EnsureInitialized()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("MCP client is not initialized. Call InitializeAsync first.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _transport.CloseAsync();
        await _transport.DisposeAsync();
    }
}
