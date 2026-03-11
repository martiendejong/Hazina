namespace Hazina.Agent.API.Models;

public class AgentEvent
{
    public required string Type { get; set; } // "thinking", "tool", "output", "complete"
    public required object Data { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class ThinkingData
{
    public required string Content { get; set; }
}

public class ToolData
{
    public required string Tool { get; set; }
    public required Dictionary<string, object> Args { get; set; }
}

public class OutputData
{
    public required string Content { get; set; }
}

public class CompleteData
{
    public required string SessionId { get; set; }
    public required string FinalResult { get; set; }
    public List<string>? FilesChanged { get; set; }
    public int ToolsUsed { get; set; }
    public double Duration { get; set; }
    public string? Provider { get; set; }
    public string? Model { get; set; }
}
