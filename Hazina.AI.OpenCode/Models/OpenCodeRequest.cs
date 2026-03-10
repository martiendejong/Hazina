using System.Text.Json.Serialization;

namespace Hazina.AI.OpenCode.Models;

/// <summary>
/// Request model for OpenCode API calls
/// </summary>
public class OpenCodeRequest
{
    /// <summary>
    /// The message/task to send to the agent
    /// </summary>
    [JsonPropertyName("message")]
    public required string Message { get; set; }

    /// <summary>
    /// Model to use (e.g., "anthropic/claude-sonnet-4-5")
    /// </summary>
    [JsonPropertyName("model")]
    public string? Model { get; set; }

    /// <summary>
    /// Agent name to route to
    /// </summary>
    [JsonPropertyName("agent")]
    public string? Agent { get; set; }

    /// <summary>
    /// Working directory for the agent
    /// </summary>
    [JsonPropertyName("cwd")]
    public string? WorkingDirectory { get; set; }

    /// <summary>
    /// Session ID to continue
    /// </summary>
    [JsonPropertyName("session")]
    public string? SessionId { get; set; }
}
