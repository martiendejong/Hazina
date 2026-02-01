namespace Hazina.Core.Plugins.Abstractions;

/// <summary>
/// Base interface for all Hazina dynamic plugins.
/// Plugins are C# code compiled and executed at runtime.
/// </summary>
public interface IHazinaPlugin
{
    /// <summary>
    /// Unique name identifying this plugin
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Semantic version of the plugin
    /// </summary>
    string Version { get; }

    /// <summary>
    /// Human-readable description of what this plugin does
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Execute the plugin logic with the provided context
    /// </summary>
    /// <param name="context">Execution context containing services, data, and logger</param>
    /// <returns>Result of plugin execution</returns>
    Task<PluginResult> ExecuteAsync(PluginContext context);
}
