using System.Text.Json;
using Hazina.Core.Plugins.Abstractions;
using Microsoft.Extensions.Logging;
using NLua;

namespace Hazina.Core.Plugins.Lua.Execution;

/// <summary>
/// Registers Hazina API globals into an NLua state so that scripts can call:
///
///   hazina.log("hello")
///   hazina.log_warn("something")
///   hazina.log_error("oops")
///   hazina.get_param("key")           -- returns string or nil
///   hazina.get_user_context("key")    -- returns string or nil
///   json.encode(table)                -- table → JSON string
///   json.decode(str)                  -- JSON string → Lua value
/// </summary>
public static class LuaGlobalContext
{
    /// <summary>
    /// Register all Hazina globals into the given <paramref name="lua"/> state.
    /// </summary>
    public static void Register(NLua.Lua lua, PluginContext context)
    {
        RegisterHazinaTable(lua, context);
        RegisterJsonHelpers(lua);
    }

    // ------------------------------------------------------------------
    // hazina.*
    // ------------------------------------------------------------------

    private static void RegisterHazinaTable(NLua.Lua lua, PluginContext context)
    {
        // Create the hazina table
        lua.NewTable("hazina");
        var hazina = lua.GetTable("hazina");

        // Logging helpers — closures capture the PluginContext logger
        hazina["log"] = (Action<string>)(msg =>
            context.Logger.LogInformation("[Lua] {Msg}", msg));

        hazina["log_warn"] = (Action<string>)(msg =>
            context.Logger.LogWarning("[Lua] {Msg}", msg));

        hazina["log_error"] = (Action<string>)(msg =>
            context.Logger.LogError("[Lua] {Msg}", msg));

        // Parameter access
        hazina["get_param"] = (Func<string, object?>)(key =>
        {
            context.Parameters.TryGetValue(key, out var val);
            return val;
        });

        // User context access
        hazina["get_user_context"] = (Func<string, string?>)(key =>
        {
            context.UserContext.TryGetValue(key, out var val);
            return val;
        });
    }

    // ------------------------------------------------------------------
    // json.*
    // ------------------------------------------------------------------

    private static void RegisterJsonHelpers(NLua.Lua lua)
    {
        lua.NewTable("json");
        var jsonTable = lua.GetTable("json");

        // json.encode(value) → string
        jsonTable["encode"] = (Func<object?, string>)(value =>
        {
            try
            {
                return JsonSerializer.Serialize(LuaToClr(value),
                    new JsonSerializerOptions { WriteIndented = false });
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"json.encode failed: {ex.Message}", ex);
            }
        });

        // json.decode(str) → Lua value (string/number/bool/table/nil)
        jsonTable["decode"] = (Func<string, object?>)(str =>
        {
            try
            {
                var doc = JsonDocument.Parse(str);
                return JsonElementToLua(lua, doc.RootElement);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"json.decode failed: {ex.Message}", ex);
            }
        });
    }

    // ------------------------------------------------------------------
    // Conversion helpers
    // ------------------------------------------------------------------

    /// <summary>Convert a Lua value (LuaTable, primitive) to a CLR object suitable for JSON serialization.</summary>
    private static object? LuaToClr(object? value)
    {
        return value switch
        {
            LuaTable table => LuaTableToDictionary(table),
            null           => null,
            _              => value
        };
    }

    private static Dictionary<string, object?> LuaTableToDictionary(LuaTable table)
    {
        var dict = new Dictionary<string, object?>();
        foreach (var key in table.Keys)
        {
            dict[key.ToString()!] = LuaToClr(table[key]);
        }
        return dict;
    }

    /// <summary>Convert a <see cref="JsonElement"/> to a Lua-friendly CLR value.</summary>
    private static object? JsonElementToLua(NLua.Lua lua, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                lua.NewTable("__json_tmp");
                var tbl = lua.GetTable("__json_tmp");
                foreach (var prop in element.EnumerateObject())
                    tbl[prop.Name] = JsonElementToLua(lua, prop.Value);
                return tbl;

            case JsonValueKind.Array:
                lua.NewTable("__json_arr");
                var arr = lua.GetTable("__json_arr");
                int i = 1; // Lua arrays are 1-based
                foreach (var item in element.EnumerateArray())
                    arr[i++] = JsonElementToLua(lua, item);
                return arr;

            case JsonValueKind.String:  return element.GetString();
            case JsonValueKind.Number:
                if (element.TryGetInt64(out var lng)) return lng;
                return element.GetDouble();
            case JsonValueKind.True:    return true;
            case JsonValueKind.False:   return false;
            default:                    return null;
        }
    }
}
