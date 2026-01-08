using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Hazina.Tools.Core.Services;

/// <summary>
/// Abstract base class for all Hazina services.
/// Provides common functionality for configuration, logging, and error handling.
/// Reduces boilerplate code across 30+ service implementations.
/// </summary>
/// <typeparam name="TService">The concrete service type (for typed logging).</typeparam>
public abstract class HazinaServiceBase<TService> where TService : class
{
    /// <summary>
    /// Application configuration.
    /// </summary>
    protected IConfiguration Configuration { get; }

    /// <summary>
    /// Typed logger for this service.
    /// </summary>
    protected ILogger<TService> Logger { get; }

    /// <summary>
    /// Gets the service name for logging purposes.
    /// </summary>
    protected virtual string ServiceName => typeof(TService).Name;

    /// <summary>
    /// Creates a new service instance with configuration and logging.
    /// </summary>
    protected HazinaServiceBase(IConfiguration configuration, ILogger<TService> logger)
    {
        Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Creates a new service instance with configuration only (logger is optional).
    /// </summary>
    protected HazinaServiceBase(IConfiguration configuration)
    {
        Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        Logger = null!;
    }

    /// <summary>
    /// Gets a configuration value from the specified section and key.
    /// </summary>
    protected string? GetConfigValue(string section, string key)
    {
        return Configuration.GetSection(section).GetValue<string>(key);
    }

    /// <summary>
    /// Gets a required configuration value, throwing if not found.
    /// </summary>
    protected string GetRequiredConfigValue(string section, string key)
    {
        var value = GetConfigValue(section, key);
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Configuration value '{section}:{key}' is required but not found.");
        return value;
    }

    /// <summary>
    /// Logs an information message.
    /// </summary>
    protected void LogInfo(string message, params object[] args)
    {
        Logger?.LogInformation($"[{ServiceName}] {message}", args);
    }

    /// <summary>
    /// Logs a warning message.
    /// </summary>
    protected void LogWarning(string message, params object[] args)
    {
        Logger?.LogWarning($"[{ServiceName}] {message}", args);
    }

    /// <summary>
    /// Logs an error message with exception.
    /// </summary>
    protected void LogError(Exception ex, string message, params object[] args)
    {
        Logger?.LogError(ex, $"[{ServiceName}] {message}", args);
    }

    /// <summary>
    /// Executes an async operation with standardized error handling and logging.
    /// </summary>
    protected async Task<T> ExecuteAsync<T>(string operationName, Func<Task<T>> operation)
    {
        try
        {
            LogInfo("Starting {Operation}", operationName);
            var result = await operation();
            LogInfo("Completed {Operation}", operationName);
            return result;
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed {Operation}: {Error}", operationName, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Executes an async operation with standardized error handling and logging (no return value).
    /// </summary>
    protected async Task ExecuteAsync(string operationName, Func<Task> operation)
    {
        try
        {
            LogInfo("Starting {Operation}", operationName);
            await operation();
            LogInfo("Completed {Operation}", operationName);
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed {Operation}: {Error}", operationName, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Executes an async operation with retry logic.
    /// </summary>
    protected async Task<T> ExecuteWithRetryAsync<T>(
        string operationName,
        Func<Task<T>> operation,
        int maxRetries = 3,
        int delayMilliseconds = 1000)
    {
        Exception? lastException = null;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lastException = ex;
                LogWarning("Attempt {Attempt}/{MaxRetries} failed for {Operation}: {Error}",
                    attempt, maxRetries, operationName, ex.Message);

                if (attempt < maxRetries)
                    await Task.Delay(delayMilliseconds * attempt);
            }
        }

        LogError(lastException!, "All {MaxRetries} attempts failed for {Operation}", maxRetries, operationName);
        throw lastException!;
    }
}

/// <summary>
/// Abstract base class for services that don't need typed logging.
/// </summary>
public abstract class HazinaServiceBase
{
    /// <summary>
    /// Application configuration.
    /// </summary>
    protected IConfiguration Configuration { get; }

    /// <summary>
    /// Creates a new service instance with configuration.
    /// </summary>
    protected HazinaServiceBase(IConfiguration configuration)
    {
        Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <summary>
    /// Gets a configuration value from the specified section and key.
    /// </summary>
    protected string? GetConfigValue(string section, string key)
    {
        return Configuration.GetSection(section).GetValue<string>(key);
    }

    /// <summary>
    /// Gets a required configuration value, throwing if not found.
    /// </summary>
    protected string GetRequiredConfigValue(string section, string key)
    {
        var value = GetConfigValue(section, key);
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Configuration value '{section}:{key}' is required but not found.");
        return value;
    }
}
