using Hazina.Security.AspNetCore.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Hazina.Security.AspNetCore;

/// <summary>
/// Extension methods for registering Hazina security middleware
/// </summary>
public static class SecurityMiddlewareExtensions
{
    /// <summary>
    /// Adds Hazina security services to the service collection
    /// </summary>
    public static IServiceCollection AddHazinaSecurityMiddleware(
        this IServiceCollection services,
        Action<SecurityHeadersOptions>? configureHeaders = null,
        Action<CorrelationIdOptions>? configureCorrelation = null,
        Action<RateLimitingOptions>? configureRateLimit = null)
    {
        var headersOptions = new SecurityHeadersOptions();
        configureHeaders?.Invoke(headersOptions);
        services.AddSingleton(headersOptions);

        var correlationOptions = new CorrelationIdOptions();
        configureCorrelation?.Invoke(correlationOptions);
        services.AddSingleton(correlationOptions);

        var rateLimitOptions = new RateLimitingOptions();
        configureRateLimit?.Invoke(rateLimitOptions);
        services.AddSingleton(rateLimitOptions);

        return services;
    }

    /// <summary>
    /// Adds security headers middleware to the application pipeline
    /// </summary>
    public static IApplicationBuilder UseHazinaSecurityHeaders(
        this IApplicationBuilder app,
        Action<SecurityHeadersOptions>? configure = null)
    {
        var options = app.ApplicationServices.GetService<SecurityHeadersOptions>() ?? new SecurityHeadersOptions();
        configure?.Invoke(options);

        return app.UseMiddleware<SecurityHeadersMiddleware>(options);
    }

    /// <summary>
    /// Adds correlation ID middleware to the application pipeline
    /// </summary>
    public static IApplicationBuilder UseHazinaCorrelationId(
        this IApplicationBuilder app,
        Action<CorrelationIdOptions>? configure = null)
    {
        var options = app.ApplicationServices.GetService<CorrelationIdOptions>() ?? new CorrelationIdOptions();
        configure?.Invoke(options);

        return app.UseMiddleware<CorrelationIdMiddleware>(options);
    }

    /// <summary>
    /// Adds rate limiting middleware to the application pipeline
    /// </summary>
    public static IApplicationBuilder UseHazinaRateLimiting(
        this IApplicationBuilder app,
        Action<RateLimitingOptions>? configure = null)
    {
        var options = app.ApplicationServices.GetService<RateLimitingOptions>() ?? new RateLimitingOptions();
        configure?.Invoke(options);

        return app.UseMiddleware<RateLimitingMiddleware>(options);
    }

    /// <summary>
    /// Adds all Hazina security middleware to the application pipeline in recommended order
    /// </summary>
    public static IApplicationBuilder UseHazinaSecurity(
        this IApplicationBuilder app,
        Action<SecurityHeadersOptions>? configureHeaders = null,
        Action<CorrelationIdOptions>? configureCorrelation = null,
        Action<RateLimitingOptions>? configureRateLimit = null)
    {
        // Order matters! Correlation ID should be first for tracking
        app.UseHazinaCorrelationId(configureCorrelation);
        app.UseHazinaRateLimiting(configureRateLimit);
        app.UseHazinaSecurityHeaders(configureHeaders);

        return app;
    }
}
