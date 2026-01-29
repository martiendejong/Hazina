using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hazina.Agents.Tools.Mcp;

/// <summary>
/// Configuration for MCP servers, compatible with Claude Code's settings format.
/// </summary>
public class McpSettings
{
    [JsonPropertyName("mcpServers")]
    public Dictionary<string, McpServerConfig> McpServers { get; set; } = new();

    /// <summary>
    /// Loads MCP settings from a file (e.g., .claude/settings.json or mcp-settings.json).
    /// </summary>
    public static McpSettings? LoadFromFile(string path)
    {
        try
        {
            if (!File.Exists(path))
                return null;

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<McpSettings>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Searches for MCP settings in common locations.
    /// </summary>
    public static McpSettings? FindSettings(string workingDirectory)
    {
        var searchPaths = new[]
        {
            Path.Combine(workingDirectory, ".claude", "settings.json"),
            Path.Combine(workingDirectory, "mcp-settings.json"),
            Path.Combine(workingDirectory, ".mcp", "settings.json"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "settings.json"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Claude", "settings.json"),
        };

        foreach (var path in searchPaths)
        {
            var settings = LoadFromFile(path);
            if (settings?.McpServers?.Count > 0)
            {
                return settings;
            }
        }

        return null;
    }
}

public class McpServerConfig
{
    [JsonPropertyName("command")]
    public string Command { get; set; } = "";

    [JsonPropertyName("args")]
    public List<string>? Args { get; set; }

    [JsonPropertyName("env")]
    public Dictionary<string, string>? Env { get; set; }

    [JsonPropertyName("disabled")]
    public bool Disabled { get; set; }
}
