using System.Text.Json;
using Hazina.LLMs.GoogleADK.Tools.Mcp.Models;
using Hazina.LLMs.GoogleADK.Tools.Mcp.Server;
using Microsoft.Extensions.Logging;

namespace Hazina.LLMs.GoogleADK.Tools.Mcp.Adapters;

/// <summary>
/// Adapter that converts Hazina tools to MCP tools for serving
/// </summary>
public class HazinaToMcpAdapter
{
    private readonly ILogger? _logger;

    public HazinaToMcpAdapter(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Register Hazina tools with an MCP server
    /// </summary>
    public void RegisterHazinaTools(McpServer server, IEnumerable<HazinaChatTool> hazinaTools)
    {
        foreach (var hazinaTool in hazinaTools)
        {
            var mcpTool = ConvertToMcpTool(hazinaTool);

            server.RegisterTool(mcpTool, async (arguments, cancellationToken) =>
            {
                return await ExecuteHazinaToolAsync(hazinaTool, arguments, cancellationToken);
            });
        }

        _logger?.LogInformation("Registered Hazina tools with MCP server");
    }

    /// <summary>
    /// Convert a Hazina tool to an MCP tool
    /// </summary>
    public McpTool ConvertToMcpTool(HazinaChatTool hazinaTool)
    {
        return new McpTool
        {
            Name = hazinaTool.FunctionName,
            Description = hazinaTool.Description,
            InputSchema = CreateInputSchema(hazinaTool.Parameters)
        };
    }

    /// <summary>
    /// Create JSON Schema for tool parameters
    /// </summary>
    private JsonElement CreateInputSchema(List<ChatToolParameter> parameters)
    {
        var properties = new Dictionary<string, object>();
        var required = new List<string>();

        foreach (var param in parameters)
        {
            properties[param.Name] = new
            {
                type = param.Type ?? "string",
                description = param.Description ?? string.Empty
            };

            if (param.Required)
            {
                required.Add(param.Name);
            }
        }

        var schema = new
        {
            type = "object",
            properties = properties,
            required = required.Count > 0 ? required : null
        };

        var json = JsonSerializer.Serialize(schema);
        return JsonSerializer.Deserialize<JsonElement>(json);
    }

    /// <summary>
    /// Execute a Hazina tool and return MCP response
    /// </summary>
    private async Task<CallToolResponse> ExecuteHazinaToolAsync(
        HazinaChatTool hazinaTool,
        Dictionary<string, object> arguments,
        CancellationToken cancellationToken)
    {
        try
        {
            // Convert arguments to JSON string format expected by Hazina tools
            var argumentsJson = JsonSerializer.Serialize(arguments);

            // Create a tool call object - HazinaChatToolCall uses BinaryData
            var toolCall = new HazinaChatToolCall(
                id: Guid.NewGuid().ToString(),
                functionName: hazinaTool.FunctionName,
                functionArguments: BinaryData.FromString(argumentsJson)
            );

            // Execute the Hazina tool
            var result = await hazinaTool.Execute(new List<HazinaChatMessage>(), toolCall, cancellationToken);

            // Return success response
            return new CallToolResponse
            {
                Content = new List<ToolContent>
                {
                    new ToolContent
                    {
                        Type = "text",
                        Text = result
                    }
                },
                IsError = false
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error executing Hazina tool: {ToolName}", hazinaTool.FunctionName);

            // Return error response
            return new CallToolResponse
            {
                Content = new List<ToolContent>
                {
                    new ToolContent
                    {
                        Type = "text",
                        Text = $"Error: {ex.Message}"
                    }
                },
                IsError = true
            };
        }
    }
}
