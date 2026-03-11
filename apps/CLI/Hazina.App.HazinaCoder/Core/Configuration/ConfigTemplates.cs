using System.Text.Json;

namespace Hazina.App.HazinaCoder.Core.Configuration;

/// <summary>
/// Provides default configuration templates for various scenarios.
/// Improvement #12: Default templates
/// Improvement #13: Config UI generator (template data)
/// </summary>
public static class ConfigTemplates
{
    /// <summary>
    /// Gets a basic development configuration
    /// </summary>
    public static HazinaCoderConfiguration GetDevelopmentTemplate()
    {
        return new HazinaCoderConfiguration
        {
            Provider = new ProviderSettings
            {
                DefaultProvider = "anthropic",
                TimeoutSeconds = 120,
                MaxRetries = 3,
                Providers = new Dictionary<string, ProviderConfig>
                {
                    ["anthropic"] = new ProviderConfig
                    {
                        DefaultModel = "claude-sonnet-4-5-20250929",
                        MaxTokens = 8192,
                        Temperature = 0.7,
                        Enabled = true
                    },
                    ["openai"] = new ProviderConfig
                    {
                        DefaultModel = "gpt-4-turbo",
                        MaxTokens = 4096,
                        Temperature = 0.7,
                        Enabled = true
                    }
                }
            },
            Features = new FeatureFlags
            {
                EnableLearningSystem = true,
                EnableMcpServers = true,
                EnableGitIntegration = true,
                EnableVerboseLogging = true,
                EnableHotReload = true
            },
            Performance = new PerformanceSettings
            {
                MaxConcurrentTasks = 4,
                MaxTurnsPerConversation = 50,
                CacheExpirationMinutes = 60
            },
            Logging = new LoggingConfiguration
            {
                MinimumLevel = "Debug",
                EnableStructuredLogging = true,
                EnableFileLogging = true
            },
            Deployment = new DeploymentSettings
            {
                Environment = "Development"
            }
        };
    }

    /// <summary>
    /// Gets a production-optimized configuration
    /// </summary>
    public static HazinaCoderConfiguration GetProductionTemplate()
    {
        return new HazinaCoderConfiguration
        {
            Provider = new ProviderSettings
            {
                DefaultProvider = "auto",
                TimeoutSeconds = 180,
                MaxRetries = 5
            },
            Features = new FeatureFlags
            {
                EnableLearningSystem = true,
                EnableMcpServers = true,
                EnableGitIntegration = true,
                EnableVerboseLogging = false,
                EnableHotReload = false,
                EnableMetrics = true,
                EnableCaching = true
            },
            Performance = new PerformanceSettings
            {
                MaxConcurrentTasks = 8,
                MaxTurnsPerConversation = 100,
                CacheExpirationMinutes = 120,
                EnableParallelProcessing = true
            },
            Logging = new LoggingConfiguration
            {
                MinimumLevel = "Information",
                EnableStructuredLogging = true,
                EnableFileLogging = true,
                EnablePerformanceLogging = true
            },
            Deployment = new DeploymentSettings
            {
                Environment = "Production"
            }
        };
    }

    /// <summary>
    /// Exports a configuration to JSON file
    /// </summary>
    public static async Task ExportToFileAsync(HazinaCoderConfiguration config, string filePath)
    {
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await File.WriteAllTextAsync(filePath, json);
    }

    /// <summary>
    /// Generates an interactive configuration UI prompt
    /// </summary>
    public static string GenerateConfigUI()
    {
        return @"
╔════════════════════════════════════════════════════════════╗
║          HazinaCoder Configuration Generator               ║
╚════════════════════════════════════════════════════════════╝

Select a configuration template:
  1. Development (verbose logging, hot-reload enabled)
  2. Production (optimized performance, reduced logging)
  3. Custom (interactive configuration)

Enter your choice (1-3): ";
    }
}
