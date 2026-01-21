using System.Text.Json.Serialization;

namespace Hazina.LLMs.Registry.Models;

/// <summary>
/// Configuration for the provider registry, typically loaded from appsettings.json.
/// </summary>
public class ProviderRegistryConfig
{
    /// <summary>
    /// Section name in appsettings.json.
    /// </summary>
    public const string SectionName = "LLMProviders";

    /// <summary>
    /// Additional providers to add to the registry (extends built-in providers).
    /// </summary>
    [JsonPropertyName("providers")]
    public List<ProviderDefinition> Providers { get; set; } = new();

    /// <summary>
    /// Provider-specific configurations with API keys and settings.
    /// Key is the provider ID (e.g., "openai", "anthropic").
    /// </summary>
    [JsonPropertyName("configurations")]
    public Dictionary<string, ProviderInstanceConfig> Configurations { get; set; } = new();

    /// <summary>
    /// Default provider ID to use when not specified.
    /// </summary>
    [JsonPropertyName("defaultProvider")]
    public string? DefaultProvider { get; set; }

    /// <summary>
    /// Whether to load built-in provider definitions.
    /// </summary>
    [JsonPropertyName("loadBuiltIn")]
    public bool LoadBuiltIn { get; set; } = true;
}

/// <summary>
/// Instance configuration for a specific provider.
/// This is what you put in appsettings.json to configure a provider.
/// </summary>
public class ProviderInstanceConfig
{
    /// <summary>
    /// API key for authentication.
    /// </summary>
    [JsonPropertyName("apiKey")]
    public string? ApiKey { get; set; }

    /// <summary>
    /// Override the default endpoint URL.
    /// </summary>
    [JsonPropertyName("endpoint")]
    public string? Endpoint { get; set; }

    /// <summary>
    /// Override the default model.
    /// </summary>
    [JsonPropertyName("model")]
    public string? Model { get; set; }

    /// <summary>
    /// Embedding model to use.
    /// </summary>
    [JsonPropertyName("embeddingModel")]
    public string? EmbeddingModel { get; set; }

    /// <summary>
    /// Image generation model to use.
    /// </summary>
    [JsonPropertyName("imageModel")]
    public string? ImageModel { get; set; }

    /// <summary>
    /// Text-to-speech model to use.
    /// </summary>
    [JsonPropertyName("ttsModel")]
    public string? TtsModel { get; set; }

    /// <summary>
    /// Path for logging API interactions.
    /// </summary>
    [JsonPropertyName("logPath")]
    public string? LogPath { get; set; }

    /// <summary>
    /// Whether this provider is enabled.
    /// </summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Additional provider-specific settings.
    /// </summary>
    [JsonPropertyName("settings")]
    public Dictionary<string, object> Settings { get; set; } = new();
}
