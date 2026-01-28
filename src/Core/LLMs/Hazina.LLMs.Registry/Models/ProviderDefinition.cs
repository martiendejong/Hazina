using System.Text.Json.Serialization;

namespace Hazina.LLMs.Registry.Models;

/// <summary>
/// Defines a known LLM provider with its capabilities and configuration.
/// </summary>
public class ProviderDefinition
{
    /// <summary>
    /// Unique identifier for this provider (e.g., "openai", "anthropic", "ollama").
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    /// <summary>
    /// Display name for the provider (e.g., "OpenAI", "Anthropic Claude").
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    /// <summary>
    /// Client kind determines which ILLMClient implementation to use.
    /// Maps to a registered client factory.
    /// </summary>
    [JsonPropertyName("clientKind")]
    public required string ClientKind { get; set; }

    /// <summary>
    /// The fully qualified type name of the config class (e.g., "Hazina.LLMs.OpenAI.OpenAIConfig").
    /// Used for reflection-based instantiation.
    /// </summary>
    [JsonPropertyName("configType")]
    public string? ConfigType { get; set; }

    /// <summary>
    /// The fully qualified type name of the client class (e.g., "OpenAIClientWrapper").
    /// Used for reflection-based instantiation.
    /// </summary>
    [JsonPropertyName("clientType")]
    public string? ClientType { get; set; }

    /// <summary>
    /// Default API endpoint URL. Can be overridden in configuration.
    /// </summary>
    [JsonPropertyName("endpoint")]
    public string? Endpoint { get; set; }

    /// <summary>
    /// Available models for this provider.
    /// </summary>
    [JsonPropertyName("models")]
    public List<ModelDefinition> Models { get; set; } = new();

    /// <summary>
    /// Capabilities supported by this provider.
    /// </summary>
    [JsonPropertyName("capabilities")]
    public ProviderCapabilities Capabilities { get; set; } = new();

    /// <summary>
    /// Version identifier for the Hazina integration.
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0";

    /// <summary>
    /// Whether this provider requires an API key.
    /// </summary>
    [JsonPropertyName("requiresApiKey")]
    public bool RequiresApiKey { get; set; } = true;

    /// <summary>
    /// Whether this is a local provider (no network required).
    /// </summary>
    [JsonPropertyName("isLocal")]
    public bool IsLocal { get; set; } = false;

    /// <summary>
    /// Additional metadata for the provider.
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>
/// Defines a model available from a provider.
/// </summary>
public class ModelDefinition
{
    /// <summary>
    /// Model identifier used in API calls (e.g., "gpt-4o", "claude-3-5-sonnet-latest").
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    /// <summary>
    /// Display name for the model.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// Whether this is the default model for the provider.
    /// </summary>
    [JsonPropertyName("isDefault")]
    public bool IsDefault { get; set; }

    /// <summary>
    /// Maximum context window size in tokens.
    /// </summary>
    [JsonPropertyName("contextWindow")]
    public int? ContextWindow { get; set; }

    /// <summary>
    /// Model capabilities (chat, embedding, vision, etc.).
    /// </summary>
    [JsonPropertyName("capabilities")]
    public List<string> Capabilities { get; set; } = new();

    /// <summary>
    /// Input cost per million tokens (USD).
    /// </summary>
    [JsonPropertyName("inputCostPerMillion")]
    public decimal? InputCostPerMillion { get; set; }

    /// <summary>
    /// Output cost per million tokens (USD).
    /// </summary>
    [JsonPropertyName("outputCostPerMillion")]
    public decimal? OutputCostPerMillion { get; set; }
}

/// <summary>
/// Capabilities supported by a provider.
/// </summary>
public class ProviderCapabilities
{
    /// <summary>
    /// Supports chat completions.
    /// </summary>
    [JsonPropertyName("chat")]
    public bool Chat { get; set; } = true;

    /// <summary>
    /// Supports streaming responses.
    /// </summary>
    [JsonPropertyName("streaming")]
    public bool Streaming { get; set; } = true;

    /// <summary>
    /// Supports embedding generation.
    /// </summary>
    [JsonPropertyName("embeddings")]
    public bool Embeddings { get; set; } = false;

    /// <summary>
    /// Supports image generation.
    /// </summary>
    [JsonPropertyName("imageGeneration")]
    public bool ImageGeneration { get; set; } = false;

    /// <summary>
    /// Supports vision/image input.
    /// </summary>
    [JsonPropertyName("vision")]
    public bool Vision { get; set; } = false;

    /// <summary>
    /// Supports text-to-speech.
    /// </summary>
    [JsonPropertyName("tts")]
    public bool Tts { get; set; } = false;

    /// <summary>
    /// Supports tool/function calling.
    /// </summary>
    [JsonPropertyName("tools")]
    public bool Tools { get; set; } = false;

    /// <summary>
    /// Supports structured JSON output.
    /// </summary>
    [JsonPropertyName("jsonMode")]
    public bool JsonMode { get; set; } = false;
}
