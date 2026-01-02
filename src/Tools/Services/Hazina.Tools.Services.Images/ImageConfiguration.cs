namespace Hazina.Tools.Services.Images;

/// <summary>
/// Configuration model for Hazina Images, bindable from appsettings.json.
///
/// Example configuration:
/// <code>
/// {
///   "Hazina": {
///     "Images": {
///       "DefaultProvider": "Local",
///       "EnableLocalProvider": true,
///       "EnableAiProvider": false,
///       "AI": {
///         "Provider": "OpenAI",
///         "Model": "dall-e-3"
///       }
///     }
///   }
/// }
/// </code>
/// </summary>
public class ImageConfiguration
{
    /// <summary>
    /// Configuration section path.
    /// </summary>
    public const string SectionPath = "Hazina:Images";

    /// <summary>
    /// The default provider to use when not specified in options.
    /// </summary>
    public string DefaultProvider { get; set; } = "Local";

    /// <summary>
    /// Whether to enable the local (ImageSharp) provider.
    /// </summary>
    public bool EnableLocalProvider { get; set; } = true;

    /// <summary>
    /// Whether to enable the AI provider.
    /// </summary>
    public bool EnableAiProvider { get; set; } = false;

    /// <summary>
    /// AI provider configuration.
    /// </summary>
    public AiProviderConfiguration AI { get; set; } = new();
}

/// <summary>
/// Configuration for AI-based image providers.
/// </summary>
public class AiProviderConfiguration
{
    /// <summary>
    /// The AI provider name (e.g., "OpenAI", "StabilityAI").
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// The model to use for image generation/editing.
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// API key for the provider (can also be set via environment variable).
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Base URL for the API (optional, for custom endpoints).
    /// </summary>
    public string? BaseUrl { get; set; }
}
