public class FlowConfig
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> CallsAgents { get; set; } = new(); // Namen van de agents voor flows
}
