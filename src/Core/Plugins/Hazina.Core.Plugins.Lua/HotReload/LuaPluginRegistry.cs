using Hazina.Core.Plugins.Abstractions;
using Hazina.Core.Plugins.Lua.Abstractions;
using Hazina.Core.Plugins.Lua.Execution;
using Hazina.Core.Plugins.Lua.Loader;
using Microsoft.Extensions.Logging;

namespace Hazina.Core.Plugins.Lua.HotReload;

/// <summary>
/// In-memory registry of loaded Lua plugins.
/// Thread-safe: uses a ReaderWriterLockSlim so reads never block each other.
/// </summary>
public class LuaPluginRegistry
{
    private readonly LuaScriptLoader _loader;
    private readonly ILogger<LuaPluginRegistry> _logger;
    private readonly TimeSpan _executionTimeout;

    private readonly Dictionary<string, LuaHazinaPlugin> _byName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, LuaHazinaPlugin> _byPath = new(StringComparer.OrdinalIgnoreCase);
    private readonly ReaderWriterLockSlim _lock = new();

    public LuaPluginRegistry(
        LuaScriptLoader loader,
        ILogger<LuaPluginRegistry> logger,
        TimeSpan executionTimeout)
    {
        _loader           = loader;
        _logger           = logger;
        _executionTimeout = executionTimeout;
    }

    // ------------------------------------------------------------------
    // Registration
    // ------------------------------------------------------------------

    public void RegisterFromFile(string filePath)
    {
        var script = _loader.LoadFromFile(filePath);
        Register(script);
    }

    public void RegisterFromSource(string sourceCode, string? filePath = null)
    {
        var script = _loader.LoadFromSource(sourceCode, filePath);
        Register(script);
    }

    public void RegisterAll(IEnumerable<LuaScript> scripts)
    {
        foreach (var s in scripts)
            Register(s);
    }

    private void Register(LuaScript script)
    {
        var plugin = new LuaHazinaPlugin(script, _executionTimeout);

        _lock.EnterWriteLock();
        try
        {
            _byName[script.Name] = plugin;
            if (script.FilePath != null)
                _byPath[script.FilePath] = plugin;

            _logger.LogInformation("Registered Lua plugin '{Name}' v{Version}", script.Name, script.Version);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    // ------------------------------------------------------------------
    // Lookup
    // ------------------------------------------------------------------

    public IHazinaPlugin? GetByName(string name)
    {
        _lock.EnterReadLock();
        try { return _byName.TryGetValue(name, out var p) ? p : null; }
        finally { _lock.ExitReadLock(); }
    }

    public IHazinaPlugin? GetByFilePath(string path)
    {
        _lock.EnterReadLock();
        try { return _byPath.TryGetValue(path, out var p) ? p : null; }
        finally { _lock.ExitReadLock(); }
    }

    public IReadOnlyList<IHazinaPlugin> GetAll()
    {
        _lock.EnterReadLock();
        try { return _byName.Values.ToList(); }
        finally { _lock.ExitReadLock(); }
    }

    // ------------------------------------------------------------------
    // Hot-reload support
    // ------------------------------------------------------------------

    /// <summary>Reload a single file in-place, replacing the existing plugin atomically.</summary>
    public void ReloadFile(string filePath)
    {
        try
        {
            var script = _loader.LoadFromFile(filePath);
            Register(script); // Register overwrites existing entry by name + path
            _logger.LogInformation("Hot-reloaded Lua plugin '{Name}' from {File}", script.Name, filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hot-reload failed for {File} — keeping previous version", filePath);
        }
    }

    /// <summary>Remove a plugin by file path (called when the file is deleted).</summary>
    public void RemoveByFilePath(string filePath)
    {
        _lock.EnterWriteLock();
        try
        {
            if (_byPath.TryGetValue(filePath, out var plugin))
            {
                _byName.Remove(plugin.Name);
                _byPath.Remove(filePath);
                _logger.LogInformation("Removed Lua plugin '{Name}' (file deleted)", plugin.Name);
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }
}
