using System.Text.Json.Serialization;

namespace Hazina.LLMs.GoogleADK.Tools.Mcp.Models;

/// <summary>
/// MCP prompt template
/// </summary>
public class McpPrompt
{
    /// <summary>
    /// Unique name of the prompt
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description of the prompt
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Arguments that can be provided to the prompt
    /// </summary>
    [JsonPropertyName("arguments")]
    public List<PromptArgument>? Arguments { get; set; }
}

/// <summary>
/// Argument for a prompt
/// </summary>
public class PromptArgument
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("required")]
    public bool Required { get; set; }
}

/// <summary>
/// Request to list prompts
/// </summary>
public class ListPromptsRequest
{
    [JsonPropertyName("cursor")]
    public string? Cursor { get; set; }
}

/// <summary>
/// Response with available prompts
/// </summary>
public class ListPromptsResponse
{
    [JsonPropertyName("prompts")]
    public List<McpPrompt> Prompts { get; set; } = new();

    [JsonPropertyName("nextCursor")]
    public string? NextCursor { get; set; }
}

/// <summary>
/// Request to get a prompt
/// </summary>
public class GetPromptRequest
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("arguments")]
    public Dictionary<string, string>? Arguments { get; set; }
}

/// <summary>
/// Response with prompt content
/// </summary>
public class GetPromptResponse
{
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("messages")]
    public List<PromptMessage> Messages { get; set; } = new();
}

/// <summary>
/// Message in a prompt
/// </summary>
public class PromptMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public PromptContent Content { get; set; } = new();
}

/// <summary>
/// Content in a prompt message
/// </summary>
public class PromptContent
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "text";

    [JsonPropertyName("text")]
    public string? Text { get; set; }
}
