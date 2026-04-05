namespace Hazina.LLMs.Streaming;

/// <summary>
/// Validates streaming chunks and provides detailed error information.
/// </summary>
public class StreamChunkValidator
{
    /// <summary>
    /// Validation result for a streaming chunk.
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; init; }
        public List<string> Errors { get; init; } = new();
        public List<string> Warnings { get; init; } = new();

        public static ValidationResult Success() => new() { IsValid = true };

        public static ValidationResult Failure(params string[] errors)
        {
            return new()
            {
                IsValid = false,
                Errors = new List<string>(errors)
            };
        }

        public ValidationResult WithWarning(string warning)
        {
            Warnings.Add(warning);
            return this;
        }
    }

    /// <summary>
    /// Validates content chunk data.
    /// </summary>
    public ValidationResult ValidateContentChunk(string? content, int chunkIndex)
    {
        if (content == null)
        {
            return ValidationResult.Success();
        }

        var result = ValidationResult.Success();

        // Check for common streaming issues
        if (content.Contains('\0'))
        {
            result = result.WithWarning($"Chunk {chunkIndex} contains null characters");
        }

        // Check for extremely large chunks (potential buffering issue)
        if (content.Length > 10000)
        {
            result = result.WithWarning($"Chunk {chunkIndex} is unusually large ({content.Length} chars)");
        }

        return result;
    }

    /// <summary>
    /// Validates tool call chunk data.
    /// </summary>
    public ValidationResult ValidateToolCallChunk(
        string? toolCallId,
        string? functionName,
        string? arguments,
        bool isFirstChunk)
    {
        var errors = new List<string>();

        // First chunk must have tool call ID and function name
        if (isFirstChunk)
        {
            if (string.IsNullOrEmpty(toolCallId))
            {
                errors.Add("First tool call chunk must include tool call ID");
            }

            if (string.IsNullOrEmpty(functionName))
            {
                errors.Add("First tool call chunk must include function name");
            }
        }
        else
        {
            if (string.IsNullOrEmpty(toolCallId))
            {
                errors.Add("Tool call ID missing in continuation chunk");
            }
        }

        // Validate arguments if present
        if (!string.IsNullOrEmpty(arguments))
        {
            var argResult = ValidatePartialJson(arguments);
            if (!argResult.IsValid)
            {
                errors.AddRange(argResult.Errors);
            }
        }

        return errors.Any()
            ? ValidationResult.Failure(errors.ToArray())
            : ValidationResult.Success();
    }

    /// <summary>
    /// Validates partial JSON for common issues.
    /// </summary>
    public ValidationResult ValidatePartialJson(string json)
    {
        var warnings = new List<string>();

        // Check for unterminated strings (common in streaming)
        var quoteCount = json.Count(c => c == '"');
        if (quoteCount % 2 != 0)
        {
            warnings.Add("Partial JSON contains unterminated string");
        }

        // Check for unmatched braces
        var openBraces = json.Count(c => c == '{');
        var closeBraces = json.Count(c => c == '}');
        if (openBraces > closeBraces)
        {
            warnings.Add($"Partial JSON has {openBraces - closeBraces} unmatched opening braces");
        }

        // Check for control characters
        if (json.Any(c => char.IsControl(c) && c != '\n' && c != '\r' && c != '\t'))
        {
            warnings.Add("Partial JSON contains unexpected control characters");
        }

        var result = ValidationResult.Success();
        foreach (var warning in warnings)
        {
            result = result.WithWarning(warning);
        }

        return result;
    }

    /// <summary>
    /// Validates finish reason.
    /// </summary>
    public ValidationResult ValidateFinishReason(string? finishReason, bool hasContent, bool hasToolCalls)
    {
        if (string.IsNullOrEmpty(finishReason))
        {
            return ValidationResult.Success();
        }

        var warnings = new List<string>();

        // Check for mismatched finish reasons
        if (finishReason == "tool_calls" && !hasToolCalls)
        {
            warnings.Add("Finish reason is 'tool_calls' but no tool calls were received");
        }

        if (finishReason == "stop" && !hasContent && !hasToolCalls)
        {
            warnings.Add("Finish reason is 'stop' but no content or tool calls were received");
        }

        if (finishReason == "length" && !hasContent)
        {
            warnings.Add("Finish reason is 'length' (max tokens) but no content was received");
        }

        var result = ValidationResult.Success();
        foreach (var warning in warnings)
        {
            result = result.WithWarning(warning);
        }

        return result;
    }
}
