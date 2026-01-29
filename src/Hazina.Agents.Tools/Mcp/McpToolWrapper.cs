using System.Text.Json;
using Hazina.LLMs;

namespace Hazina.Agents.Tools.Mcp;

/// <summary>
/// Wraps an MCP tool as a HazinaChatTool for use with the Hazina framework.
/// </summary>
public static class McpToolWrapper
{
    /// <summary>
    /// Creates a HazinaChatTool from an MCP tool definition.
    /// </summary>
    public static HazinaChatTool CreateTool(McpClient client, McpTool mcpTool)
    {
        var parameters = new List<ChatToolParameter>();

        if (mcpTool.InputSchema?.Properties != null)
        {
            foreach (var (name, schema) in mcpTool.InputSchema.Properties)
            {
                var isRequired = mcpTool.InputSchema.Required?.Contains(name) ?? false;
                parameters.Add(new ChatToolParameter
                {
                    Name = name,
                    Type = MapJsonType(schema.Type),
                    Description = schema.Description,
                    Required = isRequired
                });
            }
        }

        var toolName = $"mcp_{client.ServerName}_{mcpTool.Name}".Replace("-", "_");

        return new HazinaChatTool(
            name: toolName,
            description: $"[MCP:{client.ServerName}] {mcpTool.Description}",
            parameters: parameters,
            execute: async (messages, call, cancel) =>
            {
                try
                {
                    var args = ParseArguments(call.FunctionArguments);
                    return await client.CallToolAsync(mcpTool.Name, args, cancel);
                }
                catch (Exception ex)
                {
                    return $"Error calling MCP tool {mcpTool.Name}: {ex.Message}";
                }
            }
        );
    }

    /// <summary>
    /// Creates HazinaChatTools for all tools from an MCP client.
    /// </summary>
    public static List<HazinaChatTool> CreateToolsFromClient(McpClient client)
    {
        var tools = new List<HazinaChatTool>();

        foreach (var mcpTool in client.Tools)
        {
            tools.Add(CreateTool(client, mcpTool));
        }

        return tools;
    }

    private static string MapJsonType(string jsonType)
    {
        return jsonType.ToLowerInvariant() switch
        {
            "string" => "string",
            "number" => "number",
            "integer" => "number",
            "boolean" => "boolean",
            "array" => "array",
            "object" => "object",
            _ => "string"
        };
    }

    private static Dictionary<string, object>? ParseArguments(BinaryData argumentsData)
    {
        try
        {
            var json = argumentsData.ToString();
            return JsonSerializer.Deserialize<Dictionary<string, object>>(json);
        }
        catch
        {
            return null;
        }
    }
}
