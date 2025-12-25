using Microsoft.Extensions.Configuration;

namespace Hazina.LLMs;

public enum LLMProvider
{
    OpenAI,
    AzureOpenAI,
    Anthropic,
    Ollama,
    Custom
}

public class SemanticKernelConfig
{
    public SemanticKernelConfig(
        LLMProvider provider = LLMProvider.OpenAI,
        string apiKey = "",
        string model = "gpt-4o",
        string embeddingModel = "text-embedding-ada-002",
        string imageModel = "dall-e-3",
        string ttsModel = "tts-1",
        string logPath = "c:\\projects\\hazinalogs.txt",
        string endpoint = "",
        string deploymentName = "")
    {
        Provider = provider;
        ApiKey = apiKey;
        Model = model;
        EmbeddingModel = embeddingModel;
        ImageModel = imageModel;
        TtsModel = ttsModel;
        LogPath = logPath;
        Endpoint = endpoint;
        DeploymentName = deploymentName;
    }

    // Provider configuration
    public LLMProvider Provider { get; set; }
    public string ApiKey { get; set; }
    public string Endpoint { get; set; } // For Azure OpenAI, Ollama, or custom endpoints
    public string DeploymentName { get; set; } // For Azure OpenAI deployment names

    // Model configuration
    public string Model { get; set; }
    public string ImageModel { get; set; }
    public string EmbeddingModel { get; set; }
    public string TtsModel { get; set; }

    // Logging
    public string LogPath { get; set; }

    // Advanced settings (optional)
    public double Temperature { get; set; } = 0.7;
    public int MaxTokens { get; set; } = 4096;
    public double TopP { get; set; } = 1.0;
    public double FrequencyPenalty { get; set; } = 0.0;
    public double PresencePenalty { get; set; } = 0.0;

    // Structured output settings
    public bool UseNativeStructuredOutput { get; set; } = true; // Auto-detect and prefer native when available
    public bool FallbackToSchemaInjection { get; set; } = true; // Fall back if native fails

    /// <summary>
    /// Load configuration from appsettings.json under "SemanticKernel" section
    /// Falls back to "OpenAI" section for backward compatibility
    /// </summary>
    public static SemanticKernelConfig Load()
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true)
            .Build();

        // Try SemanticKernel section first
        var skSettings = new SemanticKernelConfig();
        var skSection = config.GetSection("SemanticKernel");

        if (skSection.Exists())
        {
            skSection.Bind(skSettings);

            // Parse provider enum
            if (skSection["Provider"] != null)
            {
                skSettings.Provider = Enum.Parse<LLMProvider>(skSection["Provider"]!);
            }
        }
        else
        {
            // Fall back to OpenAI section for backward compatibility
            var openAISection = config.GetSection("OpenAI");
            if (openAISection.Exists())
            {
                skSettings.Provider = LLMProvider.OpenAI;
                skSettings.ApiKey = openAISection["ApiKey"] ?? "";
                skSettings.Model = openAISection["Model"] ?? "gpt-4o";
                skSettings.EmbeddingModel = openAISection["EmbeddingModel"] ?? "text-embedding-ada-002";
                skSettings.ImageModel = openAISection["ImageModel"] ?? "dall-e-3";
                skSettings.TtsModel = openAISection["TtsModel"] ?? "tts-1";
                skSettings.LogPath = openAISection["LogPath"] ?? "c:\\projects\\hazinalogs.txt";
            }
        }

        return skSettings;
    }

    /// <summary>
    /// Create config from existing OpenAI config for backward compatibility
    /// </summary>
    public static SemanticKernelConfig FromOpenAI(string apiKey, string model, string embeddingModel, string imageModel, string logPath, string ttsModel = "tts-1")
    {
        return new SemanticKernelConfig(
            provider: LLMProvider.OpenAI,
            apiKey: apiKey,
            model: model,
            embeddingModel: embeddingModel,
            imageModel: imageModel,
            ttsModel: ttsModel,
            logPath: logPath
        );
    }
}
