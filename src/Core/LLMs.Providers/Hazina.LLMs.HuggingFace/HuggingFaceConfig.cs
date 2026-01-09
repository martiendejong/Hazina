using Hazina.LLMs.Configuration;

namespace Hazina.LLMs.HuggingFace;

/// <summary>
/// Configuration for HuggingFace Inference API.
/// </summary>
public class HuggingFaceConfig : HazinaConfigBase
{
    /// <inheritdoc />
    protected override string ConfigurationSectionName => "HuggingFace";

    /// <inheritdoc />
    protected override string? DefaultEndpoint => "https://api-inference.huggingface.co";

    /// <inheritdoc />
    protected override string DefaultModel => "gpt2";

    /// <summary>
    /// Loads configuration from appsettings.json.
    /// </summary>
    public static HuggingFaceConfig Load() => LoadFromConfiguration<HuggingFaceConfig>();
}
