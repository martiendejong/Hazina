namespace Hazina.LLMs;

/// <summary>
/// Context passed to an LLM client that enables tool/function calling and agentic loop control.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="IToolsContext"/> lets you attach callable tools to any LLM request. When the model
/// decides to call a tool, the framework executes it and feeds the result back automatically.
/// </para>
/// <para>
/// The continuation hooks (<see cref="ShouldContinue"/>, <see cref="ContinuationPrompt"/>,
/// <see cref="MaxContinuations"/>) let you keep the agentic loop running beyond a single
/// tool-call cycle without writing a custom loop yourself.
/// </para>
/// </remarks>
/// <example>
/// Basic tool registration:
/// <code>
/// var context = new ToolsContext();
/// context.Add(new HazinaChatTool { Name = "search", Description = "Search the web", ... });
///
/// var response = await llm.GetResponse(messages, HazinaChatResponseFormat.Text, context, null, ct);
/// </code>
/// </example>
public interface IToolsContext
{
    /// <summary>
    /// The list of tools (functions) available to the LLM during this request.
    /// </summary>
    List<HazinaChatTool> Tools { get; set; }

    /// <summary>
    /// Optional callback to push status messages to the caller during a multi-turn exchange.
    /// Parameters: <c>toolName</c>, <c>toolResult</c>, <c>messageText</c>.
    /// </summary>
    Action<string, string, string>? SendMessage { get; set; }

    /// <summary>
    /// Optional project identifier that scopes tool execution to a specific project context.
    /// </summary>
    string? ProjectId { get; set; }

    /// <summary>
    /// Optional callback invoked each time tokens are consumed.
    /// Parameters: <c>model</c>, <c>inputTokens</c>, <c>outputTokens</c>, <c>operationId</c>.
    /// Use this for token budget tracking or cost accounting.
    /// </summary>
    Action<string, int, int, string>? OnTokensUsed { get; set; }

    /// <summary>
    /// Optional callback invoked after each tool execution.
    /// Parameters: <c>toolName</c>, <c>toolResult</c>, <c>turnNumber</c>.
    /// </summary>
    Action<string, string, int>? OnToolExecuted { get; set; }

    /// <summary>
    /// Optional callback to determine if the agentic loop should continue after a non-tool response.
    /// Parameters: <c>currentResponse</c>, <c>turnNumber</c>.
    /// Returns <see langword="true"/> to inject a continuation prompt, <see langword="false"/> to stop.
    /// Default behavior (when <see langword="null"/>): stop on non-tool response.
    /// </summary>
    Func<string, int, bool>? ShouldContinue { get; set; }

    /// <summary>
    /// Optional prompt to inject when <see cref="ShouldContinue"/> returns <see langword="true"/>.
    /// This prompts the model to continue working on the task.
    /// </summary>
    string? ContinuationPrompt { get; set; }

    /// <summary>
    /// Maximum number of continuation prompts to inject (prevents infinite loops).
    /// Default: <c>5</c>.
    /// </summary>
    int MaxContinuations { get; set; }

    /// <summary>
    /// Adds a tool to the context.
    /// </summary>
    /// <param name="info">The tool definition to add.</param>
    void Add(HazinaChatTool info);
}
