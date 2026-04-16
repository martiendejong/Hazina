namespace Hazina.Core.Plugins.Lua.Abstractions;

/// <summary>
/// Configuration options for the Lua scripting layer.
/// </summary>
public class LuaScriptingOptions
{
    /// <summary>
    /// Directory that is scanned for *.lua agent scripts.
    /// Relative paths are resolved against the application's content root.
    /// Defaults to "lua-scripts".
    /// </summary>
    public string ScriptsDirectory { get; set; } = "lua-scripts";

    /// <summary>
    /// When true, a <see cref="LuaHotReloadService"/> watches the scripts
    /// directory and reloads changed scripts without restarting the host.
    /// Defaults to true.
    /// </summary>
    public bool HotReloadEnabled { get; set; } = true;

    /// <summary>
    /// Maximum execution time allowed for a single Lua script call.
    /// Defaults to 30 seconds.
    /// </summary>
    public TimeSpan ExecutionTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
