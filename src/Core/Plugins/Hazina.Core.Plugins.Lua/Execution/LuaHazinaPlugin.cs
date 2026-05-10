using System.Text.Json;
using Hazina.Core.Plugins.Abstractions;
using Hazina.Core.Plugins.Lua.Abstractions;
using Microsoft.Extensions.Logging;
using NLua;

namespace Hazina.Core.Plugins.Lua.Execution;

/// <summary>
/// Adapts a <see cref="LuaScript"/> to the <see cref="IHazinaPlugin"/> interface.
///
/// Each call to <see cref="ExecuteAsync"/> creates a fresh NLua state, registers
/// Hazina globals, executes the script, then calls the Lua function:
///
///   function execute(context)
///     -- context.params  → table of input parameters
///     -- context.user    → table of user-context keys
///     return { success = true, data = "result", message = "ok" }
///   end
///
/// The returned Lua table is mapped to <see cref="PluginResult"/>.
/// If the script does not define <c>execute</c>, a successful empty result is returned.
/// </summary>
public class LuaHazinaPlugin : IHazinaPlugin
{
    private readonly LuaScript _script;
    private readonly TimeSpan _timeout;

    public string Name        => _script.Name;
    public string Version     => _script.Version;
    public string Description => _script.Description;

    public LuaHazinaPlugin(LuaScript script, TimeSpan? timeout = null)
    {
        _script  = script;
        _timeout = timeout ?? TimeSpan.FromSeconds(30);
    }

    public async Task<PluginResult> ExecuteAsync(PluginContext context)
    {
        using var cts = new CancellationTokenSource(_timeout);

        try
        {
            return await Task.Run(() => RunLua(context), cts.Token);
        }
        catch (OperationCanceledException)
        {
            context.Logger.LogError("Lua plugin '{Name}' timed out after {Timeout}", Name, _timeout);
            return PluginResult.Failed($"Lua plugin '{Name}' execution timed out after {_timeout}.");
        }
        catch (Exception ex)
        {
            context.Logger.LogError(ex, "Lua plugin '{Name}' threw an exception", Name);
            return PluginResult.Failed($"Lua plugin '{Name}' failed: {ex.Message}", ex);
        }
    }

    // ------------------------------------------------------------------

    private PluginResult RunLua(PluginContext context)
    {
        using var lua = new NLua.Lua();

        // Register hazina.* and json.* globals
        LuaGlobalContext.Register(lua, context);

        // Execute the script body (defines functions / metadata)
        lua.DoString(_script.SourceCode);

        // Check if the script exports an `execute` function
        var executeFunc = lua["execute"] as LuaFunction;
        if (executeFunc == null)
        {
            context.Logger.LogDebug("Lua script '{Name}' has no execute() function — returning success", Name);
            return PluginResult.Successful();
        }

        // Build the context table passed to execute(ctx)
        var ctxTable = BuildContextTable(lua, context);

        // Call execute(context)
        var results = executeFunc.Call(ctxTable);

        return MapResult(results);
    }

    private static LuaTable BuildContextTable(NLua.Lua lua, PluginContext context)
    {
        lua.NewTable("__ctx");
        var ctx = lua.GetTable("__ctx");

        // params sub-table
        lua.NewTable("__ctx_params");
        var paramsTable = lua.GetTable("__ctx_params");
        foreach (var (k, v) in context.Parameters)
            paramsTable[k] = v;
        ctx["params"] = paramsTable;

        // user sub-table
        lua.NewTable("__ctx_user");
        var userTable = lua.GetTable("__ctx_user");
        foreach (var (k, v) in context.UserContext)
            userTable[k] = v;
        ctx["user"] = userTable;

        return ctx;
    }

    private static PluginResult MapResult(object?[]? results)
    {
        if (results == null || results.Length == 0)
            return PluginResult.Successful();

        // If the script returns a table, map it to PluginResult
        if (results[0] is LuaTable tbl)
        {
            var success = tbl["success"] is bool b ? b : true;
            var message = tbl["message"] as string;
            var data    = tbl["data"];

            // Convert data LuaTable → Dictionary for clean serialization
            if (data is LuaTable dataTbl)
                data = LuaTableToDictionary(dataTbl);

            return success
                ? PluginResult.Successful(data, message)
                : PluginResult.Failed(message ?? "Lua script returned failure");
        }

        // Scalar return (string, number, etc.) → success with raw data
        return PluginResult.Successful(results[0]);
    }

    private static Dictionary<string, object?> LuaTableToDictionary(LuaTable table)
    {
        var dict = new Dictionary<string, object?>();
        foreach (var key in table.Keys)
            dict[key.ToString()!] = table[key] is LuaTable nested
                ? LuaTableToDictionary(nested)
                : table[key];
        return dict;
    }
}
