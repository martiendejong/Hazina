using Microsoft.Extensions.Logging;

namespace Hazina.Core.Plugins.Abstractions;

/// <summary>
/// Execution context provided to plugins during execution.
/// Contains services, data, and utilities needed by plugins.
/// </summary>
public class PluginContext
{
    /// <summary>
    /// Service provider for dependency injection
    /// </summary>
    public required IServiceProvider Services { get; init; }

    /// <summary>
    /// Logger for plugin diagnostics
    /// </summary>
    public required ILogger Logger { get; init; }

    /// <summary>
    /// Input parameters for this plugin execution
    /// </summary>
    public Dictionary<string, object> Parameters { get; init; } = new();

    /// <summary>
    /// Optional user context (user ID, tenant ID, etc.)
    /// </summary>
    public Dictionary<string, string> UserContext { get; init; } = new();

    /// <summary>
    /// Helper method to get a service from the service provider
    /// </summary>
    /// <typeparam name="T">Service type</typeparam>
    /// <returns>Service instance</returns>
    public T GetService<T>() where T : notnull
    {
        var service = Services.GetService(typeof(T));
        if (service == null)
        {
            throw new InvalidOperationException($"Service of type {typeof(T).Name} not registered");
        }
        return (T)service;
    }

    /// <summary>
    /// Helper method to try get a service from the service provider
    /// </summary>
    /// <typeparam name="T">Service type</typeparam>
    /// <returns>Service instance or null</returns>
    public T? TryGetService<T>() where T : class
    {
        return Services.GetService(typeof(T)) as T;
    }

    /// <summary>
    /// Get a parameter value or throw if not found
    /// </summary>
    public T GetParameter<T>(string key)
    {
        if (!Parameters.TryGetValue(key, out var value))
        {
            throw new ArgumentException($"Parameter '{key}' not found", nameof(key));
        }

        if (value is T typedValue)
        {
            return typedValue;
        }

        throw new ArgumentException($"Parameter '{key}' is not of type {typeof(T).Name}", nameof(key));
    }

    /// <summary>
    /// Try to get a parameter value
    /// </summary>
    public bool TryGetParameter<T>(string key, out T? value)
    {
        if (Parameters.TryGetValue(key, out var objValue) && objValue is T typedValue)
        {
            value = typedValue;
            return true;
        }

        value = default;
        return false;
    }
}
