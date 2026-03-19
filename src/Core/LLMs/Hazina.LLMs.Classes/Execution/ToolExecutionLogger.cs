using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Hazina.LLMs.Execution;

/// <summary>
/// Structured logger for tool execution with correlation tracking
/// </summary>
public class ToolExecutionLogger
{
    private readonly ILogger _logger;
    private readonly string _category;

    public ToolExecutionLogger(ILogger logger, string category = "ToolExecution")
    {
        _logger = logger;
        _category = category;
    }

    /// <summary>
    /// Log the start of tool execution
    /// </summary>
    public void LogExecutionStart(
        ToolExecutionContext context,
        Dictionary<string, object>? arguments = null)
    {
        using var scope = CreateCorrelationScope(context);

        _logger.LogInformation(
            "[{Category}] Tool execution started: {ToolName} | ExecutionId: {ExecutionId} | CorrelationId: {CorrelationId}",
            _category,
            context.ToolName,
            context.ExecutionId,
            context.CorrelationId);

        if (arguments != null && _logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "[{Category}] Tool arguments: {@Arguments}",
                _category,
                arguments);
        }
    }

    /// <summary>
    /// Log successful tool execution
    /// </summary>
    public void LogExecutionSuccess(
        ToolExecutionContext context,
        TimeSpan duration,
        object? result = null)
    {
        using var scope = CreateCorrelationScope(context);

        _logger.LogInformation(
            "[{Category}] Tool execution succeeded: {ToolName} | Duration: {Duration}ms | ExecutionId: {ExecutionId}",
            _category,
            context.ToolName,
            duration.TotalMilliseconds,
            context.ExecutionId);

        if (result != null && _logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "[{Category}] Tool result: {@Result}",
                _category,
                result);
        }
    }

    /// <summary>
    /// Log failed tool execution
    /// </summary>
    public void LogExecutionFailure(
        ToolExecutionContext context,
        Exception exception,
        TimeSpan duration)
    {
        using var scope = CreateCorrelationScope(context);

        _logger.LogError(
            exception,
            "[{Category}] Tool execution failed: {ToolName} | Duration: {Duration}ms | ExecutionId: {ExecutionId} | Error: {ErrorMessage}",
            _category,
            context.ToolName,
            duration.TotalMilliseconds,
            context.ExecutionId,
            exception.Message);
    }

    /// <summary>
    /// Log tool validation failure
    /// </summary>
    public void LogValidationFailure(
        ToolExecutionContext context,
        IEnumerable<string> errors)
    {
        using var scope = CreateCorrelationScope(context);

        _logger.LogWarning(
            "[{Category}] Tool validation failed: {ToolName} | ExecutionId: {ExecutionId} | Errors: {Errors}",
            _category,
            context.ToolName,
            context.ExecutionId,
            string.Join("; ", errors));
    }

    /// <summary>
    /// Log custom event with correlation context
    /// </summary>
    public void LogEvent(
        ToolExecutionContext context,
        LogLevel level,
        string message,
        params object[] args)
    {
        using var scope = CreateCorrelationScope(context);

        _logger.Log(level, $"[{_category}] {message}", args);
    }

    /// <summary>
    /// Create a logging scope with correlation properties
    /// </summary>
    private IDisposable CreateCorrelationScope(ToolExecutionContext context)
    {
        var properties = context.GetCorrelationProperties();
        return _logger.BeginScope(properties);
    }

    /// <summary>
    /// Create an Activity for distributed tracing
    /// </summary>
    public static Activity? StartActivity(
        ToolExecutionContext context,
        ActivitySource activitySource,
        ActivityKind kind = ActivityKind.Internal)
    {
        var activity = activitySource.StartActivity(
            $"Tool: {context.ToolName}",
            kind);

        if (activity != null)
        {
            activity.SetTag("tool.name", context.ToolName);
            activity.SetTag("tool.execution_id", context.ExecutionId);
            activity.SetTag("tool.correlation_id", context.CorrelationId);
            activity.SetTag("tool.user_id", context.UserId);
            activity.SetTag("tool.session_id", context.SessionId);

            if (context.ParentId != null)
            {
                activity.SetTag("tool.parent_id", context.ParentId);
            }
        }

        return activity;
    }
}

/// <summary>
/// Extension methods for ILogger
/// </summary>
public static class LoggerExtensions
{
    /// <summary>
    /// Create a ToolExecutionLogger from ILogger
    /// </summary>
    public static ToolExecutionLogger ForToolExecution(this ILogger logger, string? category = null)
    {
        return new ToolExecutionLogger(logger, category ?? "ToolExecution");
    }

    /// <summary>
    /// Log with correlation context
    /// </summary>
    public static IDisposable BeginToolExecutionScope(
        this ILogger logger,
        ToolExecutionContext context)
    {
        return logger.BeginScope(context.GetCorrelationProperties());
    }
}
