namespace Hazina.AgenticOrchestration.Models;

/// <summary>
/// Request from Claude instance for user input
/// </summary>
public class AwaitInputRequest
{
    public string Type { get; set; } = string.Empty; // question, confirmation, choice
    public string Prompt { get; set; } = string.Empty;
    public List<string> Options { get; set; } = new();
}

/// <summary>
/// User response to an interaction request
/// </summary>
public class UserResponse
{
    public string Response { get; set; } = string.Empty;
    public DateTime RespondedAt { get; set; }
    public string RespondedBy { get; set; } = string.Empty;
}

/// <summary>
/// Pending interaction waiting for user response
/// </summary>
public class PendingInteraction
{
    public long Id { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public string AgentName { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public List<string> Options { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// User's response to submit
/// </summary>
public class RespondRequest
{
    public string Response { get; set; } = string.Empty;
}
