using Hazina.LLMs.Configuration;

namespace Hazina.LLMs.Anthropic;

/// <summary>
/// Configuration for Anthropic Claude API.
/// </summary>
public class AnthropicConfig : HazinaConfigBase
{
    /// <summary>
    /// API version for Anthropic (e.g., "2023-06-01").
    /// </summary>
    public string ApiVersion { get; set; } = "2023-06-01";

    /// <inheritdoc />
    protected override string ConfigurationSectionName => "Anthropic";

    /// <inheritdoc />
    protected override string? DefaultEndpoint => "https://api.anthropic.com";

    /// <inheritdoc />
    protected override string DefaultModel => "claude-3-5-sonnet-latest";

    /// <summary>
    /// Loads configuration from appsettings.json.
    /// </summary>
    public static AnthropicConfig Load() => LoadFromConfiguration<AnthropicConfig>();
}
