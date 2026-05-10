using System.Collections.Concurrent;

/// <summary>
/// Central registry for managing multiple tool providers.
/// </summary>
public class ToolProviderRegistry : IToolProviderRegistry
{
    private readonly ConcurrentDictionary<string, IToolProvider> _providers = new();

    public bool RegisterProvider(IToolProvider provider)
    {
        if (provider == null) throw new ArgumentNullException(nameof(provider));
        return _providers.TryAdd(provider.ProviderId, provider);
    }

    public bool UnregisterProvider(string providerId)
    {
        return _providers.TryRemove(providerId, out _);
    }

    public IToolProvider? GetProvider(string providerId)
    {
        return _providers.TryGetValue(providerId, out var provider) ? provider : null;
    }

    public IReadOnlyList<IToolProvider> GetAllProviders()
    {
        return _providers.Values.ToList();
    }

    public async Task<IReadOnlyList<HazinaChatTool>> GetAllToolsAsync(CancellationToken cancellationToken = default)
    {
        var tools = new List<HazinaChatTool>();
        foreach (var provider in _providers.Values)
        {
            var providerTools = await provider.GetToolsAsync(cancellationToken);
            tools.AddRange(providerTools);
        }
        return tools;
    }

    public async Task<(IToolProvider? Provider, HazinaChatTool? Tool)> FindToolAsync(
        string toolName,
        CancellationToken cancellationToken = default)
    {
        foreach (var provider in _providers.Values)
        {
            var tool = await provider.GetToolAsync(toolName, cancellationToken);
            if (tool != null) return (provider, tool);
        }
        return (null, null);
    }
}

public interface IToolProviderRegistry
{
    bool RegisterProvider(IToolProvider provider);
    bool UnregisterProvider(string providerId);
    IToolProvider? GetProvider(string providerId);
    IReadOnlyList<IToolProvider> GetAllProviders();
    Task<IReadOnlyList<HazinaChatTool>> GetAllToolsAsync(CancellationToken cancellationToken = default);
    Task<(IToolProvider? Provider, HazinaChatTool? Tool)> FindToolAsync(string toolName, CancellationToken cancellationToken = default);
}
