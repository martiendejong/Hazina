using Hazina.Core.Plugins.Lua.Abstractions;
using Hazina.Core.Plugins.Lua.HotReload;
using Hazina.Core.Plugins.Lua.Loader;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hazina.Core.Plugins.Lua.DI;

/// <summary>
/// Extension methods for registering the Lua scripting layer into the DI container.
///
/// Usage in Program.cs / Startup.cs:
///
///   builder.Services.AddLuaScripting(options =>
///   {
///       options.ScriptsDirectory = "lua-scripts";
///       options.HotReloadEnabled = true;
///       options.ExecutionTimeout = TimeSpan.FromSeconds(30);
///   });
///
/// After registration, resolve <see cref="LuaPluginRegistry"/> to look up and run plugins,
/// or resolve <see cref="LuaScriptLoader"/> to load scripts manually.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add Lua scripting support to the service collection.
    /// Registers: <see cref="LuaScriptLoader"/>, <see cref="LuaPluginRegistry"/>,
    /// and optionally <see cref="LuaHotReloadService"/> as a hosted background service.
    /// </summary>
    public static IServiceCollection AddLuaScripting(
        this IServiceCollection services,
        Action<LuaScriptingOptions>? configure = null)
    {
        // Register and configure options
        var optionsBuilder = services.AddOptions<LuaScriptingOptions>();
        if (configure != null)
            optionsBuilder.Configure(configure);

        // Core services
        services.AddSingleton<LuaScriptLoader>();

        services.AddSingleton<LuaPluginRegistry>(sp =>
        {
            var opts    = sp.GetRequiredService<IOptions<LuaScriptingOptions>>().Value;
            var loader  = sp.GetRequiredService<LuaScriptLoader>();
            var logger  = sp.GetRequiredService<ILogger<LuaPluginRegistry>>();

            var registry = new LuaPluginRegistry(loader, logger, opts.ExecutionTimeout);

            // Eagerly load all scripts from the configured directory
            var dir = opts.ScriptsDirectory;
            if (!Path.IsPathRooted(dir))
                dir = Path.Combine(AppContext.BaseDirectory, dir);

            var scripts = loader.LoadDirectory(dir);
            registry.RegisterAll(scripts);

            return registry;
        });

        // Hot-reload background service (only registered if enabled in options)
        services.AddHostedService<LuaHotReloadService>();

        return services;
    }
}
