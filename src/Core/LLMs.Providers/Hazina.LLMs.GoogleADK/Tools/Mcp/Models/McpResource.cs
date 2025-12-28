using System.Text.Json.Serialization;

namespace Hazina.LLMs.GoogleADK.Tools.Mcp.Models;

/// <summary>
/// MCP resource (data that can be read by tools)
/// </summary>
public class McpResource
{
    /// <summary>
    /// Unique URI for the resource
    /// </summary>
    [JsonPropertyName("uri")]
    public string Uri { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable name
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description of the resource
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// MIME type of the resource
    /// </summary>
    [JsonPropertyName("mimeType")]
    public string? MimeType { get; set; }

    /// <summary>
    /// Optional metadata
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Request to list available resources
/// </summary>
public class ListResourcesRequest
{
    [JsonPropertyName("cursor")]
    public string? Cursor { get; set; }
}

/// <summary>
/// Response with available resources
/// </summary>
public class ListResourcesResponse
{
    [JsonPropertyName("resources")]
    public List<McpResource> Resources { get; set; } = new();

    [JsonPropertyName("nextCursor")]
    public string? NextCursor { get; set; }
}

/// <summary>
/// Request to read a resource
/// </summary>
public class ReadResourceRequest
{
    [JsonPropertyName("uri")]
    public string Uri { get; set; } = string.Empty;
}

/// <summary>
/// Response with resource contents
/// </summary>
public class ReadResourceResponse
{
    [JsonPropertyName("contents")]
    public List<ResourceContent> Contents { get; set; } = new();
}

/// <summary>
/// Content of a resource
/// </summary>
public class ResourceContent
{
    [JsonPropertyName("uri")]
    public string Uri { get; set; } = string.Empty;

    [JsonPropertyName("mimeType")]
    public string? MimeType { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("blob")]
    public string? Blob { get; set; }
}
