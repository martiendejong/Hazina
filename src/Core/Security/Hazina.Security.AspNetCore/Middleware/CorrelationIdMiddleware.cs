using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace Hazina.Security.AspNetCore.Middleware;

/// <summary>
/// Middleware that adds correlation IDs to requests for distributed tracing
/// </summary>
public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;
    private readonly CorrelationIdOptions _options;

    public CorrelationIdMiddleware(
        RequestDelegate next,
        ILogger<CorrelationIdMiddleware> logger,
        CorrelationIdOptions options)
    {
        _next = next;
        _logger = logger;
        _options = options;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        string correlationId;

        // Try to get correlation ID from request header
        if (context.Request.Headers.TryGetValue(_options.HeaderName, out StringValues correlationIds) &&
            !string.IsNullOrEmpty(correlationIds.FirstOrDefault()))
        {
            correlationId = correlationIds.First()!;
            _logger.LogDebug("[CORRELATION] Using existing correlation ID: {CorrelationId}", correlationId);
        }
        else
        {
            // Generate new correlation ID
            correlationId = _options.CorrelationIdGenerator();
            _logger.LogDebug("[CORRELATION] Generated new correlation ID: {CorrelationId}", correlationId);
        }

        // Store in HttpContext items for access by other components
        context.Items[_options.ItemsKey] = correlationId;

        // Add to response header if enabled
        if (_options.IncludeInResponse)
        {
            context.Response.OnStarting(() =>
            {
                if (!context.Response.Headers.ContainsKey(_options.HeaderName))
                {
                    context.Response.Headers.Append(_options.HeaderName, correlationId);
                }
                return Task.CompletedTask;
            });
        }

        // Add correlation ID to logger scope
        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["RequestPath"] = context.Request.Path.Value ?? "",
            ["RequestMethod"] = context.Request.Method
        }))
        {
            _logger.LogInformation(
                "[CORRELATION] Request started | Method: {Method} | Path: {Path} | CorrelationId: {CorrelationId}",
                context.Request.Method,
                context.Request.Path,
                correlationId);

            await _next(context);

            _logger.LogInformation(
                "[CORRELATION] Request completed | Method: {Method} | Path: {Path} | Status: {StatusCode} | CorrelationId: {CorrelationId}",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                correlationId);
        }
    }
}

/// <summary>
/// Configuration options for correlation ID middleware
/// </summary>
public class CorrelationIdOptions
{
    /// <summary>
    /// Header name for correlation ID (default: X-Correlation-ID)
    /// </summary>
    public string HeaderName { get; set; } = "X-Correlation-ID";

    /// <summary>
    /// Key for storing correlation ID in HttpContext.Items
    /// </summary>
    public string ItemsKey { get; set; } = "CorrelationId";

    /// <summary>
    /// Whether to include correlation ID in response headers
    /// </summary>
    public bool IncludeInResponse { get; set; } = true;

    /// <summary>
    /// Function to generate correlation IDs
    /// </summary>
    public Func<string> CorrelationIdGenerator { get; set; } = () => Guid.NewGuid().ToString("N");
}

/// <summary>
/// Extension methods for accessing correlation ID
/// </summary>
public static class CorrelationIdExtensions
{
    /// <summary>
    /// Gets the correlation ID for the current request
    /// </summary>
    public static string? GetCorrelationId(this HttpContext context, string itemsKey = "CorrelationId")
    {
        return context.Items.TryGetValue(itemsKey, out var correlationId)
            ? correlationId as string
            : null;
    }
}
