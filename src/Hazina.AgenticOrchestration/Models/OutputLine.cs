namespace Hazina.AgenticOrchestration.Models;

/// <summary>
/// Represents a single line of output from a Claude instance
/// </summary>
public class OutputLine
{
    public int LineNumber { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string SessionId { get; set; } = string.Empty;
}
