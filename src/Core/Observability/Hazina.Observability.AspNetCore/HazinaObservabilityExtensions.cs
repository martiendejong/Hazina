using Hazina.Observability.Core;
using Hazina.Observability.Core.HealthChecks;
using Hazina.Observability.Core.Tracing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Prometheus;

namespace Hazina.Observability.AspNetCore;

/// <summary>
/// Extension methods for adding Hazina observability to ASP.NET Core applications
/// </summary>
public static class HazinaObservabilityExtensions
{
    /// <summary>
    /// Add Hazina observability services (telemetry, metrics, health checks)
    /// </summary>
    public static IServiceCollection AddHazinaObservability(
        this IServiceCollection services,
        Action<HazinaObservabilityOptions>? configure = null)
    {
        var options = new HazinaObservabilityOptions();
        configure?.Invoke(options);

        // Add telemetry system
        services.AddSingleton<ITelemetrySystem, TelemetrySystem>();

        // Add distributed tracing
        if (options.EnableDistributedTracing)
        {
            services.AddOpenTelemetry()
                .WithTracing(tracerProviderBuilder =>
                {
                    tracerProviderBuilder
                        .AddSource(HazinaActivitySource.SourceName)
                        .SetResourceBuilder(ResourceBuilder.CreateDefault()
                            .AddService(
                                serviceName: options.ServiceName ?? "HazinaApp",
                                serviceVersion: options.ServiceVersion ?? "1.0.0"))
                        .AddConsoleExporter(); // Console exporter for development

                    // Allow custom configuration
                    options.ConfigureTracing?.Invoke(tracerProviderBuilder);
                });
        }

        // Add health checks
        var healthChecksBuilder = services.AddHealthChecks();

        if (options.EnableProviderHealthChecks)
        {
            healthChecksBuilder.AddHazinaProviderHealthCheck(
                tags: new[] { "hazina", "providers", "ready" }
            );
        }

        if (options.EnableNeuroChainHealthChecks)
        {
            healthChecksBuilder.AddHazinaNeuroChainHealthCheck(
                tags: new[] { "hazina", "neurochain", "ready" }
            );
        }

        return services;
    }

    /// <summary>
    /// Use Hazina observability endpoints (metrics and health checks)
    /// </summary>
    public static IApplicationBuilder UseHazinaObservability(
        this IApplicationBuilder app,
        Action<HazinaObservabilityEndpointOptions>? configure = null)
    {
        var options = new HazinaObservabilityEndpointOptions();
        configure?.Invoke(options);

        if (options.EnableMetrics)
        {
            // Enable Prometheus HTTP metrics collection
            app.UseHttpMetrics();

            // Map Prometheus metrics endpoint
            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapMetrics(options.MetricsPath);
            });
        }

        if (options.EnableHealthChecks)
        {
            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                // Liveness probe - always returns healthy if app is running
                endpoints.MapHealthChecks(options.LivenessPath, new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
                {
                    Predicate = _ => false // No health checks, just alive
                });

                // Readiness probe - checks if dependencies are ready
                endpoints.MapHealthChecks(options.ReadinessPath, new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
                {
                    Predicate = check => check.Tags.Contains("ready")
                });

                // Full health check with JSON response
                endpoints.MapHealthChecks(options.HealthPath, new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
                {
                    ResponseWriter = async (context, report) =>
                    {
                        context.Response.ContentType = "application/json";
                        var result = System.Text.Json.JsonSerializer.Serialize(new
                        {
                            status = report.Status.ToString(),
                            duration = report.TotalDuration.TotalMilliseconds,
                            checks = report.Entries.Select(e => new
                            {
                                name = e.Key,
                                status = e.Value.Status.ToString(),
                                description = e.Value.Description,
                                duration = e.Value.Duration.TotalMilliseconds,
                                data = e.Value.Data
                            })
                        });
                        await context.Response.WriteAsync(result);
                    }
                });
            });
        }

        return app;
    }
}

/// <summary>
/// Options for configuring Hazina observability services
/// </summary>
public class HazinaObservabilityOptions
{
    /// <summary>
    /// Enable provider health checks
    /// </summary>
    public bool EnableProviderHealthChecks { get; set; } = true;

    /// <summary>
    /// Enable NeuroChain health checks
    /// </summary>
    public bool EnableNeuroChainHealthChecks { get; set; } = true;

    /// <summary>
    /// Enable distributed tracing with OpenTelemetry
    /// </summary>
    public bool EnableDistributedTracing { get; set; } = true;

    /// <summary>
    /// Service name for distributed tracing
    /// </summary>
    public string? ServiceName { get; set; }

    /// <summary>
    /// Service version for distributed tracing
    /// </summary>
    public string? ServiceVersion { get; set; }

    /// <summary>
    /// Additional tracing configuration (e.g., add OTLP exporter)
    /// </summary>
    public Action<TracerProviderBuilder>? ConfigureTracing { get; set; }
}

/// <summary>
/// Options for configuring Hazina observability endpoints
/// </summary>
public class HazinaObservabilityEndpointOptions
{
    /// <summary>
    /// Enable Prometheus metrics endpoint
    /// </summary>
    public bool EnableMetrics { get; set; } = true;

    /// <summary>
    /// Enable health check endpoints
    /// </summary>
    public bool EnableHealthChecks { get; set; } = true;

    /// <summary>
    /// Path for Prometheus metrics endpoint
    /// </summary>
    public string MetricsPath { get; set; } = "/metrics";

    /// <summary>
    /// Path for liveness probe
    /// </summary>
    public string LivenessPath { get; set; } = "/health/live";

    /// <summary>
    /// Path for readiness probe
    /// </summary>
    public string ReadinessPath { get; set; } = "/health/ready";

    /// <summary>
    /// Path for full health check
    /// </summary>
    public string HealthPath { get; set; } = "/health";
}
