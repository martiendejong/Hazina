using System.Text.Json;

namespace Hazina.App.HazinaCoder.Core.Configuration;

/// <summary>
/// Loads environment-specific configurations with fallback hierarchy.
/// Improvement #8: Environment-specific configs
/// </summary>
public class EnvironmentConfigLoader
{
    private readonly string _baseConfigPath;
    private readonly string _environment;

    public EnvironmentConfigLoader(string baseConfigPath, string? environment = null)
    {
        _baseConfigPath = baseConfigPath;
        _environment = environment ?? Environment.GetEnvironmentVariable("HAZINACODER_ENV") ?? "Development";
    }

    /// <summary>
    /// Loads configuration with environment-specific overrides.
    /// Load order: appsettings.json -> appsettings.{Environment}.json -> Environment variables -> appsettings.Secrets.json
    /// </summary>
    public async Task<HazinaCoderConfiguration> LoadConfigurationAsync()
    {
        var config = new HazinaCoderConfiguration();

        // 1. Load base configuration
        var baseConfig = await LoadConfigFileAsync("appsettings.json");
        if (baseConfig != null)
        {
            config = baseConfig;
        }

        // 2. Load environment-specific configuration
        var envConfig = await LoadConfigFileAsync($"appsettings.{_environment}.json");
        if (envConfig != null)
        {
            MergeConfigurations(config, envConfig);
        }

        // 3. Apply environment variables
        ApplyEnvironmentVariables(config);

        // 4. Load secrets (highest priority)
        var secretsConfig = await LoadConfigFileAsync("appsettings.Secrets.json");
        if (secretsConfig != null)
        {
            MergeConfigurations(config, secretsConfig);
        }

        return config;
    }

    private async Task<HazinaCoderConfiguration?> LoadConfigFileAsync(string fileName)
    {
        var filePath = Path.Combine(_baseConfigPath, fileName);
        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            return JsonSerializer.Deserialize<HazinaCoderConfiguration>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to load {fileName}: {ex.Message}");
            return null;
        }
    }

    private void MergeConfigurations(HazinaCoderConfiguration target, HazinaCoderConfiguration source)
    {
        // Simple merge strategy - can be enhanced with deep merge
        if (source.Provider.DefaultProvider != "auto")
        {
            target.Provider.DefaultProvider = source.Provider.DefaultProvider;
        }

        foreach (var kvp in source.Provider.Providers)
        {
            target.Provider.Providers[kvp.Key] = kvp.Value;
        }

        // Merge feature flags
        target.Features = source.Features;

        // Merge performance settings
        if (source.Performance.MaxConcurrentTasks > 0)
        {
            target.Performance.MaxConcurrentTasks = source.Performance.MaxConcurrentTasks;
        }
    }

    private void ApplyEnvironmentVariables(HazinaCoderConfiguration config)
    {
        // Support environment variable overrides
        var defaultProvider = Environment.GetEnvironmentVariable("HAZINACODER_DEFAULT_PROVIDER");
        if (!string.IsNullOrWhiteSpace(defaultProvider))
        {
            config.Provider.DefaultProvider = defaultProvider;
        }

        var openAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (!string.IsNullOrWhiteSpace(openAiKey))
        {
            if (!config.Provider.Providers.ContainsKey("openai"))
            {
                config.Provider.Providers["openai"] = new ProviderConfig();
            }
            config.Provider.Providers["openai"].ApiKey = openAiKey;
        }

        var anthropicKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        if (!string.IsNullOrWhiteSpace(anthropicKey))
        {
            if (!config.Provider.Providers.ContainsKey("anthropic"))
            {
                config.Provider.Providers["anthropic"] = new ProviderConfig();
            }
            config.Provider.Providers["anthropic"].ApiKey = anthropicKey;
        }
    }
}
