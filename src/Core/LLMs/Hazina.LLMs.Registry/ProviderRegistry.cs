using System.Reflection;
using System.Text.Json;
using Hazina.LLMs.Registry.Models;
using Microsoft.Extensions.Configuration;

namespace Hazina.LLMs.Registry;

/// <summary>
/// Central registry for LLM providers.
/// Loads built-in provider definitions and allows runtime configuration/extension.
/// </summary>
public class ProviderRegistry
{
    private readonly Dictionary<string, ProviderDefinition> _providers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ProviderInstanceConfig> _configurations = new(StringComparer.OrdinalIgnoreCase);
    private string? _defaultProviderId;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>
    /// Gets all registered provider definitions.
    /// </summary>
    public IReadOnlyDictionary<string, ProviderDefinition> Providers => _providers;

    /// <summary>
    /// Gets all provider instance configurations.
    /// </summary>
    public IReadOnlyDictionary<string, ProviderInstanceConfig> Configurations => _configurations;

    /// <summary>
    /// Gets the default provider ID.
    /// </summary>
    public string? DefaultProviderId => _defaultProviderId;

    /// <summary>
    /// Creates a new provider registry with built-in providers loaded.
    /// </summary>
    public ProviderRegistry()
    {
        LoadBuiltInProviders();
    }

    /// <summary>
    /// Creates a provider registry and loads configuration from IConfiguration.
    /// </summary>
    public ProviderRegistry(IConfiguration configuration) : this()
    {
        LoadFromConfiguration(configuration);
    }

    /// <summary>
    /// Creates a provider registry from a ProviderRegistryConfig.
    /// </summary>
    public ProviderRegistry(ProviderRegistryConfig config)
    {
        if (config.LoadBuiltIn)
            LoadBuiltInProviders();

        foreach (var provider in config.Providers)
        {
            RegisterProvider(provider);
        }

        foreach (var (id, instanceConfig) in config.Configurations)
        {
            ConfigureProvider(id, instanceConfig);
        }

        _defaultProviderId = config.DefaultProvider;
    }

    /// <summary>
    /// Loads built-in provider definitions from embedded resources.
    /// </summary>
    private void LoadBuiltInProviders()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = "Hazina.LLMs.Registry.Resources.providers.json";

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            throw new InvalidOperationException($"Could not find embedded resource: {resourceName}");
        }

        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();
        var data = JsonSerializer.Deserialize<ProvidersJsonRoot>(json, JsonOptions);

        if (data?.Providers != null)
        {
            foreach (var provider in data.Providers)
            {
                _providers[provider.Id] = provider;
            }
        }
    }

    /// <summary>
    /// Loads additional configuration from IConfiguration (appsettings.json).
    /// </summary>
    public void LoadFromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection(ProviderRegistryConfig.SectionName);
        if (!section.Exists())
            return;

        var config = new ProviderRegistryConfig();
        section.Bind(config);

        // Add custom providers
        foreach (var provider in config.Providers)
        {
            RegisterProvider(provider);
        }

        // Apply configurations
        foreach (var (id, instanceConfig) in config.Configurations)
        {
            ConfigureProvider(id, instanceConfig);
        }

        if (!string.IsNullOrEmpty(config.DefaultProvider))
            _defaultProviderId = config.DefaultProvider;
    }

    /// <summary>
    /// Loads additional providers from a JSON file.
    /// </summary>
    public void LoadFromFile(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Provider file not found: {path}");

        var json = File.ReadAllText(path);
        var data = JsonSerializer.Deserialize<ProvidersJsonRoot>(json, JsonOptions);

        if (data?.Providers != null)
        {
            foreach (var provider in data.Providers)
            {
                RegisterProvider(provider);
            }
        }
    }

    /// <summary>
    /// Registers a provider definition.
    /// </summary>
    public void RegisterProvider(ProviderDefinition provider)
    {
        _providers[provider.Id] = provider;
    }

    /// <summary>
    /// Configures a provider instance with API key and settings.
    /// </summary>
    public void ConfigureProvider(string providerId, ProviderInstanceConfig config)
    {
        _configurations[providerId] = config;
    }

    /// <summary>
    /// Gets a provider definition by ID.
    /// </summary>
    public ProviderDefinition? GetProvider(string providerId)
    {
        return _providers.TryGetValue(providerId, out var provider) ? provider : null;
    }

    /// <summary>
    /// Gets the configuration for a provider.
    /// </summary>
    public ProviderInstanceConfig? GetConfiguration(string providerId)
    {
        return _configurations.TryGetValue(providerId, out var config) ? config : null;
    }

    /// <summary>
    /// Gets the default provider definition.
    /// </summary>
    public ProviderDefinition? GetDefaultProvider()
    {
        if (string.IsNullOrEmpty(_defaultProviderId))
            return _providers.Values.FirstOrDefault();

        return GetProvider(_defaultProviderId);
    }

    /// <summary>
    /// Sets the default provider.
    /// </summary>
    public void SetDefaultProvider(string providerId)
    {
        if (!_providers.ContainsKey(providerId))
            throw new ArgumentException($"Provider not found: {providerId}");

        _defaultProviderId = providerId;
    }

    /// <summary>
    /// Gets all providers that have a specific capability.
    /// </summary>
    public IEnumerable<ProviderDefinition> GetProvidersWithCapability(string capability)
    {
        return _providers.Values.Where(p => HasCapability(p, capability));
    }

    /// <summary>
    /// Gets all configured and enabled providers.
    /// </summary>
    public IEnumerable<ProviderDefinition> GetConfiguredProviders()
    {
        return _providers.Values.Where(p =>
            _configurations.TryGetValue(p.Id, out var config) && config.Enabled);
    }

    /// <summary>
    /// Gets all local providers (no network required).
    /// </summary>
    public IEnumerable<ProviderDefinition> GetLocalProviders()
    {
        return _providers.Values.Where(p => p.IsLocal);
    }

    /// <summary>
    /// Checks if a provider has a specific capability.
    /// </summary>
    private static bool HasCapability(ProviderDefinition provider, string capability)
    {
        return capability.ToLowerInvariant() switch
        {
            "chat" => provider.Capabilities.Chat,
            "streaming" => provider.Capabilities.Streaming,
            "embeddings" => provider.Capabilities.Embeddings,
            "imagegeneration" or "image" => provider.Capabilities.ImageGeneration,
            "vision" => provider.Capabilities.Vision,
            "tts" => provider.Capabilities.Tts,
            "tools" => provider.Capabilities.Tools,
            "jsonmode" or "json" => provider.Capabilities.JsonMode,
            _ => false
        };
    }

    /// <summary>
    /// Root object for providers.json deserialization.
    /// </summary>
    private class ProvidersJsonRoot
    {
        public string? Version { get; set; }
        public List<ProviderDefinition> Providers { get; set; } = new();
    }
}
