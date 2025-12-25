
public interface IToolsContext
{
    List<DevGPTChatTool> Tools { get; set; }
    Action<string, string, string>? SendMessage { get; set; }
    string? ProjectId { get; set; }
    Action<string, int, int, string>? OnTokensUsed { get; set; }

    void Add(DevGPTChatTool info);
}
