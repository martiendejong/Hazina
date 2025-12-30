using Hazina.Agents.Tools.Execution;
using Hazina.Agents.Tools.FileSystem;

namespace Hazina.Agents.Tools.Context;

/// <summary>
/// Tools context for Claude Code - provides all file and command execution tools
/// </summary>
public class ClaudeCodeToolsContext : IToolsContext
{
    public List<HazinaChatTool> Tools { get; set; } = new();
    public Action<string, string, string>? SendMessage { get; set; }
    public string? ProjectId { get; set; }
    public Action<string, int, int, string>? OnTokensUsed { get; set; }

    public ClaudeCodeToolsContext(string workingDirectory)
    {
        // Register all core tools
        Tools.Add(ReadFileTool.Create(workingDirectory));
        Tools.Add(WriteFileTool.Create(workingDirectory));
        Tools.Add(EditFileTool.Create(workingDirectory));
        Tools.Add(BashTool.Create(workingDirectory));
        Tools.Add(GlobTool.Create(workingDirectory));
        Tools.Add(GrepTool.Create(workingDirectory));
    }

    public void Add(HazinaChatTool tool)
    {
        Tools.Add(tool);
    }
}
