namespace Hazina.App.HazinaCoder.Core.Configuration;

/// <summary>
/// Defines the complete configuration schema for HazinaCoder.
/// Improvement #1: Enhanced appsettings.json schema with validation
/// </summary>
public class HazinaCoderConfiguration
{
    /// <summary>
    /// LLM provider settings
    /// </summary>
    public ProviderSettings Provider { get; set; } = new();

    /// <summary>
    /// Feature flags for conditional functionality
    /// </summary>
    public FeatureFlags Features { get; set; } = new();

    /// <summary>
    /// Performance tuning parameters
    /// </summary>
    public PerformanceSettings Performance { get; set; } = new();

    /// <summary>
    /// Logging configuration
    /// </summary>
    public LoggingConfiguration Logging { get; set; } = new();

    /// <summary>
    /// Deployment environment settings
    /// </summary>
    public DeploymentSettings Deployment { get; set; } = new();

    /// <summary>
    /// Multi-tenant configuration
    /// </summary>
    public MultiTenantSettings MultiTenant { get; set; } = new();

    /// <summary>
    /// A/B testing configuration
    /// </summary>
    public ABTestingSettings ABTesting { get; set; } = new();

    /// <summary>
    /// Validates the configuration and returns validation errors
    /// </summary>
    public List<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Provider.DefaultProvider))
        {
            errors.Add("Provider.DefaultProvider is required");
        }

        if (Performance.MaxConcurrentTasks <= 0)
        {
            errors.Add("Performance.MaxConcurrentTasks must be positive");
        }

        if (Performance.CacheExpirationMinutes <= 0)
        {
            errors.Add("Performance.CacheExpirationMinutes must be positive");
        }

        return errors;
    }
}

/// <summary>
/// LLM provider configuration
/// </summary>
public class ProviderSettings
{
    public string DefaultProvider { get; set; } = "auto";
    public Dictionary<string, ProviderConfig> Providers { get; set; } = new();
    public int TimeoutSeconds { get; set; } = 120;
    public int MaxRetries { get; set; } = 3;
}

/// <summary>
/// Individual provider configuration
/// </summary>
public class ProviderConfig
{
    public string? ApiKey { get; set; }
    public string? Endpoint { get; set; }
    public string? DefaultModel { get; set; }
    public int MaxTokens { get; set; } = 4096;
    public double Temperature { get; set; } = 0.7;
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Feature flags for conditional functionality
/// Improvement #2: Feature flags management
/// </summary>
public class FeatureFlags
{
    public bool EnableLearningSystem { get; set; } = true;
    public bool EnableMcpServers { get; set; } = true;
    public bool EnableGitIntegration { get; set; } = true;
    public bool EnableReflectionLog { get; set; } = true;
    public bool EnableVerboseLogging { get; set; } = false;
    public bool EnableAutoScripts { get; set; } = true;
    public bool EnableHotReload { get; set; } = false;
    public bool EnableMetrics { get; set; } = true;
    public bool EnableCaching { get; set; } = true;
    public bool EnableCompression { get; set; } = false;
}

/// <summary>
/// Performance tuning settings
/// Improvement #3: Performance tuning configs
/// </summary>
public class PerformanceSettings
{
    public int MaxConcurrentTasks { get; set; } = 4;
    public int MaxTurnsPerConversation { get; set; } = 50;
    public int CacheExpirationMinutes { get; set; } = 60;
    public bool EnableParallelProcessing { get; set; } = true;
    public int ThreadPoolMinThreads { get; set; } = 4;
    public int ThreadPoolMaxThreads { get; set; } = 16;
    public long MaxMemoryUsageMB { get; set; } = 2048;
}

/// <summary>
/// Logging configuration
/// Improvement #4: Logging configurations
/// </summary>
public class LoggingConfiguration
{
    public string MinimumLevel { get; set; } = "Information";
    public bool EnableStructuredLogging { get; set; } = true;
    public bool EnableFileLogging { get; set; } = true;
    public string LogDirectory { get; set; } = "logs";
    public int MaxLogFileSizeMB { get; set; } = 100;
    public int MaxLogFileCount { get; set; } = 10;
    public bool EnableConsoleLogging { get; set; } = true;
    public bool EnablePerformanceLogging { get; set; } = false;
}

/// <summary>
/// Deployment environment settings
/// Improvement #5: Deployment configs
/// </summary>
public class DeploymentSettings
{
    public string Environment { get; set; } = "Development";
    public bool IsProduction => Environment.Equals("Production", StringComparison.OrdinalIgnoreCase);
    public bool IsStaging => Environment.Equals("Staging", StringComparison.OrdinalIgnoreCase);
    public bool IsDevelopment => Environment.Equals("Development", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Multi-tenant configuration
/// Improvement #6: Multi-tenant config
/// </summary>
public class MultiTenantSettings
{
    public bool Enabled { get; set; } = false;
    public string TenantIdHeader { get; set; } = "X-Tenant-ID";
    public Dictionary<string, TenantConfig> Tenants { get; set; } = new();
}

/// <summary>
/// Individual tenant configuration
/// </summary>
public class TenantConfig
{
    public string Name { get; set; } = "";
    public string? CustomProvider { get; set; }
    public Dictionary<string, string> Settings { get; set; } = new();
}

/// <summary>
/// A/B testing configuration
/// Improvement #7: A/B test config
/// </summary>
public class ABTestingSettings
{
    public bool Enabled { get; set; } = false;
    public Dictionary<string, ExperimentConfig> Experiments { get; set; } = new();
}

/// <summary>
/// Individual experiment configuration
/// </summary>
public class ExperimentConfig
{
    public string Name { get; set; } = "";
    public bool Enabled { get; set; } = false;
    public double TrafficPercentage { get; set; } = 0.0;
    public Dictionary<string, object> Variants { get; set; } = new();
}
