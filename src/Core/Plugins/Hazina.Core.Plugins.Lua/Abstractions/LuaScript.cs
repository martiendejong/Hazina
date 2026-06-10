namespace Hazina.Core.Plugins.Lua.Abstractions;

/// <summary>
/// Represents a loaded Lua script ready for execution.
/// </summary>
public class LuaScript
{
    /// <summary>Plugin name declared in the script's metadata table.</summary>
    public required string Name { get; init; }

    /// <summary>Plugin version declared in the script's metadata table.</summary>
    public required string Version { get; init; }

    /// <summary>Human-readable description declared in the script's metadata table.</summary>
    public required string Description { get; init; }

    /// <summary>Raw Lua source code.</summary>
    public required string SourceCode { get; init; }

    /// <summary>Absolute path on disk. Null for in-memory scripts.</summary>
    public string? FilePath { get; init; }

    /// <summary>UTC time the script was loaded / last reloaded.</summary>
    public DateTime LoadedAt { get; init; } = DateTime.UtcNow;
}
