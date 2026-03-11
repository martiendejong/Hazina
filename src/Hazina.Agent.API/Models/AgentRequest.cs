namespace Hazina.Agent.API.Models;

public class AgentRequest
{
    public required string Instruction { get; set; }
    public string? Provider { get; set; } // "openai" (default), "claude", "gemini", "auto"
    public string? Model { get; set; } // "gpt-4o", "claude-sonnet-4.5", etc.
    public AgentContext? Context { get; set; }
    public AgentOptions? Options { get; set; }
}

public class AgentContext
{
    public string? Project { get; set; }
    public string? WorkingDirectory { get; set; }
    public string? SessionId { get; set; } // null = create new
    public Dictionary<string, object>? Metadata { get; set; }
}

public class AgentOptions
{
    public bool Autonomous { get; set; } = false;
    public bool StreamResponse { get; set; } = true;
    public int MaxTokens { get; set; } = 4000;
    public double Temperature { get; set; } = 0.7;
}
