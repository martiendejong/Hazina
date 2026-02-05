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
        var apiKey = ResolveApiKey(_config.Provider.OpenAI?.ApiKey);
        if (string.IsNullOrEmpty(apiKey))
            throw new InvalidOperationException("OpenAI API key not configured");

        return new OpenAIClientWrapper(apiKey);
    }

    /// <summary>
    /// Create Anthropic client
    /// </summary>
    public ILLMClient CreateAnthropic()
    {
        var apiKey = ResolveApiKey(_config.Provider.Anthropic?.ApiKey);
        if (string.IsNullOrEmpty(apiKey))
            throw new InvalidOperationException("Anthropic API key not configured");

        return new AnthropicClientWrapper(apiKey);
    }

    /// <summary>
    /// Create Ollama client
    /// </summary>
    public ILLMClient CreateOllama()
    {
        var endpoint = _config.Provider.Ollama?.Endpoint ?? "http://localhost:11434";
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

        if (!string.IsNullOrEmpty(ResolveApiKey(_config.Provider.OpenAI?.ApiKey)))
            providers.Add("openai");

        if (!string.IsNullOrEmpty(ResolveApiKey(_config.Provider.Anthropic?.ApiKey)))
            providers.Add("anthropic");

        if (!string.IsNullOrEmpty(_config.Provider.Ollama?.Endpoint))
            providers.Add("ollama");

        return providers;
    }
}
