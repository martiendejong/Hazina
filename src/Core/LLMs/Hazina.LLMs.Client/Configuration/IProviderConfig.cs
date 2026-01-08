namespace Hazina.LLMs.Configuration;

/// <summary>
/// Common interface for all LLM provider configurations.
/// Enables generic factory patterns and reduces type-checking logic.
/// </summary>
public interface IProviderConfig
{
    /// <summary>
    /// API key for authentication with the provider.
    /// </summary>
    string ApiKey { get; set; }

    /// <summary>
    /// Model identifier (e.g., "gpt-4o", "claude-3-5-sonnet", "gemini-1.5-pro").
    /// </summary>
    string Model { get; set; }

    /// <summary>
    /// Base endpoint URL for the provider API.
    /// </summary>
    string? Endpoint { get; set; }

    /// <summary>
    /// Path for logging API interactions (optional).
    /// </summary>
    string? LogPath { get; set; }

    /// <summary>
    /// Validates the configuration and returns any validation errors.
    /// </summary>
    IEnumerable<string> Validate();
}
