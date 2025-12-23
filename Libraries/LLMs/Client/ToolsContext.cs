
public class ToolsContext : IToolsContext
{
    public List<DevGPTChatTool> Tools { get; set; } = new List<DevGPTChatTool>();
    public Action<string, string, string>? SendMessage { get; set; }
    public string? ProjectId { get; set; } = null;
    public Action<string, int, int, string>? OnTokensUsed { get; set; } = null;

    public void Add(DevGPTChatTool info)
    {
        Tools.Add(info);
    }
}
