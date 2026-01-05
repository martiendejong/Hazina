using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;

namespace Hazina.Observability.Core.Logging;

/// <summary>
/// Serilog configuration for structured logging
/// </summary>
public static class SerilogConfiguration
{
    /// <summary>
    /// Configures Serilog with best practices for production use
    /// </summary>
    public static LoggerConfiguration ConfigureHazinaLogger(
        this LoggerConfiguration loggerConfig,
        IConfiguration? configuration = null,
        string? applicationName = null)
    {
        var config = loggerConfig
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithThreadId()
            .Enrich.WithEnvironmentName();

        if (!string.IsNullOrEmpty(applicationName))
        {
            config = config.Enrich.WithProperty("Application", applicationName);
        }

        // Console sink with structured output
        config = config.WriteTo.Console(
            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}",
            restrictedToMinimumLevel: LogEventLevel.Information);

        // File sink with rolling files
        config = config.WriteTo.File(
            path: "logs/hazina-.log",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 30,
            fileSizeLimitBytes: 100_000_000, // 100MB
            rollOnFileSizeLimit: true,
            outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] [{SourceContext}] {Message:lj} {Properties:j}{NewLine}{Exception}",
            restrictedToMinimumLevel: LogEventLevel.Debug);

        // Read from configuration if provided
        if (configuration != null)
        {
            config = config.ReadFrom.Configuration(configuration);
        }

        return config;
    }

    /// <summary>
    /// Configures Serilog for development with detailed output
    /// </summary>
    public static LoggerConfiguration ConfigureForDevelopment(this LoggerConfiguration loggerConfig)
    {
        return loggerConfig
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}");
    }

    /// <summary>
    /// Configures Serilog for production with optimized performance
    /// </summary>
    public static LoggerConfiguration ConfigureForProduction(this LoggerConfiguration loggerConfig)
    {
        return loggerConfig
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithThreadId()
            .WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Warning)
            .WriteTo.File(
                path: "logs/hazina-.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                fileSizeLimitBytes: 100_000_000,
                rollOnFileSizeLimit: true,
                buffered: true,
                flushToDiskInterval: TimeSpan.FromSeconds(1),
                restrictedToMinimumLevel: LogEventLevel.Information);
    }
}
