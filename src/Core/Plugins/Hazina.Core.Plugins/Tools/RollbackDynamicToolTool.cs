using Hazina.AI.Agents.Tools;
using Hazina.Core.Plugins.Management;
using Microsoft.Extensions.Logging;

namespace Hazina.Core.Plugins.Tools;

/// <summary>
/// Tool for rolling back a dynamic plugin to a previous version
/// </summary>
public class RollbackDynamicToolTool : AgentTool
{
    private readonly PluginManager _pluginManager;
    private readonly ILogger<RollbackDynamicToolTool> _logger;

    public RollbackDynamicToolTool(
        PluginManager pluginManager,
        ILogger<RollbackDynamicToolTool> logger)
    {
        _pluginManager = pluginManager;
        _logger = logger;

        Name = "rollback_dynamic_tool";
        Description = "Rollback a dynamic plugin to a previous version. " +
                     "Recompiles the old version and hot-reloads without downtime. " +
                     "Use this if a plugin update caused issues.";

        Parameters = new Dictionary<string, ToolParameter>
        {
            ["plugin_identifier"] = new ToolParameter
            {
                Type = "string",
                Description = "Plugin name or ID to rollback",
                Required = true
            },
            ["target_version"] = new ToolParameter
            {
                Type = "number",
                Description = "Version number to rollback to (get from version history)",
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
            var targetVersion = Convert.ToInt32(arguments["target_version"]);

            if (targetVersion < 1)
            {
                return new ToolResult
                {
                    Success = false,
                    Error = "Version number must be >= 1"
                };
            }

            // Get plugin metadata to get ID if name was provided
            string pluginId;
            if (Guid.TryParse(identifier, out _))
            {
                pluginId = identifier;
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
            }

            // Get version history to validate target exists
            var versions = await _pluginManager.GetPluginVersionsAsync(pluginId, cancellationToken);
            if (!versions.Any(v => v.Version == targetVersion))
            {
                return new ToolResult
                {
                    Success = false,
                    Error = $"Version {targetVersion} not found for plugin '{identifier}'. " +
                           $"Available versions: {string.Join(", ", versions.Select(v => v.Version).OrderByDescending(v => v))}"
                };
            }

            // Perform rollback
            _logger.LogInformation("Rolling back plugin {PluginId} to version {Version}",
                pluginId, targetVersion);

            var success = await _pluginManager.RollbackPluginAsync(
                pluginId,
                targetVersion,
                cancellationToken);

            if (!success)
            {
                return new ToolResult
                {
                    Success = false,
                    Error = $"Rollback failed for plugin '{identifier}'"
                };
            }

            // Get the version details
            var targetVersionDetails = versions.FirstOrDefault(v => v.Version == targetVersion);

            return new ToolResult
            {
                Success = true,
                Output = $"Plugin '{identifier}' rolled back to version {targetVersion}. " +
                        $"Plugin has been hot-reloaded with previous code.",
                Metadata = new Dictionary<string, object>
                {
                    ["plugin_id"] = pluginId,
                    ["active_version"] = targetVersion,
                    ["total_versions"] = versions.Count,
                    ["rollback_description"] = targetVersionDetails?.ChangeDescription ?? "Original version",
                    ["rollback_created_at"] = targetVersionDetails?.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss") ?? "Unknown"
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rollback dynamic plugin");
            return new ToolResult
            {
                Success = false,
                Error = $"Failed to rollback plugin: {ex.Message}",
                Metadata = new Dictionary<string, object>
                {
                    ["exception_type"] = ex.GetType().Name
                }
            };
        }
    }
}
