using System.Reflection;
using System.Runtime.Loader;
using Hazina.Core.Plugins.Abstractions;

namespace Hazina.Core.Plugins.Compilation;

/// <summary>
/// Represents a compiled plugin ready for execution
/// </summary>
public class CompiledPlugin : IDisposable
{
    /// <summary>
    /// The compiled assembly
    /// </summary>
    public required Assembly Assembly { get; init; }

    /// <summary>
    /// Assembly load context (for unloading)
    /// </summary>
    public required AssemblyLoadContext LoadContext { get; init; }

    /// <summary>
    /// Original metadata
    /// </summary>
    public required PluginMetadata Metadata { get; init; }

    /// <summary>
    /// When the plugin was compiled
    /// </summary>
    public DateTime CompiledAt { get; init; }

    /// <summary>
    /// Create an instance of the plugin
    /// </summary>
    public IHazinaPlugin CreateInstance()
    {
        var pluginType = Assembly.GetTypes()
            .FirstOrDefault(t => typeof(IHazinaPlugin).IsAssignableFrom(t) && !t.IsInterface);

        if (pluginType == null)
        {
            throw new InvalidOperationException("No IHazinaPlugin implementation found in assembly");
        }

        var instance = Activator.CreateInstance(pluginType);
        if (instance is not IHazinaPlugin plugin)
        {
            throw new InvalidOperationException($"Failed to create instance of {pluginType.Name}");
        }

        return plugin;
    }

    /// <summary>
    /// Unload the plugin assembly
    /// </summary>
    public void Dispose()
    {
        LoadContext.Unload();
        GC.SuppressFinalize(this);
    }
}
