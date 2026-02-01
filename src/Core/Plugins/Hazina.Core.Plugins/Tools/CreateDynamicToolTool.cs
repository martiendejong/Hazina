using Hazina.AI.Agents.Tools;
using Hazina.Core.Plugins.Abstractions;
using Hazina.Core.Plugins.Management;
using Microsoft.Extensions.Logging;

namespace Hazina.Core.Plugins.Tools;

/// <summary>
/// Tool for creating new dynamic plugins from C# source code
/// This allows AI or users to generate new tools on-the-fly
/// </summary>
public class CreateDynamicToolTool : AgentTool
{
    private readonly PluginManager _pluginManager;
    private readonly ILogger<CreateDynamicToolTool> _logger;

    public CreateDynamicToolTool(
        PluginManager pluginManager,
        ILogger<CreateDynamicToolTool> logger)
    {
        _pluginManager = pluginManager;
        _logger = logger;

        Name = "create_dynamic_tool";
        Description = "Create a new dynamic plugin/tool from C# source code. " +
                     "The code will be compiled using Roslyn and can be executed later. " +
                     "This enables AI to generate new capabilities on-demand.";

        Parameters = new Dictionary<string, ToolParameter>
        {
            ["name"] = new ToolParameter
            {
                Type = "string",
                Description = "Unique name for the plugin (e.g., 'SendWelcomeEmail')",
                Required = true
            },
            ["source_code"] = new ToolParameter
            {
                Type = "string",
                Description = "C# source code for the plugin. Should return PluginResult. " +
                             "Has access to context.GetService<T>(), context.Parameters, context.Logger",
                Required = true
            },
            ["description"] = new ToolParameter
            {
                Type = "string",
                Description = "Human-readable description of what this plugin does",
                Required = false
            },
            ["tags"] = new ToolParameter
            {
                Type = "array",
                Description = "Optional tags for categorization (e.g., ['email', 'notifications'])",
                Required = false
            },
            ["compile_immediately"] = new ToolParameter
            {
                Type = "boolean",
                Description = "Whether to compile the plugin immediately (default: true)",
                Required = false,
                DefaultValue = true
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
            var name = arguments["name"].ToString()!;
            var sourceCode = arguments["source_code"].ToString()!;
            var description = arguments.TryGetValue("description", out var desc)
                ? desc?.ToString()
                : null;
            var compileImmediately = arguments.TryGetValue("compile_immediately", out var compile)
                ? Convert.ToBoolean(compile)
                : true;

            var tags = new List<string>();
            if (arguments.TryGetValue("tags", out var tagsObj) && tagsObj is IEnumerable<object> tagsList)
            {
                tags = tagsList.Select(t => t.ToString() ?? "").Where(t => !string.IsNullOrEmpty(t)).ToList();
            }

            // Create metadata
            var metadata = new PluginMetadata
            {
                Id = Guid.NewGuid().ToString(),
                Name = name,
                SourceCode = sourceCode,
                Description = description,
                CreatedBy = "AI",
                Tags = tags
            };

            // Register plugin
            _logger.LogInformation("Creating dynamic plugin: {PluginName}", name);
            var pluginId = await _pluginManager.RegisterPluginAsync(
                metadata,
                compileImmediately,
                cancellationToken);

            return new ToolResult
            {
                Success = true,
                Output = $"Dynamic plugin '{name}' created successfully with ID: {pluginId}",
                Metadata = new Dictionary<string, object>
                {
                    ["plugin_id"] = pluginId,
                    ["plugin_name"] = name,
                    ["compiled"] = compileImmediately
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create dynamic plugin");
            return new ToolResult
            {
                Success = false,
                Error = $"Failed to create plugin: {ex.Message}",
                Metadata = new Dictionary<string, object>
                {
                    ["exception_type"] = ex.GetType().Name
                }
            };
        }
    }
}
