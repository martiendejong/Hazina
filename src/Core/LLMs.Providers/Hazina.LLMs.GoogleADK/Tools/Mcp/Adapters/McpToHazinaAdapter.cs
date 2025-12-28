using System.Text.Json;
using Hazina.LLMs.GoogleADK.Tools.Mcp.Client;
using Hazina.LLMs.GoogleADK.Tools.Mcp.Models;
using Microsoft.Extensions.Logging;

namespace Hazina.LLMs.GoogleADK.Tools.Mcp.Adapters;

/// <summary>
/// Adapter that converts MCP tools to Hazina tools
/// </summary>
public class McpToHazinaAdapter
{
    private readonly McpClient _mcpClient;
    private readonly ILogger? _logger;

    public McpToHazinaAdapter(McpClient mcpClient, ILogger? logger = null)
    {
        _mcpClient = mcpClient ?? throw new ArgumentNullException(nameof(mcpClient));
        _logger = logger;
    }

    /// <summary>
    /// Convert MCP tools to Hazina chat tools
    /// </summary>
    public async Task<List<HazinaChatTool>> GetHazinaToolsAsync(CancellationToken cancellationToken = default)
    {
        var mcpTools = await _mcpClient.ListToolsAsync(cancellationToken);
        var hazinaTools = new List<HazinaChatTool>();

        foreach (var mcpTool in mcpTools)
        {
            var hazinaTool = ConvertToHazinaTool(mcpTool);
            hazinaTools.Add(hazinaTool);
        }

        _logger?.LogInformation("Converted {Count} MCP tools to Hazina tools", hazinaTools.Count);
        return hazinaTools;
    }

    /// <summary>
    /// Convert a single MCP tool to a Hazina tool
    /// </summary>
    public HazinaChatTool ConvertToHazinaTool(McpTool mcpTool)
    {
        var parameters = ExtractParameters(mcpTool.InputSchema);

        return new HazinaChatTool(
            name: mcpTool.Name,
            description: mcpTool.Description,
            parameters: parameters,
            execute: async (messages, toolCall, cancellationToken) =>
            {
                return await ExecuteMcpToolAsync(mcpTool.Name, toolCall, cancellationToken);
            }
        );
    }

    /// <summary>
    /// Extract parameters from JSON Schema
    /// </summary>
    private List<ChatToolParameter> ExtractParameters(JsonElement schema)
    {
        var parameters = new List<ChatToolParameter>();

        if (schema.ValueKind != JsonValueKind.Object)
        {
            return parameters;
        }

        if (!schema.TryGetProperty("properties", out var properties))
        {
            return parameters;
        }

        var required = new HashSet<string>();
        if (schema.TryGetProperty("required", out var requiredArray) && requiredArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in requiredArray.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    required.Add(item.GetString() ?? string.Empty);
                }
            }
        }

        foreach (var property in properties.EnumerateObject())
        {
            var parameter = new ChatToolParameter
            {
                Name = property.Name,
                Required = required.Contains(property.Name)
            };

            if (property.Value.TryGetProperty("type", out var typeElement))
            {
                parameter.Type = typeElement.GetString() ?? "string";
            }

            if (property.Value.TryGetProperty("description", out var descElement))
            {
                parameter.Description = descElement.GetString() ?? string.Empty;
            }

            parameters.Add(parameter);
        }

        return parameters;
    }

    /// <summary>
    /// Execute an MCP tool via the client
    /// </summary>
    private async Task<string> ExecuteMcpToolAsync(
        string toolName,
        HazinaChatToolCall toolCall,
        CancellationToken cancellationToken)
    {
        try
        {
            // Parse arguments from the tool call (FunctionArguments is BinaryData)
            var argumentsJson = toolCall.FunctionArguments.ToString();
            var arguments = ParseArguments(argumentsJson);

            // Call the MCP tool
            var response = await _mcpClient.CallToolAsync(toolName, arguments, cancellationToken);

            // Convert response to string
            return FormatToolResponse(response);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error executing MCP tool: {ToolName}", toolName);
            return $"Error executing tool {toolName}: {ex.Message}";
        }
    }

    /// <summary>
    /// Parse arguments from JSON string to dictionary
    /// </summary>
    private Dictionary<string, object> ParseArguments(string argumentsJson)
    {
        if (string.IsNullOrWhiteSpace(argumentsJson))
        {
            return new Dictionary<string, object>();
        }

        try
        {
            var jsonElement = JsonSerializer.Deserialize<JsonElement>(argumentsJson);
            return JsonElementToDictionary(jsonElement);
        }
        catch (JsonException ex)
        {
            _logger?.LogWarning(ex, "Failed to parse arguments: {Json}", argumentsJson);
            return new Dictionary<string, object>();
        }
    }

    /// <summary>
    /// Convert JsonElement to dictionary
    /// </summary>
    private Dictionary<string, object> JsonElementToDictionary(JsonElement element)
    {
        var dict = new Dictionary<string, object>();

        if (element.ValueKind != JsonValueKind.Object)
        {
            return dict;
        }

        foreach (var property in element.EnumerateObject())
        {
            dict[property.Name] = JsonElementToObject(property.Value);
        }

        return dict;
    }

    /// <summary>
    /// Convert JsonElement to object
    /// </summary>
    private object JsonElementToObject(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => element.TryGetInt32(out var intValue) ? intValue : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Array => element.EnumerateArray().Select(JsonElementToObject).ToList(),
            JsonValueKind.Object => JsonElementToDictionary(element),
            _ => element.ToString()
        };
    }

    /// <summary>
    /// Format tool response for Hazina
    /// </summary>
    private string FormatToolResponse(CallToolResponse response)
    {
        if (response.IsError)
        {
            var errorContent = response.Content.FirstOrDefault();
            return $"Error: {errorContent?.Text ?? "Unknown error"}";
        }

        if (response.Content.Count == 0)
        {
            return "Tool executed successfully (no output)";
        }

        var textContents = response.Content
            .Where(c => c.Type == "text" && !string.IsNullOrEmpty(c.Text))
            .Select(c => c.Text)
            .ToList();

        if (textContents.Any())
        {
            return string.Join("\n", textContents);
        }

        return JsonSerializer.Serialize(response.Content);
    }
}
