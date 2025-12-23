public class DevGPTFlow
{
    public string Name { get; set; }
    public List<string> CallsAgents { get; set; }
    public DevGPTFlow(string name, List<string> callsAgents)
    {
        Name = name;
        CallsAgents = callsAgents;
    }
}