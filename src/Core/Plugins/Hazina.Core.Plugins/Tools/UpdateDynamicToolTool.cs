using Hazina.AI.Agents.Tools;
using Hazina.Core.Plugins.Management;
using Microsoft.Extensions.Logging;

namespace Hazina.Core.Plugins.Tools;

/// <summary>
/// Tool for updating/modifying existing dynamic plugins
/// Creates a new version and hot-reloads the plugin without downtime
/// </summary>
public class UpdateDynamicToolTool : AgentTool
{
    private readonly PluginManager _pluginManager;
    private readonly ILogger<UpdateDynamicToolTool> _logger;

    public UpdateDynamicToolTool(
        PluginManager pluginManager,
        ILogger<UpdateDynamicToolTool> logger)
    {
        _pluginManager = pluginManager;
        _logger = logger;

        Name = "update_dynamic_tool";
        Description = "Update an existing dynamic plugin with new source code. " +
                     "Creates a new version, recompiles, and hot-reloads without downtime. " +
                     "Previous versions are kept for rollback.";

        Parameters = new Dictionary<string, ToolParameter>
        {
            ["plugin_identifier"] = new ToolParameter
            {
                Type = "string",
                Description = "Plugin name or ID to update",
                Required = true
            },
            ["new_source_code"] = new ToolParameter
            {
                Type = "string",
                Description = "New C# source code for the plugin",
                Required = true
            },
            ["change_description"] = new ToolParameter
            {
                Type = "string",
                Description = "Description of what changed in this version (e.g., 'Added error handling')",
                Required = false
            },
            ["updated_by"] = new ToolParameter
            {
                Type = "string",
                Description = "Who is updating this plugin (default: 'AI')",
                Required = false,
                DefaultValue = "AI"
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
            var newSourceCode = arguments["new_source_code"].ToString()!;
            var changeDescription = arguments.TryGetValue("change_description", out var desc)
                ? desc?.ToString()
                : null;
            var updatedBy = arguments.TryGetValue("updated_by", out var updater)
                ? updater?.ToString()
                : "AI";

            // Get plugin metadata to get ID if name was provided
            string pluginId;
            if (Guid.TryParse(identifier, out _))
            {
                pluginId = identifier;
            }
            else
            {
                // Look up by name
                var metadata = await _pluginManager.GetPluginMetadataAsync(identifier, cancellationToken);
                if (metadata == null)
                {
                    // Try getting by name from repository
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
                }
                else
                {
                    pluginId = metadata.Id;
                }
            }

            // Update plugin
            _logger.LogInformation("Updating plugin: {PluginId}", pluginId);
            var versionId = await _pluginManager.UpdatePluginAsync(
                pluginId,
                newSourceCode,
                changeDescription,
                updatedBy,
                cancellationToken);

            // Get version history for metadata
            var versions = await _pluginManager.GetPluginVersionsAsync(pluginId, cancellationToken);
            var newVersion = versions.FirstOrDefault(v => v.VersionId == versionId);

            return new ToolResult
            {
                Success = true,
                Output = $"Plugin '{identifier}' updated successfully. " +
                        $"New version: {newVersion?.Version ?? 0}. " +
                        $"Plugin has been hot-reloaded.",
                Metadata = new Dictionary<string, object>
                {
                    ["plugin_id"] = pluginId,
                    ["version_id"] = versionId,
                    ["version_number"] = newVersion?.Version ?? 0,
                    ["total_versions"] = versions.Count,
                    ["change_description"] = changeDescription ?? "No description",
                    ["updated_by"] = updatedBy ?? "AI"
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update dynamic plugin");
            return new ToolResult
            {
                Success = false,
                Error = $"Failed to update plugin: {ex.Message}",
                Metadata = new Dictionary<string, object>
                {
                    ["exception_type"] = ex.GetType().Name
                }
            };
        }
    }
}
