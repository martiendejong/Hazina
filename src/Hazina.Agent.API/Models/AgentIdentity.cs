namespace Hazina.Agent.API.Models;

public class AgentIdentity
{
    public required string AgentId { get; set; } // "jengo-desktop", "jengo-laptop1", etc.
    public required string MachineName { get; set; }
    public required CoreIdentity Core { get; set; }
    public required InstanceState Instance { get; set; }
}

public class CoreIdentity
{
    public required string Name { get; set; } // "Jengo"
    public required List<string> Values { get; set; }
    public required List<string> Capabilities { get; set; }
    public required Dictionary<string, object> Metadata { get; set; }
}

public class InstanceState
{
    public required string CurrentProject { get; set; }
    public required string WorkingDirectory { get; set; }
    public required DateTime LastSync { get; set; }
    public required int SessionCount { get; set; }
    public required Dictionary<string, object> LocalState { get; set; }
}
