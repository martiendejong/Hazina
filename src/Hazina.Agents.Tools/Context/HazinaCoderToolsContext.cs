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

    // Continuation hooks (backwards compatible - all optional with sensible defaults)
    public Action<string, string, int>? OnToolExecuted { get; set; } = null;
    public Func<string, int, bool>? ShouldContinue { get; set; } = null;
    public string? ContinuationPrompt { get; set; } = null;
    public int MaxContinuations { get; set; } = 5;

    /// <summary>
    /// Enable permission checks for dangerous commands (default: true)
    /// </summary>
    public bool EnablePermissions { get; set; } = true;

    public HazinaCoderToolsContext(string workingDirectory)
    {
        // Core file system tools
        Tools.Add(ReadFileTool.Create(workingDirectory));
        Tools.Add(WriteFileTool.Create(workingDirectory));
        Tools.Add(EditFileTool.Create(workingDirectory));
        Tools.Add(GlobTool.Create(workingDirectory));
        Tools.Add(GrepTool.Create(workingDirectory));
        Tools.Add(ListDirectoryTool.Create(workingDirectory));

        // Execution tools - with permission checking wrapper
        Tools.Add(SecureBashTool.Create(workingDirectory, () => EnablePermissions));

        // Git tools
        Tools.Add(GitStatusTool.Create(workingDirectory));

        // Web tools
        Tools.Add(WebFetchTool.Create(workingDirectory));
        Tools.Add(WebSearchTool.Create(workingDirectory));

        // Task management tools
        Tools.Add(TodoWriteTool.Create(workingDirectory));

        // User interaction tools
        Tools.Add(AskUserQuestionTool.Create(workingDirectory));

        // Background task tools
        Tools.Add(BackgroundBashTool.Create(workingDirectory));
        Tools.Add(TaskOutputTool.Create(workingDirectory));

        // Notebook editing
        Tools.Add(NotebookEditTool.Create(workingDirectory));

        // Plan mode tools
        Tools.Add(PlanModeTool.CreateEnterPlanMode(workingDirectory));
        Tools.Add(PlanModeTool.CreateExitPlanMode(workingDirectory));
    }

    public void Add(HazinaChatTool tool)
    {
        Tools.Add(tool);
    }
}
