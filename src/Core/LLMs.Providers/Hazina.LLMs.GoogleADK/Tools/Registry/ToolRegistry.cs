using System.Collections.Concurrent;
using Hazina.LLMs.GoogleADK.Tools.Mcp.Adapters;
using Hazina.LLMs.GoogleADK.Tools.Mcp.Client;
using Microsoft.Extensions.Logging;

namespace Hazina.LLMs.GoogleADK.Tools.Registry;

/// <summary>
/// Enhanced tool registry with discovery, registration, and management capabilities
/// </summary>
public class ToolRegistry
{
    private readonly ConcurrentDictionary<string, HazinaChatTool> _tools = new();
    private readonly ConcurrentDictionary<string, ToolMetadata> _metadata = new();
    private readonly List<ToolProvider> _providers = new();
    private readonly ILogger? _logger;

    public ToolRegistry(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Register a tool
    /// </summary>
    public void RegisterTool(HazinaChatTool tool, ToolMetadata? metadata = null)
    {
        if (_tools.TryAdd(tool.FunctionName, tool))
        {
            _metadata[tool.FunctionName] = metadata ?? new ToolMetadata
            {
                Name = tool.FunctionName,
                Description = tool.Description,
                Category = "general",
                RegisteredAt = DateTime.UtcNow
            };

            _logger?.LogInformation("Registered tool: {ToolName}", tool.FunctionName);
        }
        else
        {
            _logger?.LogWarning("Tool already registered: {ToolName}", tool.FunctionName);
        }
    }

    /// <summary>
    /// Unregister a tool
    /// </summary>
    public bool UnregisterTool(string toolName)
    {
        var removed = _tools.TryRemove(toolName, out _);
        if (removed)
        {
            _metadata.TryRemove(toolName, out _);
            _logger?.LogInformation("Unregistered tool: {ToolName}", toolName);
        }
        return removed;
    }

    /// <summary>
    /// Get a tool by name
    /// </summary>
    public HazinaChatTool? GetTool(string toolName)
    {
        _tools.TryGetValue(toolName, out var tool);
        return tool;
    }

    /// <summary>
    /// Get all registered tools
    /// </summary>
    public List<HazinaChatTool> GetAllTools()
    {
        return _tools.Values.ToList();
    }

    /// <summary>
    /// Get tools by category
    /// </summary>
    public List<HazinaChatTool> GetToolsByCategory(string category)
    {
        var toolNames = _metadata
            .Where(kvp => kvp.Value.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
            .Select(kvp => kvp.Key)
            .ToList();

        return toolNames
            .Select(name => _tools.TryGetValue(name, out var tool) ? tool : null)
            .Where(tool => tool != null)
            .Cast<HazinaChatTool>()
            .ToList();
    }

    /// <summary>
    /// Search tools by name or description
    /// </summary>
    public List<HazinaChatTool> SearchTools(string query)
    {
        var lowerQuery = query.ToLowerInvariant();

        return _metadata
            .Where(kvp =>
                kvp.Value.Name.ToLowerInvariant().Contains(lowerQuery) ||
                kvp.Value.Description.ToLowerInvariant().Contains(lowerQuery) ||
                kvp.Value.Tags.Any(tag => tag.ToLowerInvariant().Contains(lowerQuery)))
            .Select(kvp => _tools.TryGetValue(kvp.Key, out var tool) ? tool : null)
            .Where(tool => tool != null)
            .Cast<HazinaChatTool>()
            .ToList();
    }

    /// <summary>
    /// Get metadata for a tool
    /// </summary>
    public ToolMetadata? GetMetadata(string toolName)
    {
        _metadata.TryGetValue(toolName, out var metadata);
        return metadata;
    }

    /// <summary>
    /// Add a tool provider (e.g., MCP client)
    /// </summary>
    public void AddProvider(ToolProvider provider)
    {
        _providers.Add(provider);
        _logger?.LogInformation("Added tool provider: {ProviderName}", provider.Name);
    }

    /// <summary>
    /// Discover and load tools from all providers
    /// </summary>
    public async Task DiscoverToolsAsync(CancellationToken cancellationToken = default)
    {
        foreach (var provider in _providers)
        {
            try
            {
                _logger?.LogInformation("Discovering tools from provider: {ProviderName}", provider.Name);

                var tools = await provider.GetToolsAsync(cancellationToken);

                foreach (var tool in tools)
                {
                    RegisterTool(tool, new ToolMetadata
                    {
                        Name = tool.FunctionName,
                        Description = tool.Description,
                        Category = provider.Category,
                        Source = provider.Name,
                        RegisteredAt = DateTime.UtcNow
                    });
                }

                _logger?.LogInformation("Discovered {Count} tools from {ProviderName}", tools.Count, provider.Name);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error discovering tools from provider: {ProviderName}", provider.Name);
            }
        }
    }

    /// <summary>
    /// Get registry statistics
    /// </summary>
    public RegistryStatistics GetStatistics()
    {
        var categories = _metadata.Values
            .GroupBy(m => m.Category)
            .ToDictionary(g => g.Key, g => g.Count());

        return new RegistryStatistics
        {
            TotalTools = _tools.Count,
            ToolsByCategory = categories,
            ProviderCount = _providers.Count,
            LastDiscoveryTime = _providers
                .Select(p => p.LastDiscoveryTime ?? DateTime.MinValue)
                .DefaultIfEmpty(DateTime.MinValue)
                .Max()
        };
    }

    /// <summary>
    /// Clear all tools
    /// </summary>
    public void Clear()
    {
        _tools.Clear();
        _metadata.Clear();
        _logger?.LogInformation("Cleared all tools from registry");
    }
}

/// <summary>
/// Metadata about a tool
/// </summary>
public class ToolMetadata
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = "general";
    public string? Source { get; set; }
    public List<string> Tags { get; set; } = new();
    public DateTime RegisteredAt { get; set; }
    public Dictionary<string, object> CustomMetadata { get; set; } = new();
}

/// <summary>
/// Base class for tool providers
/// </summary>
public abstract class ToolProvider
{
    public string Name { get; protected set; } = string.Empty;
    public string Category { get; protected set; } = "general";
    public DateTime? LastDiscoveryTime { get; protected set; }

    public abstract Task<List<HazinaChatTool>> GetToolsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// MCP-based tool provider
/// </summary>
public class McpToolProvider : ToolProvider
{
    private readonly McpClient _mcpClient;
    private readonly McpToHazinaAdapter _adapter;

    public McpToolProvider(
        string name,
        McpClient mcpClient,
        string category = "mcp")
    {
        Name = name;
        Category = category;
        _mcpClient = mcpClient;
        _adapter = new McpToHazinaAdapter(mcpClient);
    }

    public override async Task<List<HazinaChatTool>> GetToolsAsync(CancellationToken cancellationToken = default)
    {
        var tools = await _adapter.GetHazinaToolsAsync(cancellationToken);
        LastDiscoveryTime = DateTime.UtcNow;
        return tools;
    }
}

/// <summary>
/// Statistics about the tool registry
/// </summary>
public class RegistryStatistics
{
    public int TotalTools { get; set; }
    public Dictionary<string, int> ToolsByCategory { get; set; } = new();
    public int ProviderCount { get; set; }
    public DateTime LastDiscoveryTime { get; set; }
}
