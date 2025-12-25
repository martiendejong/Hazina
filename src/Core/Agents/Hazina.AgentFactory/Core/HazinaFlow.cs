public class HazinaFlow
{
    public string Name { get; set; }
    public List<string> CallsAgents { get; set; }
    public HazinaFlow(string name, List<string> callsAgents)
    {
        Name = name;
        CallsAgents = callsAgents;
    }
}