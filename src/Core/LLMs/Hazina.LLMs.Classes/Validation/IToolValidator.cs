/// <summary>
/// Interface for validating tool calls before execution
/// </summary>
public interface IToolValidator
{
    Task<ToolValidationResult> ValidateAsync(
        HazinaChatTool tool,
        HazinaChatToolCall toolCall,
        ToolExecutionContext context,
        CancellationToken cancellationToken = default);
}

public class ToolValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();

    public static ToolValidationResult Success() => new() { IsValid = true };
    
    public static ToolValidationResult Failure(params string[] errors)
    {
        return new ToolValidationResult { IsValid = false, Errors = errors.ToList() };
    }
}

public class ToolExecutionContext
{
    public string? UserId { get; set; }
    public string? SessionId { get; set; }
    public TimeSpan? Timeout { get; set; }
    public bool IsReadOnly { get; set; }
    public bool IsDryRun { get; set; }
    public ToolPermissionLevel PermissionLevel { get; set; } = ToolPermissionLevel.Standard;
}

public enum ToolPermissionLevel
{
    ReadOnly = 0,
    Standard = 1,
    Elevated = 2,
    Admin = 3
}
