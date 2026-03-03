using Hazina.LLMs;
using Hazina.LLMs.OpenAI;
using Hazina.LLMs.Anthropic;
using Hazina.App.HazinaCoder.Core.Configuration;

namespace Hazina.App.HazinaCoder.Core.Providers.Implementations;

/// <summary>
/// Factory for creating LLM provider instances
/// Iteration 3: Wire providers to actual LLM clients
/// </summary>
public class ProviderFactory
{
    private readonly HazinaCoderConfiguration _config;

    public ProviderFactory(HazinaCoderConfiguration config)
    {
        _config = config;
    }

    /// <summary>
    /// Create OpenAI client
    /// </summary>
    public ILLMClient CreateOpenAI()
    {
        var providerConfig = _config.Provider.Providers.GetValueOrDefault("openai");
        var apiKey = ResolveApiKey(providerConfig?.ApiKey);
        if (string.IsNullOrEmpty(apiKey))
            throw new InvalidOperationException("OpenAI API key not configured");

        var openAIConfig = new OpenAIConfig(apiKey);
        if (providerConfig?.DefaultModel != null)
            openAIConfig.Model = providerConfig.DefaultModel;

        return new OpenAIClientWrapper(openAIConfig);
    }

    /// <summary>
    /// Create Anthropic client
    /// </summary>
    public ILLMClient CreateAnthropic()
    {
        var providerConfig = _config.Provider.Providers.GetValueOrDefault("anthropic");
        var apiKey = ResolveApiKey(providerConfig?.ApiKey);
        if (string.IsNullOrEmpty(apiKey))
            throw new InvalidOperationException("Anthropic API key not configured");

        // TODO: Update when AnthropicClientWrapper constructor signature is available
        throw new NotImplementedException("AnthropicClientWrapper needs config parameter");
    }

    /// <summary>
    /// Create Ollama client
    /// </summary>
    public ILLMClient CreateOllama()
    {
        var providerConfig = _config.Provider.Providers.GetValueOrDefault("ollama");
        var endpoint = providerConfig?.Endpoint ?? "http://localhost:11434";
        // Ollama doesn't require API key
        throw new NotImplementedException("Ollama provider not yet implemented");
    }

    /// <summary>
    /// Create client based on provider name
    /// </summary>
    public ILLMClient CreateProvider(string providerName)
    {
        return providerName.ToLowerInvariant() switch
        {
            "openai" => CreateOpenAI(),
            "anthropic" or "claude" => CreateAnthropic(),
            "ollama" => CreateOllama(),
            _ => throw new ArgumentException($"Unknown provider: {providerName}", nameof(providerName))
        };
    }

    /// <summary>
    /// Resolve API key from config (supports ENV: prefix)
    /// </summary>
    private string? ResolveApiKey(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return null;

        if (value.StartsWith("ENV:"))
        {
            var envVar = value.Substring(4);
            return Environment.GetEnvironmentVariable(envVar);
        }

        return value;
    }

    /// <summary>
    /// Get available providers
    /// </summary>
    public List<string> GetAvailableProviders()
    {
        var providers = new List<string>();

        var openAIConfig = _config.Provider.Providers.GetValueOrDefault("openai");
        if (!string.IsNullOrEmpty(ResolveApiKey(openAIConfig?.ApiKey)))
            providers.Add("openai");

        var anthropicConfig = _config.Provider.Providers.GetValueOrDefault("anthropic");
        if (!string.IsNullOrEmpty(ResolveApiKey(anthropicConfig?.ApiKey)))
            providers.Add("anthropic");

        var ollamaConfig = _config.Provider.Providers.GetValueOrDefault("ollama");
        if (!string.IsNullOrEmpty(ollamaConfig?.Endpoint))
            providers.Add("ollama");

        return providers;
    }
}
