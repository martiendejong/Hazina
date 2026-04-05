using System.Text;
using System.Text.Json;

namespace Hazina.LLMs.Streaming;

/// <summary>
/// Assembles streaming tool call data with error detection and validation.
/// </summary>
/// <remarks>
/// Handles the complexity of assembling tool calls from streaming chunks:
/// - Partial JSON assembly with validation
/// - Error surfaces for malformed data
/// - Multiple concurrent tool calls
/// - State management across chunks
/// </remarks>
public class StreamingToolCallAssembler
{
    private readonly Dictionary<string, ToolCallState> _toolCalls = new();

    /// <summary>
    /// Represents the assembly state of a tool call.
    /// </summary>
    private class ToolCallState
    {
        public string ToolCallId { get; set; } = string.Empty;
        public string FunctionName { get; set; } = string.Empty;
        public StringBuilder Arguments { get; } = new();
        public int ChunkCount { get; set; }
        public bool IsComplete { get; set; }
        public List<string> Errors { get; } = new();
    }

    /// <summary>
    /// Represents a completed or in-progress tool call.
    /// </summary>
    public class ToolCall
    {
        public required string ToolCallId { get; init; }
        public required string FunctionName { get; init; }
        public required string Arguments { get; init; }
        public bool IsComplete { get; init; }
        public int ChunkCount { get; init; }
        public List<string> Errors { get; init; } = new();
    }

    /// <summary>
    /// Adds a tool call chunk to the assembler.
    /// </summary>
    /// <param name="toolCallId">Unique identifier for this tool call.</param>
    /// <param name="functionName">Function name (may be partial or null for subsequent chunks).</param>
    /// <param name="argumentsChunk">Partial JSON arguments chunk.</param>
    /// <returns>The current state of the tool call, or null if invalid.</returns>
    public ToolCall? AddChunk(string? toolCallId, string? functionName, string? argumentsChunk)
    {
        // Validate tool call ID
        if (string.IsNullOrEmpty(toolCallId))
        {
            return CreateErrorToolCall("MISSING_TOOL_CALL_ID", "Tool call ID is missing");
        }

        // Get or create tool call state
        if (!_toolCalls.TryGetValue(toolCallId, out var state))
        {
            if (string.IsNullOrEmpty(functionName))
            {
                return CreateErrorToolCall(toolCallId, "First chunk must include function name");
            }

            state = new ToolCallState
            {
                ToolCallId = toolCallId,
                FunctionName = functionName
            };
            _toolCalls[toolCallId] = state;
        }

        // Update function name if provided (shouldn't change after first chunk)
        if (!string.IsNullOrEmpty(functionName) && functionName != state.FunctionName)
        {
            state.Errors.Add($"Function name changed from '{state.FunctionName}' to '{functionName}'");
        }

        // Append arguments chunk
        if (!string.IsNullOrEmpty(argumentsChunk))
        {
            state.Arguments.Append(argumentsChunk);
            state.ChunkCount++;
        }

        // Return current state
        return new ToolCall
        {
            ToolCallId = state.ToolCallId,
            FunctionName = state.FunctionName,
            Arguments = state.Arguments.ToString(),
            IsComplete = state.IsComplete,
            ChunkCount = state.ChunkCount,
            Errors = new List<string>(state.Errors)
        };
    }

    /// <summary>
    /// Marks a tool call as complete and validates its JSON.
    /// </summary>
    /// <param name="toolCallId">The tool call ID to complete.</param>
    /// <returns>The completed tool call with validation results.</returns>
    public ToolCall? CompleteToolCall(string toolCallId)
    {
        if (!_toolCalls.TryGetValue(toolCallId, out var state))
        {
            return CreateErrorToolCall(toolCallId, "Tool call not found");
        }

        state.IsComplete = true;

        // Validate JSON
        var arguments = state.Arguments.ToString();
        if (!string.IsNullOrWhiteSpace(arguments))
        {
            if (!IsValidJson(arguments))
            {
                state.Errors.Add($"Invalid JSON: {arguments.Substring(0, Math.Min(100, arguments.Length))}...");
            }
        }
        else
        {
            // Empty arguments are valid (no parameters)
            state.Arguments.Append("{}");
        }

        return new ToolCall
        {
            ToolCallId = state.ToolCallId,
            FunctionName = state.FunctionName,
            Arguments = state.Arguments.ToString(),
            IsComplete = true,
            ChunkCount = state.ChunkCount,
            Errors = new List<string>(state.Errors)
        };
    }

    /// <summary>
    /// Gets all completed tool calls.
    /// </summary>
    public IEnumerable<ToolCall> GetCompletedToolCalls()
    {
        return _toolCalls.Values
            .Where(s => s.IsComplete)
            .Select(s => new ToolCall
            {
                ToolCallId = s.ToolCallId,
                FunctionName = s.FunctionName,
                Arguments = s.Arguments.ToString(),
                IsComplete = s.IsComplete,
                ChunkCount = s.ChunkCount,
                Errors = new List<string>(s.Errors)
            });
    }

    /// <summary>
    /// Gets all in-progress tool calls.
    /// </summary>
    public IEnumerable<ToolCall> GetInProgressToolCalls()
    {
        return _toolCalls.Values
            .Where(s => !s.IsComplete)
            .Select(s => new ToolCall
            {
                ToolCallId = s.ToolCallId,
                FunctionName = s.FunctionName,
                Arguments = s.Arguments.ToString(),
                IsComplete = s.IsComplete,
                ChunkCount = s.ChunkCount,
                Errors = new List<string>(s.Errors)
            });
    }

    /// <summary>
    /// Clears all tool call state.
    /// </summary>
    public void Clear()
    {
        _toolCalls.Clear();
    }

    /// <summary>
    /// Gets diagnostic information about all tool calls.
    /// </summary>
    public string GetDiagnostics()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Total tool calls: {_toolCalls.Count}");
        sb.AppendLine($"Completed: {_toolCalls.Values.Count(s => s.IsComplete)}");
        sb.AppendLine($"In progress: {_toolCalls.Values.Count(s => !s.IsComplete)}");
        sb.AppendLine($"With errors: {_toolCalls.Values.Count(s => s.Errors.Any())}");

        foreach (var (id, state) in _toolCalls)
        {
            sb.AppendLine($"\nTool Call: {id}");
            sb.AppendLine($"  Function: {state.FunctionName}");
            sb.AppendLine($"  Chunks: {state.ChunkCount}");
            sb.AppendLine($"  Complete: {state.IsComplete}");
            sb.AppendLine($"  Args length: {state.Arguments.Length}");

            if (state.Errors.Any())
            {
                sb.AppendLine($"  Errors:");
                foreach (var error in state.Errors)
                {
                    sb.AppendLine($"    - {error}");
                }
            }
        }

        return sb.ToString();
    }

    private static bool IsValidJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static ToolCall CreateErrorToolCall(string toolCallId, string error)
    {
        return new ToolCall
        {
            ToolCallId = toolCallId,
            FunctionName = "ERROR",
            Arguments = "{}",
            IsComplete = false,
            ChunkCount = 0,
            Errors = new List<string> { error }
        };
    }
}
