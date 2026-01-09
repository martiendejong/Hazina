using Hazina.LLMs.Configuration;
using Microsoft.Extensions.Configuration;

namespace Hazina.LLMs;

/// <summary>
/// Supported LLM providers for Semantic Kernel.
/// </summary>
public enum LLMProvider
{
    OpenAI,
    AzureOpenAI,
    Anthropic,
    Ollama,
    Custom
}

/// <summary>
/// Configuration for Microsoft Semantic Kernel LLM integration.
/// Supports multiple providers with advanced settings.
/// </summary>
public class SemanticKernelConfig : HazinaConfigBase
{
    /// <summary>
    /// The LLM provider to use.
    /// </summary>
    public LLMProvider Provider { get; set; } = LLMProvider.OpenAI;

    /// <summary>
    /// Azure OpenAI deployment name (only for AzureOpenAI provider).
    /// </summary>
    public string DeploymentName { get; set; } = string.Empty;

    /// <summary>
    /// Model for image generation.
    /// </summary>
    public string ImageModel { get; set; } = "dall-e-3";

    /// <summary>
    /// Model for embeddings.
    /// </summary>
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";

    /// <summary>
    /// Model for text-to-speech.
    /// </summary>
    public string TtsModel { get; set; } = "tts-1";

    /// <summary>
    /// Temperature for generation (0.0-2.0).
    /// </summary>
    public double Temperature { get; set; } = 0.7;

    /// <summary>
    /// Maximum tokens to generate.
    /// </summary>
    public int MaxTokens { get; set; } = 4096;

    /// <summary>
    /// Top-p sampling parameter.
    /// </summary>
    public double TopP { get; set; } = 1.0;

    /// <summary>
    /// Frequency penalty for generation.
    /// </summary>
    public double FrequencyPenalty { get; set; } = 0.0;

    /// <summary>
    /// Presence penalty for generation.
    /// </summary>
    public double PresencePenalty { get; set; } = 0.0;

    /// <summary>
    /// Whether to use native structured output when available.
    /// </summary>
    public bool UseNativeStructuredOutput { get; set; } = true;

    /// <summary>
    /// Whether to fall back to schema injection if native structured output fails.
    /// </summary>
    public bool FallbackToSchemaInjection { get; set; } = true;

    /// <inheritdoc />
    protected override string ConfigurationSectionName => "SemanticKernel";

    /// <inheritdoc />
    protected override string? DefaultEndpoint => null;

    /// <inheritdoc />
    protected override string DefaultModel => "gpt-4o";

    /// <summary>
    /// Loads configuration from appsettings.json.
    /// Falls back to "OpenAI" section for backward compatibility.
    /// </summary>
    public static new SemanticKernelConfig Load()
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true)
            .Build();

        return LoadWithFallback(config);
    }

    /// <summary>
    /// Loads configuration with fallback to OpenAI section for backward compatibility.
    /// </summary>
    private static SemanticKernelConfig LoadWithFallback(IConfiguration config)
    {
        var skSection = config.GetSection("SemanticKernel");

        if (skSection.Exists())
        {
            var settings = new SemanticKernelConfig();
            skSection.Bind(settings);

            if (skSection["Provider"] != null)
                settings.Provider = Enum.Parse<LLMProvider>(skSection["Provider"]!);

            settings.ApplyDefaults();
            return settings;
        }

        // Fall back to OpenAI section for backward compatibility
        var openAISection = config.GetSection("OpenAI");
        if (openAISection.Exists())
        {
            return new SemanticKernelConfig
            {
                Provider = LLMProvider.OpenAI,
                ApiKey = openAISection["ApiKey"] ?? string.Empty,
                Model = openAISection["Model"] ?? "gpt-4o",
                EmbeddingModel = openAISection["EmbeddingModel"] ?? "text-embedding-3-small",
                ImageModel = openAISection["ImageModel"] ?? "dall-e-3",
                TtsModel = openAISection["TtsModel"] ?? "tts-1",
                LogPath = openAISection["LogPath"] ?? "c:\\projects\\hazinalogs.txt"
            };
        }

        return new SemanticKernelConfig();
    }

    /// <summary>
    /// Creates config from existing OpenAI config for backward compatibility.
    /// </summary>
    public static SemanticKernelConfig FromOpenAI(string apiKey, string model, string embeddingModel, string imageModel, string logPath, string ttsModel = "tts-1")
    {
        return new SemanticKernelConfig
        {
            Provider = LLMProvider.OpenAI,
            ApiKey = apiKey,
            Model = model,
            EmbeddingModel = embeddingModel,
            ImageModel = imageModel,
            TtsModel = ttsModel,
            LogPath = logPath
        };
    }
}
