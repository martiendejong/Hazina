using System.Text.Json;
using Hazina.LLMs.GoogleADK.Tools.Mcp.Models;
using Microsoft.Extensions.Logging;

namespace Hazina.LLMs.GoogleADK.Tools.Mcp.Server;

/// <summary>
/// MCP server that exposes tools, resources, and prompts
/// </summary>
public class McpServer
{
    private readonly ILogger? _logger;
    private readonly string _serverName;
    private readonly string _serverVersion;
    private readonly List<McpTool> _tools = new();
    private readonly List<McpResource> _resources = new();
    private readonly List<McpPrompt> _prompts = new();
    private readonly Dictionary<string, Func<Dictionary<string, object>, CancellationToken, Task<CallToolResponse>>> _toolHandlers = new();
    private readonly Dictionary<string, Func<CancellationToken, Task<ReadResourceResponse>>> _resourceHandlers = new();
    private readonly Dictionary<string, Func<Dictionary<string, string>?, CancellationToken, Task<GetPromptResponse>>> _promptHandlers = new();

    public McpServer(
        string serverName = "HazinaADKServer",
        string serverVersion = "1.0.0",
        ILogger? logger = null)
    {
        _serverName = serverName;
        _serverVersion = serverVersion;
        _logger = logger;
    }

    /// <summary>
    /// Register a tool with the server
    /// </summary>
    public void RegisterTool(
        McpTool tool,
        Func<Dictionary<string, object>, CancellationToken, Task<CallToolResponse>> handler)
    {
        _tools.Add(tool);
        _toolHandlers[tool.Name] = handler;
        _logger?.LogInformation("Registered MCP tool: {ToolName}", tool.Name);
    }

    /// <summary>
    /// Register a resource with the server
    /// </summary>
    public void RegisterResource(
        McpResource resource,
        Func<CancellationToken, Task<ReadResourceResponse>> handler)
    {
        _resources.Add(resource);
        _resourceHandlers[resource.Uri] = handler;
        _logger?.LogInformation("Registered MCP resource: {ResourceUri}", resource.Uri);
    }

    /// <summary>
    /// Register a prompt with the server
    /// </summary>
    public void RegisterPrompt(
        McpPrompt prompt,
        Func<Dictionary<string, string>?, CancellationToken, Task<GetPromptResponse>> handler)
    {
        _prompts.Add(prompt);
        _promptHandlers[prompt.Name] = handler;
        _logger?.LogInformation("Registered MCP prompt: {PromptName}", prompt.Name);
    }

    /// <summary>
    /// Handle an incoming MCP request
    /// </summary>
    public async Task<McpResponse> HandleRequestAsync(McpRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.LogDebug("Handling MCP request: {Method}", request.Method);

            object? result = request.Method switch
            {
                "initialize" => HandleInitialize(request),
                "tools/list" => HandleListTools(),
                "tools/call" => await HandleCallToolAsync(request, cancellationToken),
                "resources/list" => HandleListResources(),
                "resources/read" => await HandleReadResourceAsync(request, cancellationToken),
                "prompts/list" => HandleListPrompts(),
                "prompts/get" => await HandleGetPromptAsync(request, cancellationToken),
                _ => throw new InvalidOperationException($"Unknown method: {request.Method}")
            };

            return new McpResponse
            {
                Id = request.Id,
                Result = result
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error handling MCP request: {Method}", request.Method);

            return new McpResponse
            {
                Id = request.Id,
                Error = new McpError
                {
                    Code = McpErrorCode.InternalError,
                    Message = ex.Message,
                    Data = ex.StackTrace
                }
            };
        }
    }

    private object HandleInitialize(McpRequest request)
    {
        return new
        {
            protocolVersion = "2024-11-05",
            capabilities = new
            {
                tools = new { },
                resources = new { },
                prompts = new { }
            },
            serverInfo = new
            {
                name = _serverName,
                version = _serverVersion
            }
        };
    }

    private ListToolsResponse HandleListTools()
    {
        return new ListToolsResponse
        {
            Tools = new List<McpTool>(_tools)
        };
    }

    private async Task<CallToolResponse> HandleCallToolAsync(McpRequest request, CancellationToken cancellationToken)
    {
        var callRequest = JsonSerializer.Deserialize<CallToolRequest>(
            JsonSerializer.SerializeToElement(request.Params));

        if (callRequest == null)
        {
            throw new InvalidOperationException("Invalid tool call request");
        }

        if (!_toolHandlers.TryGetValue(callRequest.Name, out var handler))
        {
            throw new InvalidOperationException($"Tool not found: {callRequest.Name}");
        }

        return await handler(callRequest.Arguments, cancellationToken);
    }

    private ListResourcesResponse HandleListResources()
    {
        return new ListResourcesResponse
        {
            Resources = new List<McpResource>(_resources)
        };
    }

    private async Task<ReadResourceResponse> HandleReadResourceAsync(McpRequest request, CancellationToken cancellationToken)
    {
        var readRequest = JsonSerializer.Deserialize<ReadResourceRequest>(
            JsonSerializer.SerializeToElement(request.Params));

        if (readRequest == null)
        {
            throw new InvalidOperationException("Invalid resource read request");
        }

        if (!_resourceHandlers.TryGetValue(readRequest.Uri, out var handler))
        {
            throw new InvalidOperationException($"Resource not found: {readRequest.Uri}");
        }

        return await handler(cancellationToken);
    }

    private ListPromptsResponse HandleListPrompts()
    {
        return new ListPromptsResponse
        {
            Prompts = new List<McpPrompt>(_prompts)
        };
    }

    private async Task<GetPromptResponse> HandleGetPromptAsync(McpRequest request, CancellationToken cancellationToken)
    {
        var promptRequest = JsonSerializer.Deserialize<GetPromptRequest>(
            JsonSerializer.SerializeToElement(request.Params));

        if (promptRequest == null)
        {
            throw new InvalidOperationException("Invalid prompt request");
        }

        if (!_promptHandlers.TryGetValue(promptRequest.Name, out var handler))
        {
            throw new InvalidOperationException($"Prompt not found: {promptRequest.Name}");
        }

        return await handler(promptRequest.Arguments, cancellationToken);
    }

    /// <summary>
    /// Get statistics about registered capabilities
    /// </summary>
    public ServerStatistics GetStatistics()
    {
        return new ServerStatistics
        {
            ToolCount = _tools.Count,
            ResourceCount = _resources.Count,
            PromptCount = _prompts.Count
        };
    }
}

/// <summary>
/// Statistics about the MCP server
/// </summary>
public class ServerStatistics
{
    public int ToolCount { get; set; }
    public int ResourceCount { get; set; }
    public int PromptCount { get; set; }
}
