namespace Hazina.LLMs.Classes.Guardrails;

/// <summary>
/// Guardrails for safe tool execution with resource limits and monitoring
/// </summary>
public class ToolGuardrails
{
    private readonly ToolGuardrailsConfig _config;

    public ToolGuardrails(ToolGuardrailsConfig? config = null)
    {
        _config = config ?? new ToolGuardrailsConfig();
    }

    /// <summary>
    /// Execute a tool with guardrails applied
    /// </summary>
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
            // Create timeout token
            using var timeoutCts = CancellationToken.None;
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts);

            if (_config.DefaultTimeout.HasValue)
            {
                linkedCts.CancelAfter(_config.DefaultTimeout.Value);
            }

            // Execute with timeout
            var executeTask = tool.Execute(messages, toolCall, linkedCts.Token);

            if (_config.DefaultTimeout.HasValue)
            {
                var completedTask = await Task.WhenAny(executeTask, Task.Delay(_config.DefaultTimeout.Value, linkedCts.Token));

                if (completedTask != executeTask)
                {
                    result.Success = false;
                    result.Error = $"Tool execution timed out after {_config.DefaultTimeout.Value.TotalSeconds}s";
                    result.TimedOut = true;
                    return result;
                }
            }

            result.Output = await executeTask;
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

            if (_config.PropagateExceptions)
            {
                throw;
            }
        }
        finally
        {
            result.EndTime = DateTime.UtcNow;
            result.Duration = result.EndTime.Value - startTime;
        }

        return result;
    }

    /// <summary>
    /// Check if tool execution should be allowed based on guardrails
    /// </summary>
    public ToolGuardrailCheck CheckGuardrails(
        HazinaChatTool tool,
        ToolExecutionContext context)
    {
        var check = new ToolGuardrailCheck { Allowed = true };

        // Check if tool is in blacklist
        if (_config.BlacklistedTools.Contains(tool.FunctionName))
        {
            check.Allowed = false;
            check.Reason = $"Tool '{tool.FunctionName}' is blacklisted";
            return check;
        }

        // Check if tool requires whitelist and is not in it
        if (_config.RequireWhitelist && !_config.WhitelistedTools.Contains(tool.FunctionName))
        {
            check.Allowed = false;
            check.Reason = $"Tool '{tool.FunctionName}' is not whitelisted";
            return check;
        }

        // Check concurrent execution limit
        if (_config.MaxConcurrentExecutions.HasValue)
        {
            // This would need to be tracked externally
            check.Warnings.Add($"Concurrent execution limit: {_config.MaxConcurrentExecutions}");
        }

        return check;
    }
}

/// <summary>
/// Configuration for tool guardrails
/// </summary>
public class ToolGuardrailsConfig
{
    /// <summary>
    /// Default timeout for tool execution
    /// </summary>
    public TimeSpan? DefaultTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Maximum number of concurrent tool executions
    /// </summary>
    public int? MaxConcurrentExecutions { get; set; }

    /// <summary>
    /// Maximum memory usage per tool execution (in bytes)
    /// </summary>
    public long? MaxMemoryBytes { get; set; }

    /// <summary>
    /// Whether to propagate exceptions or catch and return error
    /// </summary>
    public bool PropagateExceptions { get; set; } = false;

    /// <summary>
    /// Tools that are explicitly blacklisted
    /// </summary>
    public HashSet<string> BlacklistedTools { get; set; } = new();

    /// <summary>
    /// Tools that are whitelisted (if RequireWhitelist is true)
    /// </summary>
    public HashSet<string> WhitelistedTools { get; set; } = new();

    /// <summary>
    /// Whether to require tools to be whitelisted
    /// </summary>
    public bool RequireWhitelist { get; set; } = false;

    /// <summary>
    /// Whether to log tool executions
    /// </summary>
    public bool EnableLogging { get; set; } = true;

    /// <summary>
    /// Whether to collect metrics
    /// </summary>
    public bool EnableMetrics { get; set; } = true;
}

/// <summary>
/// Result of tool execution with guardrails
/// </summary>
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
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Result of guardrail check
/// </summary>
public class ToolGuardrailCheck
{
    public bool Allowed { get; set; }
    public string? Reason { get; set; }
    public List<string> Warnings { get; set; } = new();
}
