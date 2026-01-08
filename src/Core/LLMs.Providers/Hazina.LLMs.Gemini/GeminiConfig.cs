using Hazina.LLMs.Configuration;

namespace Hazina.LLMs.Gemini;

/// <summary>
/// Configuration for Google Gemini API.
/// </summary>
public class GeminiConfig : HazinaConfigBase
{
    /// <summary>
    /// Model for image generation.
    /// </summary>
    public string ImageModel { get; set; } = "imagegeneration";

    /// <summary>
    /// API key for Text-to-Speech (defaults to main ApiKey if not set).
    /// </summary>
    public string? TtsApiKey { get; set; }

    /// <summary>
    /// Language code for TTS (e.g., "en-US").
    /// </summary>
    public string TtsLanguageCode { get; set; } = "en-US";

    /// <summary>
    /// Voice name for TTS (e.g., "en-US-Neural2-C").
    /// </summary>
    public string TtsVoiceName { get; set; } = "en-US-Neural2-C";

    /// <summary>
    /// Audio encoding for TTS output (e.g., "MP3").
    /// </summary>
    public string TtsAudioEncoding { get; set; } = "MP3";

    /// <inheritdoc />
    protected override string ConfigurationSectionName => "Gemini";

    /// <inheritdoc />
    protected override string? DefaultEndpoint => "https://generativelanguage.googleapis.com/v1beta";

    /// <inheritdoc />
    protected override string DefaultModel => "gemini-1.5-pro";

    /// <inheritdoc />
    protected override void ApplyDefaults()
    {
        base.ApplyDefaults();
        TtsApiKey ??= ApiKey;
    }

    /// <summary>
    /// Loads configuration from appsettings.json.
    /// </summary>
    public static GeminiConfig Load() => LoadFromConfiguration<GeminiConfig>();
}
