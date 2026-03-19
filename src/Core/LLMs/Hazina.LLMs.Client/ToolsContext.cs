namespace Hazina.LLMs;

/// <summary>
/// Default implementation of <see cref="IToolsContext"/> for attaching tools and agentic-loop hooks to LLM requests.
/// </summary>
/// <remarks>
/// All callback properties are optional and default to <see langword="null"/>.
/// The agentic loop stops after a non-tool response unless <see cref="ShouldContinue"/> is configured.
/// </remarks>
/// <example>
/// <code>
/// var ctx = new ToolsContext { ProjectId = "my-project" };
/// ctx.Add(new HazinaChatTool { Name = "get_weather", Description = "Fetch current weather", ... });
/// ctx.OnTokensUsed = (model, input, output, op) =>
///     Console.WriteLine($"[{op}] {model}: {input}in / {output}out tokens");
///
/// var response = await llm.GetResponse(messages, HazinaChatResponseFormat.Text, ctx, null, ct);
/// </code>
/// </example>
public class ToolsContext : IToolsContext
{
    /// <inheritdoc/>
    public List<HazinaChatTool> Tools { get; set; } = new List<HazinaChatTool>();

    /// <inheritdoc/>
    public Action<string, string, string>? SendMessage { get; set; }

    /// <inheritdoc/>
    public string? ProjectId { get; set; } = null;

    /// <inheritdoc/>
    public Action<string, int, int, string>? OnTokensUsed { get; set; } = null;

    /// <inheritdoc/>
    public Action<string, string, int>? OnToolExecuted { get; set; } = null;

    /// <inheritdoc/>
    public Func<string, int, bool>? ShouldContinue { get; set; } = null;

    /// <inheritdoc/>
    public string? ContinuationPrompt { get; set; } = null;

    /// <inheritdoc/>
    public int MaxContinuations { get; set; } = 5;

    /// <inheritdoc/>
    public void Add(HazinaChatTool info)
    {
        Tools.Add(info);
    }
}
