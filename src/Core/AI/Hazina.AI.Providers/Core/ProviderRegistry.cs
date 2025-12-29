namespace Hazina.AI.Providers.Core;

/// <summary>
/// Registry of all available LLM providers
/// Maintains a mapping of provider names to their ILLMClient instances and metadata
/// </summary>
public class ProviderRegistry
{
    private readonly Dictionary<string, RegisteredProvider> _providers = new();
    private readonly object _lock = new();

    /// <summary>
    /// Register a new provider
    /// </summary>
    public void Register(string name, ILLMClient client, ProviderMetadata metadata)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Provider name cannot be empty", nameof(name));
        if (client == null)
            throw new ArgumentNullException(nameof(client));
        if (metadata == null)
            throw new ArgumentNullException(nameof(metadata));

        lock (_lock)
        {
            _providers[name] = new RegisteredProvider
            {
                Client = client,
                Metadata = metadata,
                RegisteredAt = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Unregister a provider
    /// </summary>
    public bool Unregister(string name)
    {
        lock (_lock)
        {
            return _providers.Remove(name);
        }
    }

    /// <summary>
    /// Get a provider by name
    /// </summary>
    public ILLMClient? GetProvider(string name)
    {
        lock (_lock)
        {
            return _providers.TryGetValue(name, out var provider)
                ? provider.Client
                : null;
        }
    }

    /// <summary>
    /// Get provider metadata
    /// </summary>
    public ProviderMetadata? GetMetadata(string name)
    {
        lock (_lock)
        {
            return _providers.TryGetValue(name, out var provider)
                ? provider.Metadata
                : null;
        }
    }

    /// <summary>
    /// Get all registered provider names
    /// </summary>
    public IEnumerable<string> GetProviderNames()
    {
        lock (_lock)
        {
            return _providers.Keys.ToList();
        }
    }

    /// <summary>
    /// Get all enabled providers
    /// </summary>
    public IEnumerable<(string Name, ILLMClient Client, ProviderMetadata Metadata)> GetEnabledProviders()
    {
        lock (_lock)
        {
            return _providers
                .Where(p => p.Value.Metadata.IsEnabled)
                .Select(p => (p.Key, p.Value.Client, p.Value.Metadata))
                .ToList();
        }
    }

    /// <summary>
    /// Get all providers sorted by priority
    /// </summary>
    public IEnumerable<(string Name, ILLMClient Client, ProviderMetadata Metadata)> GetProvidersByPriority()
    {
        lock (_lock)
        {
            return _providers
                .Where(p => p.Value.Metadata.IsEnabled)
                .OrderBy(p => p.Value.Metadata.Priority)
                .Select(p => (p.Key, p.Value.Client, p.Value.Metadata))
                .ToList();
        }
    }

    /// <summary>
    /// Find providers by capability
    /// </summary>
    public IEnumerable<(string Name, ILLMClient Client, ProviderMetadata Metadata)> FindByCapability(
        Func<ProviderCapabilities, bool> predicate)
    {
        lock (_lock)
        {
            return _providers
                .Where(p => p.Value.Metadata.IsEnabled && predicate(p.Value.Metadata.Capabilities))
                .OrderBy(p => p.Value.Metadata.Priority)
                .Select(p => (p.Key, p.Value.Client, p.Value.Metadata))
                .ToList();
        }
    }

    /// <summary>
    /// Enable/disable a provider
    /// </summary>
    public void SetProviderEnabled(string name, bool enabled)
    {
        lock (_lock)
        {
            if (_providers.TryGetValue(name, out var provider))
            {
                provider.Metadata.IsEnabled = enabled;
            }
        }
    }

    /// <summary>
    /// Update provider priority
    /// </summary>
    public void SetProviderPriority(string name, int priority)
    {
        lock (_lock)
        {
            if (_providers.TryGetValue(name, out var provider))
            {
                provider.Metadata.Priority = priority;
            }
        }
    }

    /// <summary>
    /// Check if a provider is registered
    /// </summary>
    public bool IsRegistered(string name)
    {
        lock (_lock)
        {
            return _providers.ContainsKey(name);
        }
    }

    /// <summary>
    /// Get count of registered providers
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _providers.Count;
            }
        }
    }

    /// <summary>
    /// Clear all providers
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _providers.Clear();
        }
    }

    private class RegisteredProvider
    {
        public required ILLMClient Client { get; init; }
        public required ProviderMetadata Metadata { get; init; }
        public DateTime RegisteredAt { get; init; }
    }
}
