namespace Hazina.AI.Agents.Tools;

/// <summary>
/// Base class for agent tools
/// </summary>
public abstract class AgentTool
{
    public string Name { get; protected set; } = string.Empty;
    public string Description { get; protected set; } = string.Empty;
    public Dictionary<string, ToolParameter> Parameters { get; protected set; } = new();

    /// <summary>
    /// Execute the tool
    /// </summary>
    public abstract Task<ToolResult> ExecuteAsync(
        Dictionary<string, object> arguments,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate arguments
    /// </summary>
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
/// Tool parameter definition
/// </summary>
public class ToolParameter
{
    public string Type { get; set; } = "string";
    public string Description { get; set; } = string.Empty;
    public bool Required { get; set; } = false;
    public object? DefaultValue { get; set; }
}

/// <summary>
/// Tool execution result
/// </summary>
public class ToolResult
{
    public bool Success { get; set; }
    public string Output { get; set; } = string.Empty;
    public string? Error { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}
