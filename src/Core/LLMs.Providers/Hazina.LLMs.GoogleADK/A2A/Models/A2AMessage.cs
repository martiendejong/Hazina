using System.Text.Json.Serialization;

namespace Hazina.LLMs.GoogleADK.A2A.Models;

/// <summary>
/// Base class for Agent-to-Agent (A2A) messages
/// </summary>
public abstract class A2AMessage
{
    [JsonPropertyName("messageId")]
    public string MessageId { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("messageType")]
    public string MessageType { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0";

    [JsonPropertyName("metadata")]
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Request message from one agent to another
/// </summary>
public class A2ARequest : A2AMessage
{
    public A2ARequest()
    {
        MessageType = "request";
    }

    [JsonPropertyName("sourceAgentId")]
    public string SourceAgentId { get; set; } = string.Empty;

    [JsonPropertyName("targetAgentId")]
    public string TargetAgentId { get; set; } = string.Empty;

    [JsonPropertyName("requestType")]
    public string RequestType { get; set; } = string.Empty;

    [JsonPropertyName("payload")]
    public object? Payload { get; set; }

    [JsonPropertyName("requiresResponse")]
    public bool RequiresResponse { get; set; } = true;

    [JsonPropertyName("timeout")]
    public TimeSpan? Timeout { get; set; }
}

/// <summary>
/// Response message from target agent to source agent
/// </summary>
public class A2AResponse : A2AMessage
{
    public A2AResponse()
    {
        MessageType = "response";
    }

    [JsonPropertyName("requestId")]
    public string RequestId { get; set; } = string.Empty;

    [JsonPropertyName("sourceAgentId")]
    public string SourceAgentId { get; set; } = string.Empty;

    [JsonPropertyName("targetAgentId")]
    public string TargetAgentId { get; set; } = string.Empty;

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("result")]
    public object? Result { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

/// <summary>
/// Notification message (one-way, no response expected)
/// </summary>
public class A2ANotification : A2AMessage
{
    public A2ANotification()
    {
        MessageType = "notification";
    }

    [JsonPropertyName("sourceAgentId")]
    public string SourceAgentId { get; set; } = string.Empty;

    [JsonPropertyName("targetAgentIds")]
    public List<string> TargetAgentIds { get; set; } = new();

    [JsonPropertyName("notificationType")]
    public string NotificationType { get; set; } = string.Empty;

    [JsonPropertyName("payload")]
    public object? Payload { get; set; }
}

/// <summary>
/// Handshake message for capability negotiation
/// </summary>
public class A2AHandshake : A2AMessage
{
    public A2AHandshake()
    {
        MessageType = "handshake";
    }

    [JsonPropertyName("agentId")]
    public string AgentId { get; set; } = string.Empty;

    [JsonPropertyName("agentName")]
    public string AgentName { get; set; } = string.Empty;

    [JsonPropertyName("capabilities")]
    public List<AgentCapability> Capabilities { get; set; } = new();

    [JsonPropertyName("protocolVersion")]
    public string ProtocolVersion { get; set; } = "1.0";
}

/// <summary>
/// Agent capability description
/// </summary>
public class AgentCapability
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("inputSchema")]
    public object? InputSchema { get; set; }

    [JsonPropertyName("outputSchema")]
    public object? OutputSchema { get; set; }

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();
}
