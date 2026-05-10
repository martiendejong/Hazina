using Hazina.Core.Plugins.Lua.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hazina.Core.Plugins.Lua.HotReload;

/// <summary>
/// Background service that watches the Lua scripts directory for changes
/// and hot-reloads modified scripts via <see cref="LuaPluginRegistry"/>
/// without restarting the host.
/// </summary>
public class LuaHotReloadService : IHostedService, IDisposable
{
    private readonly LuaPluginRegistry _registry;
    private readonly LuaScriptingOptions _options;
    private readonly ILogger<LuaHotReloadService> _logger;

    private FileSystemWatcher? _watcher;

    public LuaHotReloadService(
        LuaPluginRegistry registry,
        IOptions<LuaScriptingOptions> options,
        ILogger<LuaHotReloadService> logger)
    {
        _registry = registry;
        _options  = options.Value;
        _logger   = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.HotReloadEnabled)
        {
            _logger.LogDebug("Lua hot-reload is disabled via options");
            return Task.CompletedTask;
        }

        var dir = ResolveDirectory();
        if (!Directory.Exists(dir))
        {
            _logger.LogWarning("Lua scripts directory '{Dir}' does not exist — hot-reload inactive", dir);
            return Task.CompletedTask;
        }

        _watcher = new FileSystemWatcher(dir, "*.lua")
        {
            IncludeSubdirectories = true,
            NotifyFilter          = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime,
            EnableRaisingEvents   = true
        };

        _watcher.Changed += OnChanged;
        _watcher.Created += OnChanged;
        _watcher.Deleted += OnDeleted;
        _watcher.Renamed += OnRenamed;

        _logger.LogInformation("Lua hot-reload watching: {Dir}", dir);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _watcher?.Dispose();
        return Task.CompletedTask;
    }

    public void Dispose() => _watcher?.Dispose();

    // ------------------------------------------------------------------
    // Handlers
    // ------------------------------------------------------------------

    private void OnChanged(object _, FileSystemEventArgs e)
    {
        _logger.LogDebug("Lua file changed: {Path}", e.FullPath);
        _registry.ReloadFile(e.FullPath);
    }

    private void OnDeleted(object _, FileSystemEventArgs e)
    {
        _logger.LogDebug("Lua file deleted: {Path}", e.FullPath);
        _registry.RemoveByFilePath(e.FullPath);
    }

    private void OnRenamed(object _, RenamedEventArgs e)
    {
        _logger.LogDebug("Lua file renamed: {Old} → {New}", e.OldFullPath, e.FullPath);
        _registry.RemoveByFilePath(e.OldFullPath);

        if (e.FullPath.EndsWith(".lua", StringComparison.OrdinalIgnoreCase))
            _registry.ReloadFile(e.FullPath);
    }

    // ------------------------------------------------------------------

    private string ResolveDirectory()
    {
        var dir = _options.ScriptsDirectory;
        if (!Path.IsPathRooted(dir))
            dir = Path.Combine(AppContext.BaseDirectory, dir);
        return dir;
    }
}
