using System.Text.Json.Serialization;

namespace Hazina.AI.OpenCode.Models;

/// <summary>
/// Response model from OpenCode API
/// </summary>
public class OpenCodeResponse
{
    /// <summary>
    /// The response content from the agent
    /// </summary>
    [JsonPropertyName("content")]
    public string? Content { get; set; }

    /// <summary>
    /// Session ID for continuation
    /// </summary>
    [JsonPropertyName("session")]
    public string? SessionId { get; set; }

    /// <summary>
    /// Whether the response is complete
    /// </summary>
    [JsonPropertyName("done")]
    public bool Done { get; set; }

    /// <summary>
    /// Error message if any
    /// </summary>
    [JsonPropertyName("error")]
    public string? Error { get; set; }

    /// <summary>
    /// Token usage statistics
    /// </summary>
    [JsonPropertyName("usage")]
    public OpenCodeUsage? Usage { get; set; }
}

/// <summary>
/// Token usage statistics from OpenCode
/// </summary>
public class OpenCodeUsage
{
    [JsonPropertyName("input_tokens")]
    public int InputTokens { get; set; }

    [JsonPropertyName("output_tokens")]
    public int OutputTokens { get; set; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }
}
