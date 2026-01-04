using Microsoft.Extensions.Configuration;

namespace Hazina.LLMs;

/// <summary>
/// Configuration for Ollama local LLM API
/// </summary>
public class OllamaConfig
{
    public OllamaConfig(
        string endpoint = "http://localhost:11434",
        string model = "llama3.2",
        string embeddingModel = "nomic-embed-text",
        string logPath = "c:\\projects\\ollama-logs.txt")
    {
        Endpoint = endpoint;
        Model = model;
        EmbeddingModel = embeddingModel;
        LogPath = logPath;
    }

    /// <summary>
    /// Ollama API endpoint (default: http://localhost:11434)
    /// </summary>
    public string Endpoint { get; set; }

    /// <summary>
    /// Model name for chat completions (e.g., llama3.2, mistral, codellama)
    /// </summary>
    public string Model { get; set; }

    /// <summary>
    /// Model name for embeddings (e.g., nomic-embed-text, mxbai-embed-large)
    /// </summary>
    public string EmbeddingModel { get; set; }

    /// <summary>
    /// Path for logging API interactions
    /// </summary>
    public string LogPath { get; set; }

    /// <summary>
    /// Temperature for generation (0.0-2.0, default 0.7)
    /// </summary>
    public double Temperature { get; set; } = 0.7;

    /// <summary>
    /// Maximum tokens to generate
    /// </summary>
    public int MaxTokens { get; set; } = 4096;

    /// <summary>
    /// Top-p sampling parameter
    /// </summary>
    public double TopP { get; set; } = 1.0;

    /// <summary>
    /// Load configuration from appsettings.json "Ollama" section
    /// </summary>
    public static OllamaConfig Load()
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        var ollamaSettings = new OllamaConfig();
        config.GetSection("Ollama").Bind(ollamaSettings);
        return ollamaSettings;
    }

    /// <summary>
    /// Create config from IConfiguration
    /// </summary>
    public static OllamaConfig FromConfiguration(IConfiguration configuration)
    {
        var ollamaSettings = new OllamaConfig();
        configuration.GetSection("Ollama").Bind(ollamaSettings);
        return ollamaSettings;
    }
}
