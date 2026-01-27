using Hazina.AI.Agents.Tools;
using Hazina.Core.Plugins.Management;
using Microsoft.Extensions.Logging;

namespace Hazina.Core.Plugins.Tools;

/// <summary>
/// Tool for viewing version history of a dynamic plugin
/// </summary>
public class GetPluginVersionHistoryTool : AgentTool
{
    private readonly PluginManager _pluginManager;
    private readonly ILogger<GetPluginVersionHistoryTool> _logger;

    public GetPluginVersionHistoryTool(
        PluginManager pluginManager,
        ILogger<GetPluginVersionHistoryTool> logger)
    {
        _pluginManager = pluginManager;
        _logger = logger;

        Name = "get_plugin_version_history";
        Description = "Get version history for a dynamic plugin. " +
                     "Shows all versions with timestamps, change descriptions, and which version is active.";

        Parameters = new Dictionary<string, ToolParameter>
        {
            ["plugin_identifier"] = new ToolParameter
            {
                Type = "string",
                Description = "Plugin name or ID to get history for",
                Required = true
            }
        };
    }

    public override async Task<ToolResult> ExecuteAsync(
        Dictionary<string, object> arguments,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate arguments
            if (!ValidateArguments(arguments, out var validationError))
            {
                return new ToolResult
                {
                    Success = false,
                    Error = validationError
                };
            }

            // Extract parameters
            var identifier = arguments["plugin_identifier"].ToString()!;

            // Get plugin metadata to get ID if name was provided
            string pluginId;
            string pluginName;

            if (Guid.TryParse(identifier, out _))
            {
                pluginId = identifier;
                var metadata = await _pluginManager.GetPluginMetadataAsync(pluginId, cancellationToken);
                pluginName = metadata?.Name ?? "Unknown";
            }
            else
            {
                // Look up by name
                var allPlugins = await _pluginManager.ListPluginsAsync(false, cancellationToken);
                var plugin = allPlugins.FirstOrDefault(p =>
                    p.Name.Equals(identifier, StringComparison.OrdinalIgnoreCase));

                if (plugin == null)
                {
                    return new ToolResult
                    {
                        Success = false,
                        Error = $"Plugin not found: {identifier}"
                    };
                }

                pluginId = plugin.Id;
                pluginName = plugin.Name;
            }

            // Get version history
            var versions = await _pluginManager.GetPluginVersionsAsync(pluginId, cancellationToken);

            if (!versions.Any())
            {
                return new ToolResult
                {
                    Success = true,
                    Output = $"Plugin '{pluginName}' has no version history yet.",
                    Metadata = new Dictionary<string, object>
                    {
                        ["plugin_id"] = pluginId,
                        ["plugin_name"] = pluginName,
                        ["total_versions"] = 0
                    }
                };
            }

            // Format output
            var output = FormatVersionHistory(pluginName, versions);

            return new ToolResult
            {
                Success = true,
                Output = output,
                Metadata = new Dictionary<string, object>
                {
                    ["plugin_id"] = pluginId,
                    ["plugin_name"] = pluginName,
                    ["total_versions"] = versions.Count,
                    ["active_version"] = versions.FirstOrDefault(v => v.IsActive)?.Version ?? 0,
                    ["versions"] = versions.OrderByDescending(v => v.Version).Select(v => new
                    {
                        v.Version,
                        v.IsActive,
                        v.ChangeDescription,
                        v.CreatedBy,
                        v.CreatedAt,
                        SourceCodeLength = v.SourceCode.Length
                    }).ToList()
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get plugin version history");
            return new ToolResult
            {
                Success = false,
                Error = $"Failed to get version history: {ex.Message}",
                Metadata = new Dictionary<string, object>
                {
                    ["exception_type"] = ex.GetType().Name
                }
            };
        }
    }

    private string FormatVersionHistory(string pluginName, List<Abstractions.PluginVersion> versions)
    {
        var lines = new List<string>
        {
            $"Version history for plugin: {pluginName}",
            $"Total versions: {versions.Count}",
            ""
        };

        foreach (var version in versions.OrderByDescending(v => v.Version))
        {
            var activeMarker = version.IsActive ? "★ ACTIVE" : "  ";
            lines.Add($"{activeMarker} Version {version.Version}");
            lines.Add($"         Created: {version.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC");
            lines.Add($"         Created By: {version.CreatedBy ?? "Unknown"}");

            if (!string.IsNullOrEmpty(version.ChangeDescription))
            {
                lines.Add($"         Changes: {version.ChangeDescription}");
            }

            lines.Add($"         Source Code: {version.SourceCode.Length} characters");
            lines.Add("");
        }

        return string.Join("\n", lines);
    }
}
