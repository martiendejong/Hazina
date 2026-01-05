using Hazina.Observability.Core.Logging;
using Hazina.Observability.Core.Metrics;
using Hazina.Observability.Core.Tracing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Hazina.Observability.Core;

/// <summary>
/// Unified extensions for adding complete observability stack
/// </summary>
public static class ObservabilityExtensions
{
    /// <summary>
    /// Adds complete Hazina observability stack (logging, metrics, tracing)
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Configuration</param>
    /// <param name="applicationName">Application name for telemetry</param>
    /// <param name="configureOptions">Optional configuration callback</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddHazinaObservability(
        this IServiceCollection services,
        IConfiguration configuration,
        string applicationName = "Hazina",
        Action<ObservabilityOptions>? configureOptions = null)
    {
        var options = new ObservabilityOptions();
        configureOptions?.Invoke(options);

        // Configure Serilog
        if (options.EnableStructuredLogging)
        {
            var loggerConfig = new LoggerConfiguration();

            if (options.Environment == "Development")
            {
                loggerConfig.ConfigureForDevelopment();
            }
            else
            {
                loggerConfig.ConfigureForProduction();
            }

            Log.Logger = loggerConfig.CreateLogger();

            services.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.AddSerilog(dispose: true);
            });
        }

        // Add OpenTelemetry
        if (options.EnableDistributedTracing)
        {
            services.AddHazinaOpenTelemetry(
                serviceName: applicationName,
                serviceVersion: options.ServiceVersion,
                configure: builder =>
                {
                    if (options.UseConsoleExporter)
                    {
                        builder.AddConsoleExporter();
                    }

                    if (!string.IsNullOrEmpty(options.OtlpEndpoint))
                    {
                        builder.AddOtlpExporter(options.OtlpEndpoint);
                    }
                });
        }

        // Add Prometheus metrics endpoint
        if (options.EnableMetrics)
        {
            // Metrics are exposed via HazinaMetrics static class
            // They can be scraped at /metrics endpoint
        }

        // Register telemetry system
        services.AddSingleton<ITelemetrySystem, TelemetrySystem>();

        return services;
    }

    /// <summary>
    /// Creates a configured logger for use before DI is set up
    /// </summary>
    public static Microsoft.Extensions.Logging.ILogger CreateBootstrapLogger(string applicationName = "Hazina")
    {
        Log.Logger = new LoggerConfiguration()
            .ConfigureForDevelopment()
            .CreateLogger();

        return new LoggerFactory()
            .AddSerilog()
            .CreateLogger(applicationName);
    }
}

/// <summary>
/// Configuration options for observability stack
/// </summary>
public class ObservabilityOptions
{
    /// <summary>
    /// Application environment (Development, Production, etc.)
    /// </summary>
    public string Environment { get; set; } =
        System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";

    /// <summary>
    /// Service version for telemetry
    /// </summary>
    public string ServiceVersion { get; set; } = "1.0.0";

    /// <summary>
    /// Enable structured logging with Serilog
    /// </summary>
    public bool EnableStructuredLogging { get; set; } = true;

    /// <summary>
    /// Enable distributed tracing with OpenTelemetry
    /// </summary>
    public bool EnableDistributedTracing { get; set; } = true;

    /// <summary>
    /// Enable Prometheus metrics
    /// </summary>
    public bool EnableMetrics { get; set; } = true;

    /// <summary>
    /// Use console exporter for development
    /// </summary>
    public bool UseConsoleExporter { get; set; } = false;

    /// <summary>
    /// OTLP endpoint for production (Jaeger, Zipkin, etc.)
    /// </summary>
    public string? OtlpEndpoint { get; set; }

    /// <summary>
    /// Log file path
    /// </summary>
    public string LogPath { get; set; } = "logs/hazina-.log";

    /// <summary>
    /// Maximum log file size in bytes
    /// </summary>
    public long MaxLogFileSize { get; set; } = 100_000_000; // 100MB

    /// <summary>
    /// Number of log files to retain
    /// </summary>
    public int RetainedFileCountLimit { get; set; } = 30;
}
