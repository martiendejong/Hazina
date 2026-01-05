using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Net;

namespace Hazina.Security.AspNetCore.Middleware;

/// <summary>
/// Middleware that enforces rate limiting on API requests
/// </summary>
public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private readonly RateLimitingOptions _options;
    private readonly Dictionary<string, RateLimitBucket> _buckets = new();
    private readonly object _lock = new();

    public RateLimitingMiddleware(
        RequestDelegate next,
        ILogger<RateLimitingMiddleware> logger,
        RateLimitingOptions options)
    {
        _next = next;
        _logger = logger;
        _options = options;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip rate limiting for excluded paths
        if (_options.ExcludedPaths.Any(p => context.Request.Path.StartsWithSegments(p)))
        {
            await _next(context);
            return;
        }

        // Get client identifier
        var clientId = GetClientIdentifier(context);

        // Check rate limit
        if (!TryAcquireToken(clientId))
        {
            _logger.LogWarning(
                "[RATE LIMIT] Rate limit exceeded for client: {ClientId} | Path: {Path}",
                MaskClientId(clientId),
                context.Request.Path);

            context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
            context.Response.Headers.Append("Retry-After", _options.RetryAfterSeconds.ToString());
            context.Response.Headers.Append("X-RateLimit-Limit", _options.MaxRequests.ToString());
            context.Response.Headers.Append("X-RateLimit-Remaining", "0");

            await context.Response.WriteAsJsonAsync(new
            {
                error = "Rate limit exceeded",
                message = $"Too many requests. Please try again after {_options.RetryAfterSeconds} seconds.",
                retryAfter = _options.RetryAfterSeconds
            });

            return;
        }

        // Add rate limit headers
        var bucket = GetBucket(clientId);
        context.Response.Headers.Append("X-RateLimit-Limit", _options.MaxRequests.ToString());
        context.Response.Headers.Append("X-RateLimit-Remaining", bucket.RemainingTokens.ToString());
        context.Response.Headers.Append("X-RateLimit-Reset", bucket.ResetTime.ToUnixTimeSeconds().ToString());

        await _next(context);
    }

    private string GetClientIdentifier(HttpContext context)
    {
        // Use custom identifier function if provided
        if (_options.ClientIdentifierFunc != null)
        {
            return _options.ClientIdentifierFunc(context);
        }

        // Default: use IP address
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        // Check for forwarded IP (behind proxy/load balancer)
        if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
        {
            ip = forwardedFor.ToString().Split(',').FirstOrDefault()?.Trim() ?? ip;
        }

        return ip;
    }

    private bool TryAcquireToken(string clientId)
    {
        lock (_lock)
        {
            var bucket = GetBucket(clientId);

            // Refill tokens if needed
            var now = DateTimeOffset.UtcNow;
            if (now >= bucket.ResetTime)
            {
                bucket.RemainingTokens = _options.MaxRequests;
                bucket.ResetTime = now.AddSeconds(_options.WindowSeconds);
            }

            // Check if tokens available
            if (bucket.RemainingTokens > 0)
            {
                bucket.RemainingTokens--;
                return true;
            }

            return false;
        }
    }

    private RateLimitBucket GetBucket(string clientId)
    {
        if (!_buckets.TryGetValue(clientId, out var bucket))
        {
            bucket = new RateLimitBucket
            {
                RemainingTokens = _options.MaxRequests,
                ResetTime = DateTimeOffset.UtcNow.AddSeconds(_options.WindowSeconds)
            };
            _buckets[clientId] = bucket;
        }

        return bucket;
    }

    private string MaskClientId(string clientId)
    {
        if (clientId.Length <= 4)
            return "****";

        return $"{clientId.Substring(0, 2)}****{clientId.Substring(clientId.Length - 2)}";
    }

    private class RateLimitBucket
    {
        public int RemainingTokens { get; set; }
        public DateTimeOffset ResetTime { get; set; }
    }
}

/// <summary>
/// Configuration options for rate limiting middleware
/// </summary>
public class RateLimitingOptions
{
    /// <summary>
    /// Maximum number of requests allowed in the time window
    /// </summary>
    public int MaxRequests { get; set; } = 100;

    /// <summary>
    /// Time window in seconds
    /// </summary>
    public int WindowSeconds { get; set; } = 60;

    /// <summary>
    /// Retry-After header value in seconds
    /// </summary>
    public int RetryAfterSeconds { get; set; } = 60;

    /// <summary>
    /// Paths to exclude from rate limiting
    /// </summary>
    public List<string> ExcludedPaths { get; set; } = new() { "/health", "/metrics" };

    /// <summary>
    /// Custom function to identify clients (default: IP address)
    /// </summary>
    public Func<HttpContext, string>? ClientIdentifierFunc { get; set; }
}
