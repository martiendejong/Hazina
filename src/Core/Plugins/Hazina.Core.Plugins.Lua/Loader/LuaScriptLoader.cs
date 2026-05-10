using Hazina.Core.Plugins.Lua.Abstractions;
using Microsoft.Extensions.Logging;
using NLua;

namespace Hazina.Core.Plugins.Lua.Loader;

/// <summary>
/// Loads and validates Lua scripts from source code or files.
/// Modelled after HazinaPluginCompiler — loads instead of compiles.
/// </summary>
public class LuaScriptLoader
{
    private readonly ILogger<LuaScriptLoader> _logger;

    public LuaScriptLoader(ILogger<LuaScriptLoader> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Load a Lua script from a file path.
    /// Validates that the file parses cleanly and declares the required metadata table.
    /// </summary>
    public LuaScript LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Lua script not found: {filePath}", filePath);

        var source = File.ReadAllText(filePath);
        _logger.LogInformation("Loading Lua script from file: {Path}", filePath);
        return LoadFromSource(source, filePath);
    }

    /// <summary>
    /// Load a Lua script from raw source code.
    /// </summary>
    public LuaScript LoadFromSource(string sourceCode, string? filePath = null)
    {
        ValidateAndExtractMetadata(sourceCode, filePath, out var name, out var version, out var description);

        return new LuaScript
        {
            Name = name,
            Version = version,
            Description = description,
            SourceCode = sourceCode,
            FilePath = filePath,
            LoadedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Load all *.lua files from a directory.
    /// Files that fail to load are logged and skipped.
    /// </summary>
    public IReadOnlyList<LuaScript> LoadDirectory(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            _logger.LogWarning("Lua scripts directory does not exist: {Path}", directoryPath);
            return Array.Empty<LuaScript>();
        }

        var scripts = new List<LuaScript>();
        foreach (var file in Directory.GetFiles(directoryPath, "*.lua", SearchOption.AllDirectories))
        {
            try
            {
                scripts.Add(LoadFromFile(file));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load Lua script: {File}", file);
            }
        }

        _logger.LogInformation("Loaded {Count} Lua scripts from {Directory}", scripts.Count, directoryPath);
        return scripts;
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private void ValidateAndExtractMetadata(
        string source,
        string? filePath,
        out string name,
        out string version,
        out string description)
    {
        // Use a throw-away NLua state to validate the script parses and to
        // read its optional metadata table.
        using var lua = new NLua.Lua();

        try
        {
            lua.DoString(source);
        }
        catch (NLua.Exceptions.LuaException ex)
        {
            var label = filePath ?? "<inline>";
            _logger.LogError("Lua syntax error in {File}: {Error}", label, ex.Message);
            throw new InvalidOperationException($"Lua syntax error in '{label}': {ex.Message}", ex);
        }

        // Try to read optional metadata table: plugin = { name=..., version=..., description=... }
        var meta = lua["plugin"] as NLua.LuaTable;

        name        = (meta?["name"]        as string) ?? Path.GetFileNameWithoutExtension(filePath ?? "lua-plugin");
        version     = (meta?["version"]     as string) ?? "1.0.0";
        description = (meta?["description"] as string) ?? "Lua plugin";

        _logger.LogDebug("Loaded Lua script '{Name}' v{Version}", name, version);
    }
}
