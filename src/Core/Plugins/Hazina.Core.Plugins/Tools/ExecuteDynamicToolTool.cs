using System.Text.Json;
using Hazina.AI.Agents.Tools;
using Hazina.Core.Plugins.Abstractions;
using Hazina.Core.Plugins.Management;
using Microsoft.Extensions.Logging;

namespace Hazina.Core.Plugins.Tools;

/// <summary>
/// Tool for executing a registered dynamic plugin
/// </summary>
public class ExecuteDynamicToolTool : AgentTool
{
    private readonly PluginManager _pluginManager;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ExecuteDynamicToolTool> _logger;

    public ExecuteDynamicToolTool(
        PluginManager pluginManager,
        IServiceProvider serviceProvider,
        ILogger<ExecuteDynamicToolTool> logger)
    {
        _pluginManager = pluginManager;
        _serviceProvider = serviceProvider;
        _logger = logger;

        Name = "execute_dynamic_tool";
        Description = "Execute a previously registered dynamic plugin by name or ID. " +
                     "Provides parameters and receives the plugin's result.";

        Parameters = new Dictionary<string, ToolParameter>
        {
            ["plugin_identifier"] = new ToolParameter
            {
                Type = "string",
                Description = "Plugin name or ID to execute",
                Required = true
            },
            ["parameters"] = new ToolParameter
            {
                Type = "object",
                Description = "Parameters to pass to the plugin (as key-value pairs)",
                Required = false,
                DefaultValue = new Dictionary<string, object>()
            },
            ["user_context"] = new ToolParameter
            {
                Type = "object",
                Description = "Optional user context (user_id, tenant_id, etc.)",
                Required = false
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

            var parameters = new Dictionary<string, object>();
            if (arguments.TryGetValue("parameters", out var paramsObj))
            {
                if (paramsObj is Dictionary<string, object> paramsDict)
                {
                    parameters = paramsDict;
                }
                else if (paramsObj is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Object)
                {
                    // Convert JsonElement to Dictionary
                    parameters = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonElement.GetRawText())
                        ?? new Dictionary<string, object>();
                }
            }

            var userContext = new Dictionary<string, string>();
            if (arguments.TryGetValue("user_context", out var userCtxObj))
            {
                if (userCtxObj is Dictionary<string, string> userCtxDict)
                {
                    userContext = userCtxDict;
                }
                else if (userCtxObj is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Object)
                {
                    userContext = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonElement.GetRawText())
                        ?? new Dictionary<string, string>();
                }
            }

            // Create plugin context
            var context = new PluginContext
            {
                Services = _serviceProvider,
                Logger = _logger,
                Parameters = parameters,
                UserContext = userContext
            };

            // Execute plugin (try by ID first, then by name)
            _logger.LogInformation("Executing dynamic plugin: {Identifier}", identifier);
            PluginResult result;

            // Check if identifier is a GUID (ID) or name
            if (Guid.TryParse(identifier, out _))
            {
                result = await _pluginManager.ExecutePluginAsync(identifier, context, cancellationToken);
            }
            else
            {
                result = await _pluginManager.ExecutePluginByNameAsync(identifier, context, cancellationToken);
            }

            // Convert PluginResult to ToolResult
            return new ToolResult
            {
                Success = result.Success,
                Output = result.Message ?? (result.Success ? "Plugin executed successfully" : "Plugin execution failed"),
                Error = result.Success ? null : result.Message,
                Metadata = new Dictionary<string, object>
                {
                    ["plugin_identifier"] = identifier,
                    ["plugin_success"] = result.Success,
                    ["plugin_data"] = result.Data ?? new object(),
                    ["plugin_metadata"] = result.Metadata
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute dynamic plugin");
            return new ToolResult
            {
                Success = false,
                Error = $"Failed to execute plugin: {ex.Message}",
                Metadata = new Dictionary<string, object>
                {
                    ["exception_type"] = ex.GetType().Name
                }
            };
        }
    }
}
