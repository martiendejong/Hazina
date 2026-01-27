using Hazina.LLMs.Configuration;
using Microsoft.Extensions.Configuration;

namespace Hazina.LLMs;

/// <summary>
/// Configuration for Ollama local LLM API.
/// </summary>
public class OllamaConfig : HazinaConfigBase
{
    /// <summary>
    /// Model name for embeddings (e.g., nomic-embed-text, mxbai-embed-large).
    /// </summary>
    public string EmbeddingModel { get; set; } = "nomic-embed-text";

    /// <summary>
    /// Temperature for generation (0.0-2.0, default 0.7).
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

    /// <inheritdoc />
    protected override string ConfigurationSectionName => "Ollama";

    /// <inheritdoc />
    protected override string? DefaultEndpoint =>
        Environment.GetEnvironmentVariable("OLLAMA_HOST") ?? "http://localhost:11434";

    /// <inheritdoc />
    protected override string DefaultModel => "llama3.2";

    /// <inheritdoc />
    public override IEnumerable<string> Validate()
    {
        // Ollama doesn't require an API key (local)
        if (string.IsNullOrWhiteSpace(Model))
            yield return $"{GetType().Name}: Model is required";

        if (string.IsNullOrWhiteSpace(Endpoint))
            yield return $"{GetType().Name}: Endpoint is required";
    }

    /// <summary>
    /// Loads configuration from appsettings.json.
    /// </summary>
    public static OllamaConfig Load() => LoadFromConfiguration<OllamaConfig>();

    /// <summary>
    /// Creates config from IConfiguration instance.
    /// </summary>
    public static OllamaConfig FromConfiguration(IConfiguration configuration)
        => LoadFromConfiguration<OllamaConfig>(configuration);
}
