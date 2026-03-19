namespace Hazina.AI.Agents.Tools;

/// <summary>
/// Base class for tools that an <see cref="Hazina.AI.Agents.Core.Agent"/> can invoke during task execution.
/// </summary>
/// <remarks>
/// <para>
/// Subclass <see cref="AgentTool"/> to create custom tools. Set <see cref="Name"/>,
/// <see cref="Description"/>, and populate <see cref="Parameters"/> in the constructor,
/// then implement <see cref="ExecuteAsync"/>.
/// </para>
/// <para>
/// The agent injects tool names and parameter descriptions into its system prompt automatically.
/// </para>
/// </remarks>
/// <example>
/// Creating a custom web-search tool:
/// <code>
/// public class WebSearchTool : AgentTool
/// {
///     public WebSearchTool()
///     {
///         Name        = "web_search";
///         Description = "Search the web for up-to-date information.";
///         Parameters["query"] = new ToolParameter
///         {
///             Type        = "string",
///             Description = "The search query.",
///             Required    = true
///         };
///     }
///
///     public override async Task&lt;ToolResult&gt; ExecuteAsync(
///         Dictionary&lt;string, object&gt; arguments,
///         CancellationToken cancellationToken = default)
///     {
///         if (!ValidateArguments(arguments, out var error))
///             return new ToolResult { Success = false, Error = error };
///
///         var query = arguments["query"].ToString()!;
///         var results = await SearchAsync(query, cancellationToken);
///         return new ToolResult { Success = true, Output = results };
///     }
/// }
/// </code>
/// </example>
public abstract class AgentTool
{
    /// <summary>Gets the unique name used by the agent to call this tool (e.g., <c>"web_search"</c>).</summary>
    public string Name { get; protected set; } = string.Empty;

    /// <summary>Gets a human-readable description injected into the agent system prompt.</summary>
    public string Description { get; protected set; } = string.Empty;

    /// <summary>Gets the parameter definitions keyed by parameter name.</summary>
    public Dictionary<string, ToolParameter> Parameters { get; protected set; } = new();

    /// <summary>
    /// Executes the tool with the supplied arguments.
    /// </summary>
    /// <param name="arguments">Named arguments parsed from the LLM response.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// A <see cref="ToolResult"/> with <see cref="ToolResult.Success"/> and either
    /// <see cref="ToolResult.Output"/> (on success) or <see cref="ToolResult.Error"/> (on failure).
    /// </returns>
    public abstract Task<ToolResult> ExecuteAsync(
        Dictionary<string, object> arguments,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that all required parameters are present in <paramref name="arguments"/>.
    /// </summary>
    /// <param name="arguments">Arguments to validate.</param>
    /// <param name="error">Populated with a descriptive error message when validation fails.</param>
    /// <returns><see langword="true"/> if all required parameters are present; otherwise <see langword="false"/>.</returns>
    protected virtual bool ValidateArguments(Dictionary<string, object> arguments, out string? error)
    {
        error = null;

        foreach (var param in Parameters)
        {
            if (param.Value.Required && !arguments.ContainsKey(param.Key))
            {
                error = $"Missing required parameter: {param.Key}";
                return false;
            }
        }

        return true;
    }
}

/// <summary>
/// Describes a single input parameter accepted by an <see cref="AgentTool"/>.
/// </summary>
public class ToolParameter
{
    /// <summary>Gets or sets the JSON schema type name (e.g., <c>"string"</c>, <c>"integer"</c>, <c>"boolean"</c>). Default: <c>"string"</c>.</summary>
    public string Type { get; set; } = "string";

    /// <summary>Gets or sets a human-readable description of the parameter, injected into the agent system prompt.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the parameter must be supplied by the LLM. Default: <see langword="false"/>.</summary>
    public bool Required { get; set; } = false;

    /// <summary>Gets or sets the fallback value used when the parameter is omitted. Default: <see langword="null"/>.</summary>
    public object? DefaultValue { get; set; }
}

/// <summary>
/// Result returned by <see cref="AgentTool.ExecuteAsync"/>.
/// </summary>
public class ToolResult
{
    /// <summary>Gets or sets a value indicating whether the tool executed successfully.</summary>
    public bool Success { get; set; }

    /// <summary>Gets or sets the tool output fed back to the LLM as the tool result.</summary>
    public string Output { get; set; } = string.Empty;

    /// <summary>Gets or sets the error message when <see cref="Success"/> is <see langword="false"/>.</summary>
    public string? Error { get; set; }

    /// <summary>Gets or sets optional structured metadata about the execution (timing, source, etc.).</summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}
