/// <summary>
/// Guardrails for safe tool execution with resource limits
/// </summary>
public class ToolGuardrails
{
    private readonly ToolGuardrailsConfig _config;

    public ToolGuardrails(ToolGuardrailsConfig? config = null)
    {
        _config = config ?? new ToolGuardrailsConfig();
    }

    public async Task<ToolExecutionResult> ExecuteWithGuardrailsAsync(
        HazinaChatTool tool,
        HazinaChatToolCall toolCall,
        List<HazinaChatMessage> messages,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        var result = new ToolExecutionResult
        {
            ToolName = tool.FunctionName,
            StartTime = startTime
        };

        try
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (_config.DefaultTimeout.HasValue)
            {
                linkedCts.CancelAfter(_config.DefaultTimeout.Value);
            }

            result.Output = await tool.Execute(messages, toolCall, linkedCts.Token);
            result.Success = true;
        }
        catch (OperationCanceledException)
        {
            result.Success = false;
            result.Error = "Tool execution was cancelled";
            result.Cancelled = true;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
            result.Exception = ex;
            if (_config.PropagateExceptions) throw;
        }
        finally
        {
            result.EndTime = DateTime.UtcNow;
            result.Duration = result.EndTime.Value - startTime;
        }

        return result;
    }

    public ToolGuardrailCheck CheckGuardrails(HazinaChatTool tool, ToolExecutionContext context)
    {
        var check = new ToolGuardrailCheck { Allowed = true };

        if (_config.BlacklistedTools.Contains(tool.FunctionName))
        {
            check.Allowed = false;
            check.Reason = $"Tool '{tool.FunctionName}' is blacklisted";
            return check;
        }

        if (_config.RequireWhitelist && !_config.WhitelistedTools.Contains(tool.FunctionName))
        {
            check.Allowed = false;
            check.Reason = $"Tool '{tool.FunctionName}' is not whitelisted";
            return check;
        }

        return check;
    }
}

public class ToolGuardrailsConfig
{
    public TimeSpan? DefaultTimeout { get; set; } = TimeSpan.FromMinutes(5);
    public int? MaxConcurrentExecutions { get; set; }
    public bool PropagateExceptions { get; set; } = false;
    public HashSet<string> BlacklistedTools { get; set; } = new();
    public HashSet<string> WhitelistedTools { get; set; } = new();
    public bool RequireWhitelist { get; set; } = false;
}

public class ToolExecutionResult
{
    public string ToolName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Output { get; set; }
    public string? Error { get; set; }
    public Exception? Exception { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public TimeSpan? Duration { get; set; }
    public bool TimedOut { get; set; }
    public bool Cancelled { get; set; }
}

public class ToolGuardrailCheck
{
    public bool Allowed { get; set; }
    public string? Reason { get; set; }
    public List<string> Warnings { get; set; } = new();
}
