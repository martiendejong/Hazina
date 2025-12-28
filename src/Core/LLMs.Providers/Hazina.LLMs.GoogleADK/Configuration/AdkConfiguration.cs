using Microsoft.Extensions.Configuration;

namespace Hazina.LLMs.GoogleADK.Configuration;

/// <summary>
/// Configuration for Google ADK agents
/// </summary>
public class AdkConfiguration
{
    /// <summary>
    /// Default model to use for LLM agents
    /// </summary>
    public string DefaultModel { get; set; } = "gemini-1.5-pro";

    /// <summary>
    /// Gemini API key
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gemini API endpoint
    /// </summary>
    public string Endpoint { get; set; } = "https://generativelanguage.googleapis.com/v1beta";

    /// <summary>
    /// Maximum conversation history size
    /// </summary>
    public int MaxHistorySize { get; set; } = 50;

    /// <summary>
    /// Enable streaming by default
    /// </summary>
    public bool EnableStreaming { get; set; } = false;

    /// <summary>
    /// Default system instructions for agents
    /// </summary>
    public string DefaultSystemInstructions { get; set; } = string.Empty;

    /// <summary>
    /// Session timeout in minutes
    /// </summary>
    public int SessionTimeoutMinutes { get; set; } = 30;

    /// <summary>
    /// Enable detailed logging
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = false;

    /// <summary>
    /// Load configuration from appsettings.json
    /// </summary>
    public static AdkConfiguration Load(string? configPath = null)
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(configPath ?? AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}.json", optional: true);

        var configuration = builder.Build();
        var config = new AdkConfiguration();
        configuration.GetSection("GoogleADK").Bind(config);

        return config;
    }

    /// <summary>
    /// Validate configuration
    /// </summary>
    public bool Validate(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            errors.Add("ApiKey is required");
        }

        if (string.IsNullOrWhiteSpace(Endpoint))
        {
            errors.Add("Endpoint is required");
        }

        if (MaxHistorySize < 1)
        {
            errors.Add("MaxHistorySize must be greater than 0");
        }

        if (SessionTimeoutMinutes < 1)
        {
            errors.Add("SessionTimeoutMinutes must be greater than 0");
        }

        return errors.Count == 0;
    }
}
