namespace Hazina.LLMs;

public interface IToolsContext
{
    List<HazinaChatTool> Tools { get; set; }
    Action<string, string, string>? SendMessage { get; set; }
    string? ProjectId { get; set; }
    Action<string, int, int, string>? OnTokensUsed { get; set; }

    /// <summary>
    /// Optional callback invoked after each tool execution.
    /// Parameters: toolName, toolResult, turnNumber
    /// </summary>
    Action<string, string, int>? OnToolExecuted { get; set; }

    /// <summary>
    /// Optional callback to determine if the agentic loop should continue after a non-tool response.
    /// Parameters: currentResponse, turnNumber
    /// Returns: true to inject continuation prompt, false to stop
    /// Default behavior (when null): stop on non-tool response
    /// </summary>
    Func<string, int, bool>? ShouldContinue { get; set; }

    /// <summary>
    /// Optional prompt to inject when ShouldContinue returns true.
    /// This prompts the model to continue working on the task.
    /// </summary>
    string? ContinuationPrompt { get; set; }

    /// <summary>
    /// Maximum number of continuation prompts to inject (prevents infinite loops).
    /// Default: 5
    /// </summary>
    int MaxContinuations { get; set; }

    void Add(HazinaChatTool info);
}
