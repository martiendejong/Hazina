using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hazina.LLMs.GoogleADK.Tools.Mcp.Models;

/// <summary>
/// MCP tool definition
/// </summary>
public class McpTool
{
    /// <summary>
    /// Unique name of the tool
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable description of what the tool does
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// JSON Schema for the tool's input parameters
    /// </summary>
    [JsonPropertyName("inputSchema")]
    public JsonElement InputSchema { get; set; }

    /// <summary>
    /// Optional metadata about the tool
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Request to list available tools
/// </summary>
public class ListToolsRequest
{
    [JsonPropertyName("cursor")]
    public string? Cursor { get; set; }
}

/// <summary>
/// Response with available tools
/// </summary>
public class ListToolsResponse
{
    [JsonPropertyName("tools")]
    public List<McpTool> Tools { get; set; } = new();

    [JsonPropertyName("nextCursor")]
    public string? NextCursor { get; set; }
}

/// <summary>
/// Request to call a tool
/// </summary>
public class CallToolRequest
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("arguments")]
    public Dictionary<string, object> Arguments { get; set; } = new();
}

/// <summary>
/// Result of a tool call
/// </summary>
public class CallToolResponse
{
    [JsonPropertyName("content")]
    public List<ToolContent> Content { get; set; } = new();

    [JsonPropertyName("isError")]
    public bool IsError { get; set; }
}

/// <summary>
/// Content returned by a tool
/// </summary>
public class ToolContent
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "text";

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("data")]
    public string? Data { get; set; }

    [JsonPropertyName("mimeType")]
    public string? MimeType { get; set; }
}
