using Hazina.Agents.Tools.Execution;
using Hazina.Agents.Tools.FileSystem;
using Hazina.Agents.Tools.Additional;
using Hazina.LLMs;

namespace Hazina.Agents.Tools.Context;

/// <summary>
/// Extended tools context for HazinaCoder - includes all coding assistant tools
/// </summary>
public class HazinaCoderToolsContext : IToolsContext
{
    public List<HazinaChatTool> Tools { get; set; } = new();
    public Action<string, string, string>? SendMessage { get; set; }
    public string? ProjectId { get; set; }
    public Action<string, int, int, string>? OnTokensUsed { get; set; }

    public HazinaCoderToolsContext(string workingDirectory)
    {
        // Core file system tools
        Tools.Add(ReadFileTool.Create(workingDirectory));
        Tools.Add(WriteFileTool.Create(workingDirectory));
        Tools.Add(EditFileTool.Create(workingDirectory));
        Tools.Add(GlobTool.Create(workingDirectory));
        Tools.Add(GrepTool.Create(workingDirectory));
        Tools.Add(ListDirectoryTool.Create(workingDirectory));

        // Execution tools
        Tools.Add(BashTool.Create(workingDirectory));

        // Git tools
        Tools.Add(GitStatusTool.Create(workingDirectory));

        // Web tools
        Tools.Add(WebFetchTool.Create(workingDirectory));
    }

    public void Add(HazinaChatTool tool)
    {
        Tools.Add(tool);
    }
}
