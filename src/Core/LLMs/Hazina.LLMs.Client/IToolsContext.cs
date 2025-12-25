
public interface IToolsContext
{
    List<HazinaChatTool> Tools { get; set; }
    Action<string, string, string>? SendMessage { get; set; }
    string? ProjectId { get; set; }
    Action<string, int, int, string>? OnTokensUsed { get; set; }

    void Add(HazinaChatTool info);
}
