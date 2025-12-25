
public class AgentConfig
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public List<StoreRef> Stores { get; set; } = new();
    public List<string> Functions { get; set; } = new();
    public List<string> CallsAgents { get; set; } = new();
    public List<string> CallsFlows { get; set; } = new();
    public bool ExplicitModify { get; set; } = false;
}
