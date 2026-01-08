using Microsoft.Extensions.Configuration;

namespace Hazina.LLMs.Configuration;

/// <summary>
/// Abstract base class for LLM provider configurations.
/// Provides common functionality for API key, model, endpoint, and logging configuration.
/// Reduces code duplication across 7+ provider config classes (~400 LOC reduction).
/// </summary>
public abstract class HazinaConfigBase : IProviderConfig
{
    /// <inheritdoc />
    public string ApiKey { get; set; } = string.Empty;

    /// <inheritdoc />
    public string Model { get; set; } = string.Empty;

    /// <inheritdoc />
    public string? Endpoint { get; set; }

    /// <inheritdoc />
    public string? LogPath { get; set; }

    /// <summary>
    /// Gets the configuration section name for this provider (e.g., "OpenAI", "Anthropic").
    /// </summary>
    protected abstract string ConfigurationSectionName { get; }

    /// <summary>
    /// Gets the default endpoint URL for this provider.
    /// </summary>
    protected abstract string? DefaultEndpoint { get; }

    /// <summary>
    /// Gets the default model for this provider.
    /// </summary>
    protected abstract string DefaultModel { get; }

    /// <summary>
    /// Validates the configuration and returns validation errors.
    /// Override to add provider-specific validation.
    /// </summary>
    public virtual IEnumerable<string> Validate()
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
            yield return $"{GetType().Name}: ApiKey is required";

        if (string.IsNullOrWhiteSpace(Model))
            yield return $"{GetType().Name}: Model is required";
    }

    /// <summary>
    /// Applies default values for properties that are not set.
    /// Called after binding from configuration.
    /// </summary>
    protected virtual void ApplyDefaults()
    {
        if (string.IsNullOrEmpty(Endpoint) && DefaultEndpoint != null)
            Endpoint = DefaultEndpoint;

        if (string.IsNullOrEmpty(Model))
            Model = DefaultModel;

        // Normalize endpoint (remove trailing slash)
        if (!string.IsNullOrEmpty(Endpoint))
            Endpoint = Endpoint.TrimEnd('/');
    }

    /// <summary>
    /// Loads configuration from appsettings.json using the provider's section name.
    /// </summary>
    protected static T LoadFromConfiguration<T>() where T : HazinaConfigBase, new()
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true)
            .Build();

        return LoadFromConfiguration<T>(config);
    }

    /// <summary>
    /// Loads configuration from an IConfiguration instance.
    /// </summary>
    protected static T LoadFromConfiguration<T>(IConfiguration configuration) where T : HazinaConfigBase, new()
    {
        var instance = new T();
        configuration.GetSection(instance.ConfigurationSectionName).Bind(instance);
        instance.ApplyDefaults();
        return instance;
    }
}
