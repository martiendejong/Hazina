namespace Hazina.LLMs;

public class ToolsContext : IToolsContext
{
    public List<HazinaChatTool> Tools { get; set; } = new List<HazinaChatTool>();
    public Action<string, string, string>? SendMessage { get; set; }
    public string? ProjectId { get; set; } = null;
    public Action<string, int, int, string>? OnTokensUsed { get; set; } = null;

    // Continuation hooks (backwards compatible - all optional with sensible defaults)
    public Action<string, string, int>? OnToolExecuted { get; set; } = null;
    public Func<string, int, bool>? ShouldContinue { get; set; } = null;
    public string? ContinuationPrompt { get; set; } = null;
    public int MaxContinuations { get; set; } = 5;

    public void Add(HazinaChatTool info)
    {
        Tools.Add(info);
    }
}
