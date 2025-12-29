using Hazina.LLMs.GoogleADK.Core;

namespace Hazina.LLMs.GoogleADK.DeveloperUI.Models;

/// <summary>
/// Debug information for an agent execution
/// </summary>
public class DebugInfo
{
    public string ExecutionId { get; set; } = Guid.NewGuid().ToString();
    public string AgentId { get; set; } = string.Empty;
    public string Input { get; set; } = string.Empty;
    public string Output { get; set; } = string.Empty;
    public AgentStatus Status { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public TimeSpan? Duration { get; set; }
    public List<DebugStep> Steps { get; set; } = new();
    public Dictionary<string, object> State { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Individual step in agent execution
/// </summary>
public class DebugStep
{
    public int StepNumber { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> Data { get; set; } = new();
    public TimeSpan Duration { get; set; }
}

/// <summary>
/// Performance profile for an agent
/// </summary>
public class PerformanceProfile
{
    public string AgentId { get; set; } = string.Empty;
    public int TotalExecutions { get; set; }
    public TimeSpan AverageExecutionTime { get; set; }
    public TimeSpan MinExecutionTime { get; set; }
    public TimeSpan MaxExecutionTime { get; set; }
    public double SuccessRate { get; set; }
    public Dictionary<string, TimeSpan> StepAverages { get; set; } = new();
}
