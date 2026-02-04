namespace Hazina.App.HazinaCoder.Core.Configuration;

/// <summary>
/// Validates configuration settings and provides detailed error messages.
/// Improvement #11: Configuration validation
/// </summary>
public class ConfigurationValidator
{
    private readonly HazinaCoderConfiguration _config;
    private readonly List<ValidationError> _errors = new();
    private readonly List<ValidationWarning> _warnings = new();

    public ConfigurationValidator(HazinaCoderConfiguration config)
    {
        _config = config;
    }

    /// <summary>
    /// Validates all configuration sections
    /// </summary>
    public ValidationResult Validate()
    {
        ValidateProviderSettings();
        ValidatePerformanceSettings();
        ValidateLoggingSettings();
        ValidateFeatureFlags();
        ValidateMultiTenantSettings();
        ValidateABTestingSettings();

        return new ValidationResult
        {
            IsValid = !_errors.Any(),
            Errors = _errors,
            Warnings = _warnings
        };
    }

    private void ValidateProviderSettings()
    {
        if (string.IsNullOrWhiteSpace(_config.Provider.DefaultProvider))
        {
            AddError("Provider.DefaultProvider", "Default provider is required");
        }

        if (_config.Provider.TimeoutSeconds <= 0)
        {
            AddError("Provider.TimeoutSeconds", "Timeout must be positive");
        }
        else if (_config.Provider.TimeoutSeconds < 10)
        {
            AddWarning("Provider.TimeoutSeconds", "Timeout is very short, may cause frequent failures");
        }

        if (_config.Provider.MaxRetries < 0)
        {
            AddError("Provider.MaxRetries", "MaxRetries cannot be negative");
        }

        foreach (var kvp in _config.Provider.Providers)
        {
            ValidateProviderConfig(kvp.Key, kvp.Value);
        }
    }

    private void ValidateProviderConfig(string name, ProviderConfig config)
    {
        if (config.Enabled && string.IsNullOrWhiteSpace(config.ApiKey))
        {
            AddWarning($"Provider.Providers.{name}.ApiKey", "API key is missing for enabled provider");
        }

        if (config.MaxTokens <= 0)
        {
            AddError($"Provider.Providers.{name}.MaxTokens", "MaxTokens must be positive");
        }

        if (config.Temperature < 0 || config.Temperature > 2)
        {
            AddError($"Provider.Providers.{name}.Temperature", "Temperature must be between 0 and 2");
        }
    }

    private void ValidatePerformanceSettings()
    {
        if (_config.Performance.MaxConcurrentTasks <= 0)
        {
            AddError("Performance.MaxConcurrentTasks", "MaxConcurrentTasks must be positive");
        }
        else if (_config.Performance.MaxConcurrentTasks > 16)
        {
            AddWarning("Performance.MaxConcurrentTasks", "Very high concurrency may cause resource exhaustion");
        }

        if (_config.Performance.MaxTurnsPerConversation <= 0)
        {
            AddError("Performance.MaxTurnsPerConversation", "MaxTurnsPerConversation must be positive");
        }

        if (_config.Performance.CacheExpirationMinutes <= 0)
        {
            AddError("Performance.CacheExpirationMinutes", "CacheExpirationMinutes must be positive");
        }

        if (_config.Performance.ThreadPoolMinThreads > _config.Performance.ThreadPoolMaxThreads)
        {
            AddError("Performance.ThreadPool", "MinThreads cannot be greater than MaxThreads");
        }

        if (_config.Performance.MaxMemoryUsageMB <= 0)
        {
            AddError("Performance.MaxMemoryUsageMB", "MaxMemoryUsageMB must be positive");
        }
        else if (_config.Performance.MaxMemoryUsageMB < 512)
        {
            AddWarning("Performance.MaxMemoryUsageMB", "Memory limit is very low, may cause OOM errors");
        }
    }

    private void ValidateLoggingSettings()
    {
        var validLevels = new[] { "Trace", "Debug", "Information", "Warning", "Error", "Critical" };
        if (!validLevels.Contains(_config.Logging.MinimumLevel))
        {
            AddError("Logging.MinimumLevel", $"Invalid log level. Must be one of: {string.Join(", ", validLevels)}");
        }

        if (_config.Logging.MaxLogFileSizeMB <= 0)
        {
            AddError("Logging.MaxLogFileSizeMB", "MaxLogFileSizeMB must be positive");
        }

        if (_config.Logging.MaxLogFileCount <= 0)
        {
            AddError("Logging.MaxLogFileCount", "MaxLogFileCount must be positive");
        }

        if (string.IsNullOrWhiteSpace(_config.Logging.LogDirectory))
        {
            AddError("Logging.LogDirectory", "LogDirectory is required");
        }
    }

    private void ValidateFeatureFlags()
    {
        // No strict validation needed for feature flags, but can add consistency checks
        if (!_config.Features.EnableLearningSystem && _config.Features.EnableReflectionLog)
        {
            AddWarning("Features.EnableReflectionLog", "Reflection log enabled but learning system is disabled");
        }
    }

    private void ValidateMultiTenantSettings()
    {
        if (_config.MultiTenant.Enabled)
        {
            if (string.IsNullOrWhiteSpace(_config.MultiTenant.TenantIdHeader))
            {
                AddError("MultiTenant.TenantIdHeader", "TenantIdHeader is required when multi-tenancy is enabled");
            }

            if (!_config.MultiTenant.Tenants.Any())
            {
                AddWarning("MultiTenant.Tenants", "No tenants configured");
            }

            foreach (var kvp in _config.MultiTenant.Tenants)
            {
                if (string.IsNullOrWhiteSpace(kvp.Value.Name))
                {
                    AddError($"MultiTenant.Tenants.{kvp.Key}.Name", "Tenant name is required");
                }
            }
        }
    }

    private void ValidateABTestingSettings()
    {
        if (_config.ABTesting.Enabled)
        {
            if (!_config.ABTesting.Experiments.Any())
            {
                AddWarning("ABTesting.Experiments", "No experiments configured");
            }

            foreach (var kvp in _config.ABTesting.Experiments)
            {
                var exp = kvp.Value;
                if (exp.Enabled)
                {
                    if (exp.TrafficPercentage < 0 || exp.TrafficPercentage > 100)
                    {
                        AddError($"ABTesting.Experiments.{kvp.Key}.TrafficPercentage", "Traffic percentage must be between 0 and 100");
                    }

                    if (!exp.Variants.Any())
                    {
                        AddError($"ABTesting.Experiments.{kvp.Key}.Variants", "At least one variant is required");
                    }
                }
            }
        }
    }

    private void AddError(string field, string message)
    {
        _errors.Add(new ValidationError { Field = field, Message = message });
    }

    private void AddWarning(string field, string message)
    {
        _warnings.Add(new ValidationWarning { Field = field, Message = message });
    }
}

public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<ValidationError> Errors { get; set; } = new();
    public List<ValidationWarning> Warnings { get; set; } = new();

    public void PrintResults()
    {
        if (Errors.Any())
        {
            Console.WriteLine("Configuration Validation Errors:");
            foreach (var error in Errors)
            {
                Console.WriteLine($"  [ERROR] {error.Field}: {error.Message}");
            }
        }

        if (Warnings.Any())
        {
            Console.WriteLine("Configuration Validation Warnings:");
            foreach (var warning in Warnings)
            {
                Console.WriteLine($"  [WARN] {warning.Field}: {warning.Message}");
            }
        }

        if (IsValid && !Warnings.Any())
        {
            Console.WriteLine("Configuration validation passed successfully.");
        }
    }
}

public class ValidationError
{
    public string Field { get; set; } = "";
    public string Message { get; set; } = "";
}

public class ValidationWarning
{
    public string Field { get; set; } = "";
    public string Message { get; set; } = "";
}
