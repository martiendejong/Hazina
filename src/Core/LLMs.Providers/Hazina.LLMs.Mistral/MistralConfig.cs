using Hazina.LLMs.Configuration;

namespace Hazina.LLMs.Mistral;

/// <summary>
/// Configuration for Mistral AI API.
/// </summary>
public class MistralConfig : HazinaConfigBase
{
    /// <inheritdoc />
    protected override string ConfigurationSectionName => "Mistral";

    /// <inheritdoc />
    protected override string? DefaultEndpoint => "https://api.mistral.ai/v1";

    /// <inheritdoc />
    protected override string DefaultModel => "mistral-large-latest";

    /// <summary>
    /// Loads configuration from appsettings.json.
    /// </summary>
    public static MistralConfig Load() => LoadFromConfiguration<MistralConfig>();
}
