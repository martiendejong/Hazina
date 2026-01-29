using Hazina.LLMs;
using Spectre.Console;

namespace Hazina.Agents.Tools.Mcp;

/// <summary>
/// Manages multiple MCP server connections and provides unified tool access.
/// </summary>
public class McpManager : IDisposable
{
    private readonly Dictionary<string, McpClient> _clients = new();
    private readonly List<HazinaChatTool> _tools = new();
    private readonly bool _verbose;

    public IReadOnlyList<HazinaChatTool> Tools => _tools.AsReadOnly();
    public IReadOnlyDictionary<string, McpClient> Clients => _clients;
    public int ConnectedServers => _clients.Count(c => c.Value.IsConnected);

    public McpManager(bool verbose = false)
    {
        _verbose = verbose;
    }

    /// <summary>
    /// Loads and connects to MCP servers from a settings object.
    /// </summary>
    public async Task LoadServersAsync(McpSettings settings, CancellationToken cancel = default)
    {
        foreach (var (name, config) in settings.McpServers)
        {
            if (config.Disabled)
            {
                if (_verbose)
                {
                    AnsiConsole.MarkupLine($"[dim]MCP server '{name}' is disabled[/]");
                }
                continue;
            }

            await ConnectServerAsync(name, config, cancel);
        }
    }

    /// <summary>
    /// Connects to a single MCP server.
    /// </summary>
    public async Task<bool> ConnectServerAsync(string name, McpServerConfig config, CancellationToken cancel = default)
    {
        try
        {
            var client = new McpClient(
                name,
                config.Command,
                config.Args?.ToArray(),
                config.Env
            );

            if (_verbose)
            {
                AnsiConsole.MarkupLine($"[dim]Connecting to MCP server '{name}'...[/]");
            }

            if (await client.ConnectAsync(cancel))
            {
                _clients[name] = client;

                // Create tools from the connected server
                var serverTools = McpToolWrapper.CreateToolsFromClient(client);
                _tools.AddRange(serverTools);

                if (_verbose)
                {
                    AnsiConsole.MarkupLine($"[green]✓[/] [dim]MCP server '{name}' connected ({client.Tools.Count} tools)[/]");
                }

                return true;
            }
            else
            {
                if (_verbose)
                {
                    AnsiConsole.MarkupLine($"[yellow]⚠[/] [dim]Failed to connect to MCP server '{name}'[/]");
                }
                return false;
            }
        }
        catch (Exception ex)
        {
            if (_verbose)
            {
                AnsiConsole.MarkupLine($"[red]✗[/] [dim]Error connecting to MCP server '{name}': {ex.Message}[/]");
            }
            return false;
        }
    }

    /// <summary>
    /// Disconnects from a specific MCP server.
    /// </summary>
    public void DisconnectServer(string name)
    {
        if (_clients.TryGetValue(name, out var client))
        {
            // Remove tools from this server
            var prefix = $"mcp_{name}_";
            _tools.RemoveAll(t => t.FunctionName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

            client.Disconnect();
            _clients.Remove(name);
        }
    }

    /// <summary>
    /// Gets tools from a specific MCP server.
    /// </summary>
    public IEnumerable<HazinaChatTool> GetToolsFromServer(string serverName)
    {
        var prefix = $"mcp_{serverName}_";
        return _tools.Where(t => t.FunctionName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Lists all available MCP tools.
    /// </summary>
    public IEnumerable<(string ServerName, string ToolName, string Description)> ListAllTools()
    {
        foreach (var (name, client) in _clients)
        {
            foreach (var tool in client.Tools)
            {
                yield return (name, tool.Name, tool.Description);
            }
        }
    }

    public void Dispose()
    {
        foreach (var client in _clients.Values)
        {
            client.Dispose();
        }
        _clients.Clear();
        _tools.Clear();
    }
}
