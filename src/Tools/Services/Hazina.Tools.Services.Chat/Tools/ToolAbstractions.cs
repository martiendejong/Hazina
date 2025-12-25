using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Hazina.Tools.Services.Chat.Tools;

/// <summary>
/// Minimal tool call contract used by the chat orchestration layer.
/// </summary>
public interface IToolCall
{
    string FunctionName { get; }
    string Arguments { get; }
}

/// <summary>
/// Minimal tool definition contract for exposing available tools.
/// </summary>
public interface IToolDefinition
{
    string Name { get; set; }
    string Description { get; set; }
    JsonElement Parameters { get; set; }
}

/// <summary>
/// Minimal result contract returned by tool execution.
/// </summary>
public interface IToolResult
{
    bool Success { get; set; }
    string Error { get; set; }
    int TokensUsed { get; set; }
    object? Result { get; set; }
}

/// <summary>
/// Contract for executing tools defined by the chat service.
/// </summary>
public interface IToolExecutor
{
    Task<IToolResult> ExecuteAsync(string toolName, string argumentsJson, string context, CancellationToken cancellationToken = default);
    List<IToolDefinition> GetToolDefinitions();
}

/// <summary>
/// Basic tool call implementation used for orchestration stubs.
/// </summary>
public sealed class ToolCall : IToolCall
{
    public required string FunctionName { get; init; }
    public required string Arguments { get; init; }
}

/// <summary>
/// Basic tool definition implementation.
/// </summary>
public sealed class ToolDefinition : IToolDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public JsonElement Parameters { get; set; }
}

/// <summary>
/// Basic tool result implementation.
/// </summary>
public sealed class ToolResult : IToolResult
{
    public bool Success { get; set; }
    public string Error { get; set; } = string.Empty;
    public int TokensUsed { get; set; }
    public object? Result { get; set; }
}
