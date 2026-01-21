using Hazina.LLMs.Configuration;
using Hazina.LLMs.Registry.Models;

namespace Hazina.LLMs.Registry.Factory;

/// <summary>
/// Factory for creating ILLMClient instances from provider definitions and configurations.
/// Uses reflection to instantiate providers based on their type information.
/// </summary>
public class ProviderFactory
{
    private readonly ProviderRegistry _registry;
    private readonly Dictionary<string, Func<ProviderDefinition, ProviderInstanceConfig?, ILLMClient>> _factories = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Creates a new provider factory with the given registry.
    /// </summary>
    public ProviderFactory(ProviderRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    /// <summary>
    /// Registers a custom factory for a specific client kind.
    /// </summary>
    public void RegisterFactory(string clientKind, Func<ProviderDefinition, ProviderInstanceConfig?, ILLMClient> factory)
    {
        _factories[clientKind] = factory;
    }

    /// <summary>
    /// Creates an ILLMClient for the specified provider.
    /// </summary>
    public ILLMClient CreateClient(string providerId)
    {
        var provider = _registry.GetProvider(providerId)
            ?? throw new ArgumentException($"Provider not found: {providerId}");

        var config = _registry.GetConfiguration(providerId);

        return CreateClientForProvider(provider, config);
    }

    /// <summary>
    /// Creates an ILLMClient for the default provider.
    /// </summary>
    public ILLMClient CreateDefaultClient()
    {
        var provider = _registry.GetDefaultProvider()
            ?? throw new InvalidOperationException("No default provider configured");

        var config = _registry.GetConfiguration(provider.Id);

        return CreateClientForProvider(provider, config);
    }

    /// <summary>
    /// Creates an ILLMClient for a provider with explicit configuration.
    /// </summary>
    public ILLMClient CreateClient(string providerId, ProviderInstanceConfig config)
    {
        var provider = _registry.GetProvider(providerId)
            ?? throw new ArgumentException($"Provider not found: {providerId}");

        return CreateClientForProvider(provider, config);
    }

    /// <summary>
    /// Creates an ILLMClient directly from a provider definition.
    /// </summary>
    public ILLMClient CreateClientForProvider(ProviderDefinition provider, ProviderInstanceConfig? config)
    {
        // Check for custom factory first
        if (_factories.TryGetValue(provider.ClientKind, out var factory))
        {
            return factory(provider, config);
        }

        // Fall back to reflection-based instantiation
        return CreateClientViaReflection(provider, config);
    }

    /// <summary>
    /// Creates a client using reflection based on the provider's type information.
    /// </summary>
    private static ILLMClient CreateClientViaReflection(ProviderDefinition provider, ProviderInstanceConfig? config)
    {
        if (string.IsNullOrEmpty(provider.ConfigType) || string.IsNullOrEmpty(provider.ClientType))
        {
            throw new InvalidOperationException(
                $"Provider '{provider.Id}' does not have ConfigType and ClientType defined. " +
                "Either register a custom factory or define these properties.");
        }

        // Resolve config type
        var configType = Type.GetType(provider.ConfigType)
            ?? throw new InvalidOperationException($"Could not resolve config type: {provider.ConfigType}");

        // Create config instance
        var providerConfig = Activator.CreateInstance(configType) as IProviderConfig
            ?? throw new InvalidOperationException($"Could not create config instance: {provider.ConfigType}");

        // Apply configuration
        ApplyConfiguration(providerConfig, provider, config);

        // Resolve client type
        var clientType = Type.GetType(provider.ClientType)
            ?? throw new InvalidOperationException($"Could not resolve client type: {provider.ClientType}");

        // Create client instance
        var client = Activator.CreateInstance(clientType, providerConfig) as ILLMClient
            ?? throw new InvalidOperationException($"Could not create client instance: {provider.ClientType}");

        return client;
    }

    /// <summary>
    /// Applies configuration values to a provider config instance.
    /// </summary>
    private static void ApplyConfiguration(IProviderConfig providerConfig, ProviderDefinition provider, ProviderInstanceConfig? config)
    {
        // Apply defaults from provider definition
        if (!string.IsNullOrEmpty(provider.Endpoint))
            providerConfig.Endpoint = provider.Endpoint;

        var defaultModel = provider.Models.FirstOrDefault(m => m.IsDefault)?.Id
            ?? provider.Models.FirstOrDefault()?.Id;
        if (!string.IsNullOrEmpty(defaultModel))
            providerConfig.Model = defaultModel;

        // Apply instance configuration (overrides defaults)
        if (config == null)
            return;

        if (!string.IsNullOrEmpty(config.ApiKey))
            providerConfig.ApiKey = config.ApiKey;

        if (!string.IsNullOrEmpty(config.Endpoint))
            providerConfig.Endpoint = config.Endpoint;

        if (!string.IsNullOrEmpty(config.Model))
            providerConfig.Model = config.Model;

        if (!string.IsNullOrEmpty(config.LogPath))
            providerConfig.LogPath = config.LogPath;

        // Apply additional settings via reflection
        if (config.Settings.Count > 0)
        {
            var configObjectType = providerConfig.GetType();
            foreach (var (key, value) in config.Settings)
            {
                var property = configObjectType.GetProperty(key,
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);

                if (property != null && property.CanWrite)
                {
                    try
                    {
                        var convertedValue = Convert.ChangeType(value, property.PropertyType);
                        property.SetValue(providerConfig, convertedValue);
                    }
                    catch
                    {
                        // Ignore conversion errors for additional settings
                    }
                }
            }
        }
    }

    /// <summary>
    /// Validates that all configured providers can be instantiated.
    /// </summary>
    public IEnumerable<(string ProviderId, string? Error)> ValidateProviders()
    {
        foreach (var provider in _registry.GetConfiguredProviders())
        {
            var config = _registry.GetConfiguration(provider.Id);

            // Check if we have a factory or can resolve types
            if (!_factories.ContainsKey(provider.ClientKind))
            {
                if (string.IsNullOrEmpty(provider.ConfigType))
                    yield return (provider.Id, "Missing ConfigType");
                else if (Type.GetType(provider.ConfigType) == null)
                    yield return (provider.Id, $"Cannot resolve ConfigType: {provider.ConfigType}");

                if (string.IsNullOrEmpty(provider.ClientType))
                    yield return (provider.Id, "Missing ClientType");
                else if (Type.GetType(provider.ClientType) == null)
                    yield return (provider.Id, $"Cannot resolve ClientType: {provider.ClientType}");
            }

            // Check API key if required
            if (provider.RequiresApiKey && string.IsNullOrEmpty(config?.ApiKey))
                yield return (provider.Id, "API key required but not configured");
        }
    }
}
