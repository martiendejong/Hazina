using Hazina.LLMs.GoogleADK.Core;
using Hazina.LLMs.GoogleADK.Tools.Mcp.Client;
using Hazina.LLMs.GoogleADK.Tools.Mcp.Transport;
using Hazina.LLMs.GoogleADK.Tools.Registry;
using Microsoft.Extensions.Logging;

namespace Hazina.LLMs.GoogleADK.Agents;

/// <summary>
/// Agent that connects to MCP servers and uses their tools
/// </summary>
public class McpAgent : ToolEnabledAgent
{
    private readonly List<McpClient> _mcpClients = new();

    public McpAgent(
        string name,
        ILLMClient llmClient,
        ToolRegistry toolRegistry,
        AgentContext? context = null,
        int maxHistorySize = 50) : base(name, llmClient, toolRegistry, context, false, maxHistorySize)
    {
    }

    /// <summary>
    /// Connect to an MCP server via stdio
    /// </summary>
    public async Task ConnectToStdioServerAsync(
        string serverCommand,
        string[] serverArgs,
        string providerName,
        string category = "mcp",
        CancellationToken cancellationToken = default)
    {
        var transport = new StdioMcpTransport(serverCommand, serverArgs, Context.Logger);
        var client = new McpClient(transport, "HazinaADK", "1.0.0", Context.Logger);

        await client.InitializeAsync(cancellationToken);
        _mcpClients.Add(client);

        // Add as a tool provider
        var provider = new McpToolProvider(providerName, client, category);
        var tools = await provider.GetToolsAsync(cancellationToken);

        foreach (var tool in tools)
        {
            AddTool(tool, new ToolMetadata
            {
                Name = tool.FunctionName,
                Description = tool.Description,
                Category = category,
                Source = providerName,
                RegisteredAt = DateTime.UtcNow
            });
        }

        Context.Log(LogLevel.Information,
            "Connected to MCP server '{ProviderName}' and loaded {Count} tools",
            providerName,
            tools.Count);
    }

    /// <summary>
    /// Connect to an MCP server via HTTP
    /// </summary>
    public async Task ConnectToHttpServerAsync(
        string serverUrl,
        string providerName,
        string category = "mcp",
        HttpClient? httpClient = null,
        CancellationToken cancellationToken = default)
    {
        var transport = new HttpMcpTransport(serverUrl, httpClient, Context.Logger);
        var client = new McpClient(transport, "HazinaADK", "1.0.0", Context.Logger);

        await client.InitializeAsync(cancellationToken);
        _mcpClients.Add(client);

        // Add as a tool provider
        var provider = new McpToolProvider(providerName, client, category);
        var tools = await provider.GetToolsAsync(cancellationToken);

        foreach (var tool in tools)
        {
            AddTool(tool, new ToolMetadata
            {
                Name = tool.FunctionName,
                Description = tool.Description,
                Category = category,
                Source = providerName,
                RegisteredAt = DateTime.UtcNow
            });
        }

        Context.Log(LogLevel.Information,
            "Connected to HTTP MCP server '{ProviderName}' at {Url} and loaded {Count} tools",
            providerName,
            serverUrl,
            tools.Count);
    }

    /// <summary>
    /// Add an existing MCP client
    /// </summary>
    public async Task AddMcpClientAsync(
        McpClient client,
        string providerName,
        string category = "mcp",
        CancellationToken cancellationToken = default)
    {
        _mcpClients.Add(client);

        // Add as a tool provider
        var provider = new McpToolProvider(providerName, client, category);
        var tools = await provider.GetToolsAsync(cancellationToken);

        foreach (var tool in tools)
        {
            AddTool(tool, new ToolMetadata
            {
                Name = tool.FunctionName,
                Description = tool.Description,
                Category = category,
                Source = providerName,
                RegisteredAt = DateTime.UtcNow
            });
        }

        Context.Log(LogLevel.Information,
            "Added MCP client '{ProviderName}' with {Count} tools",
            providerName,
            tools.Count);
    }

    /// <summary>
    /// Get all connected MCP clients
    /// </summary>
    public IReadOnlyList<McpClient> GetMcpClients()
    {
        return _mcpClients.AsReadOnly();
    }

    protected override async Task OnDisposeAsync()
    {
        // Dispose all MCP clients
        foreach (var client in _mcpClients)
        {
            await client.DisposeAsync();
        }

        _mcpClients.Clear();

        await base.OnDisposeAsync();
    }
}
