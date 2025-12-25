
public class ToolsContext : IToolsContext
{
    public List<HazinaChatTool> Tools { get; set; } = new List<HazinaChatTool>();
    public Action<string, string, string>? SendMessage { get; set; }
    public string? ProjectId { get; set; } = null;
    public Action<string, int, int, string>? OnTokensUsed { get; set; } = null;

    public void Add(HazinaChatTool info)
    {
        Tools.Add(info);
    }
}
