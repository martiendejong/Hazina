using Hazina.LLMs.Configuration;
using Microsoft.Extensions.Configuration;

namespace Hazina.LLMs.OpenAI;

/// <summary>
/// Configuration for OpenAI API.
/// </summary>
public class OpenAIConfig : HazinaConfigBase
{
    /// <summary>
    /// Creates a default OpenAI configuration.
    /// </summary>
    public OpenAIConfig() { }

    /// <summary>
    /// Creates an OpenAI configuration with the specified API key.
    /// </summary>
    public OpenAIConfig(string apiKey)
    {
        ApiKey = apiKey;
    }

    /// <summary>
    /// Creates an OpenAI configuration with full parameters (backwards compatibility).
    /// </summary>
    public OpenAIConfig(string apiKey, string embeddingModel, string model, string imageModel, string logPath, string ttsModel)
    {
        ApiKey = apiKey;
        Model = model;
        EmbeddingModel = embeddingModel;
        ImageModel = imageModel;
        LogPath = logPath;
        TtsModel = ttsModel;
    }

    /// <summary>
    /// Model for image generation (e.g., "gpt-image-1", "dall-e-3").
    /// </summary>
    public string ImageModel { get; set; } = "gpt-image-1";

    /// <summary>
    /// Model for embeddings (e.g., "text-embedding-3-small").
    /// </summary>
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";

    /// <summary>
    /// Model for text-to-speech (e.g., "gpt-4o-mini-tts", "tts-1").
    /// </summary>
    public string TtsModel { get; set; } = "gpt-4o-mini-tts";

    /// <inheritdoc />
    protected override string ConfigurationSectionName => "OpenAI";

    /// <inheritdoc />
    protected override string? DefaultEndpoint => null; // OpenAI SDK handles this

    /// <inheritdoc />
    protected override string DefaultModel => "gpt-4o-mini";

    /// <summary>
    /// Loads configuration from appsettings.json.
    /// </summary>
    public static OpenAIConfig Load() => LoadFromConfiguration<OpenAIConfig>();

    /// <summary>
    /// Creates config from IConfiguration instance.
    /// </summary>
    public static OpenAIConfig FromConfiguration(IConfiguration configuration)
        => LoadFromConfiguration<OpenAIConfig>(configuration);
}
