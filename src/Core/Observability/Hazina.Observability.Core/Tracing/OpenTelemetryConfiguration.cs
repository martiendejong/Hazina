using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Hazina.Observability.Core.Tracing;

/// <summary>
/// OpenTelemetry configuration for distributed tracing
/// </summary>
public static class OpenTelemetryConfiguration
{
    /// <summary>
    /// Adds OpenTelemetry tracing with recommended configuration
    /// </summary>
    public static IServiceCollection AddHazinaOpenTelemetry(
        this IServiceCollection services,
        string serviceName = "Hazina",
        string serviceVersion = "1.0.0",
        Action<TracerProviderBuilder>? configure = null)
    {
        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName, serviceVersion: serviceVersion)
                .AddAttributes(new Dictionary<string, object>
                {
                    ["deployment.environment"] = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
                    ["host.name"] = Environment.MachineName
                }))
            .WithTracing(builder =>
            {
                builder
                    .AddSource("Hazina.*")
                    .AddSource(HazinaActivitySource.SourceName)
                    .SetSampler(new AlwaysOnSampler())
                    .AddHttpClientInstrumentation(options =>
                    {
                        options.RecordException = true;
                        options.EnrichWithHttpRequestMessage = (activity, request) =>
                        {
                            activity.SetTag("http.request.method", request.Method.ToString());
                            activity.SetTag("http.request.url", request.RequestUri?.ToString());
                        };
                        options.EnrichWithHttpResponseMessage = (activity, response) =>
                        {
                            activity.SetTag("http.response.status_code", (int)response.StatusCode);
                        };
                    });

                configure?.Invoke(builder);
            });

        return services;
    }

    /// <summary>
    /// Adds console exporter for development
    /// </summary>
    public static TracerProviderBuilder AddConsoleExporter(this TracerProviderBuilder builder)
    {
        return builder.AddConsoleExporter(options =>
        {
            options.Targets = OpenTelemetry.Exporter.ConsoleExporterOutputTargets.Console;
        });
    }

    /// <summary>
    /// Adds OTLP exporter for production (works with Jaeger, Zipkin, etc.)
    /// </summary>
    public static TracerProviderBuilder AddOtlpExporter(
        this TracerProviderBuilder builder,
        string endpoint = "http://localhost:4317")
    {
        return builder.AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri(endpoint);
            options.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
        });
    }
}

/// <summary>
/// Options for configuring OpenTelemetry
/// </summary>
public class OpenTelemetryOptions
{
    /// <summary>
    /// Service name for telemetry
    /// </summary>
    public string ServiceName { get; set; } = "Hazina";

    /// <summary>
    /// Service version
    /// </summary>
    public string ServiceVersion { get; set; } = "1.0.0";

    /// <summary>
    /// OTLP endpoint (Jaeger, Zipkin, etc.)
    /// </summary>
    public string? OtlpEndpoint { get; set; }

    /// <summary>
    /// Whether to use console exporter (for development)
    /// </summary>
    public bool UseConsoleExporter { get; set; } = false;

    /// <summary>
    /// Whether to enable runtime instrumentation
    /// </summary>
    public bool EnableRuntimeInstrumentation { get; set; } = true;

    /// <summary>
    /// Sample rate (0.0 to 1.0, where 1.0 = 100%)
    /// </summary>
    public double SampleRate { get; set; } = 1.0;
}
