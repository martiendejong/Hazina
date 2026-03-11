using System.Text.Json;
using Hazina.AI.Agents.Tools;
using Hazina.Core.Plugins.Management;
using Microsoft.Extensions.Logging;

namespace Hazina.Core.Plugins.Tools;

/// <summary>
/// Tool for listing all registered dynamic plugins
/// </summary>
public class ListDynamicToolsTool : AgentTool
{
    private readonly PluginManager _pluginManager;
    private readonly ILogger<ListDynamicToolsTool> _logger;

    public ListDynamicToolsTool(
        PluginManager pluginManager,
        ILogger<ListDynamicToolsTool> logger)
    {
        _pluginManager = pluginManager;
        _logger = logger;

        Name = "list_dynamic_tools";
        Description = "List all registered dynamic plugins/tools. " +
                     "Shows plugin names, IDs, descriptions, and enabled status.";

        Parameters = new Dictionary<string, ToolParameter>
        {
            ["enabled_only"] = new ToolParameter
            {
                Type = "boolean",
                Description = "If true, only return enabled plugins (default: false)",
                Required = false,
                DefaultValue = false
            },
            ["tag"] = new ToolParameter
            {
                Type = "string",
                Description = "Optional tag to filter plugins (e.g., 'email')",
                Required = false
            },
            ["format"] = new ToolParameter
            {
                Type = "string",
                Description = "Output format: 'summary' (default) or 'detailed'",
                Required = false,
                DefaultValue = "summary"
            }
        };
    }

    public override async Task<ToolResult> ExecuteAsync(
        Dictionary<string, object> arguments,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Extract parameters
            var enabledOnly = arguments.TryGetValue("enabled_only", out var enabled)
                ? Convert.ToBoolean(enabled)
                : false;
            var format = arguments.TryGetValue("format", out var fmt)
                ? fmt?.ToString() ?? "summary"
                : "summary";

            // Get plugins
            _logger.LogInformation("Listing dynamic plugins (enabled only: {EnabledOnly})", enabledOnly);
            var plugins = await _pluginManager.ListPluginsAsync(enabledOnly, cancellationToken);

            // Filter by tag if specified
            if (arguments.TryGetValue("tag", out var tagObj) && tagObj != null)
            {
                var tag = tagObj.ToString()!;
                plugins = plugins.Where(p => p.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase)).ToList();
            }

            // Format output
            string output;
            if (format == "detailed")
            {
                output = FormatDetailed(plugins);
            }
            else
            {
                output = FormatSummary(plugins);
            }

            return new ToolResult
            {
                Success = true,
                Output = output,
                Metadata = new Dictionary<string, object>
                {
                    ["total_plugins"] = plugins.Count,
                    ["enabled_count"] = plugins.Count(p => p.Enabled),
                    ["disabled_count"] = plugins.Count(p => !p.Enabled),
                    ["plugins"] = plugins.Select(p => new
                    {
                        p.Id,
                        p.Name,
                        p.Version,
                        p.Description,
                        p.Enabled,
                        p.Tags,
                        p.CreatedBy,
                        p.CreatedAt
                    }).ToList()
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list dynamic plugins");
            return new ToolResult
            {
                Success = false,
                Error = $"Failed to list plugins: {ex.Message}"
            };
        }
    }

    private string FormatSummary(List<Abstractions.PluginMetadata> plugins)
    {
        if (plugins.Count == 0)
        {
            return "No dynamic plugins registered.";
        }

        var lines = new List<string>
        {
            $"Found {plugins.Count} dynamic plugin(s):",
            ""
        };

        foreach (var plugin in plugins.OrderBy(p => p.Name))
        {
            var status = plugin.Enabled ? "✓ ENABLED" : "✗ DISABLED";
            var tags = plugin.Tags.Any() ? $" [{string.Join(", ", plugin.Tags)}]" : "";
            lines.Add($"  {status} {plugin.Name} (v{plugin.Version}) - {plugin.Id}{tags}");
            if (!string.IsNullOrEmpty(plugin.Description))
            {
                lines.Add($"           {plugin.Description}");
            }
        }

        return string.Join("\n", lines);
    }

    private string FormatDetailed(List<Abstractions.PluginMetadata> plugins)
    {
        if (plugins.Count == 0)
        {
            return "No dynamic plugins registered.";
        }

        var lines = new List<string>
        {
            $"Found {plugins.Count} dynamic plugin(s):",
            ""
        };

        foreach (var plugin in plugins.OrderBy(p => p.Name))
        {
            lines.Add($"Plugin: {plugin.Name}");
            lines.Add($"  ID: {plugin.Id}");
            lines.Add($"  Version: {plugin.Version}");
            lines.Add($"  Enabled: {plugin.Enabled}");
            lines.Add($"  Created: {plugin.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC");
            lines.Add($"  Created By: {plugin.CreatedBy ?? "Unknown"}");

            if (!string.IsNullOrEmpty(plugin.Description))
            {
                lines.Add($"  Description: {plugin.Description}");
            }

            if (plugin.Tags.Any())
            {
                lines.Add($"  Tags: {string.Join(", ", plugin.Tags)}");
            }

            lines.Add($"  Source Code Length: {plugin.SourceCode.Length} characters");
            lines.Add("");
        }

        return string.Join("\n", lines);
    }
}
