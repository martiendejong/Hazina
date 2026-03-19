using System.Collections.Concurrent;

/// <summary>
/// Built-in tool provider for statically defined tools.
/// </summary>
public class BuiltInToolProvider : IToolProvider
{
    private readonly ConcurrentDictionary<string, HazinaChatTool> _tools = new();

    public string ProviderId => "builtin";
    public string DisplayName => "Built-in Tools";
    public string Description => "Core tools built into Hazina framework";

    public BuiltInToolProvider() { }

    public BuiltInToolProvider(IEnumerable<HazinaChatTool> initialTools)
    {
        foreach (var tool in initialTools)
        {
            _tools.TryAdd(tool.FunctionName, tool);
        }
    }

    public Task<IReadOnlyList<HazinaChatTool>> GetToolsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<HazinaChatTool> result = _tools.Values.ToList();
        return Task.FromResult(result);
    }

    public Task<HazinaChatTool?> GetToolAsync(string toolName, CancellationToken cancellationToken = default)
    {
        _tools.TryGetValue(toolName, out var tool);
        return Task.FromResult(tool);
    }

    public Task<bool> HasToolAsync(string toolName, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_tools.ContainsKey(toolName));
    }

    public Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public ToolProviderCapabilities GetCapabilities()
    {
        return new ToolProviderCapabilities
        {
            SupportsDynamicRegistration = true,
            SupportsUnregistration = true,
            SupportsHotReload = false,
            RequiresConfiguration = false,
            MaxTools = null,
            Tags = new List<string> { "builtin", "core" }
        };
    }

    public bool RegisterTool(HazinaChatTool tool)
    {
        if (tool == null) throw new ArgumentNullException(nameof(tool));
        return _tools.TryAdd(tool.FunctionName, tool);
    }

    public bool UnregisterTool(string toolName)
    {
        return _tools.TryRemove(toolName, out _);
    }
}
