using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Hazina.AI.ContextEngineering.Enrichers;

/// <summary>
/// Extension methods for registering message enrichers
/// </summary>
public static class EnricherServiceCollectionExtensions
{
    /// <summary>
    /// Adds the complete message enricher pipeline with all default enrichers
    /// </summary>
    public static IServiceCollection AddMessageEnricherPipeline(
        this IServiceCollection services,
        Action<EnricherPipelineOptions>? configure = null)
    {
        var options = new EnricherPipelineOptions();
        configure?.Invoke(options);

        // Register pipeline
        services.TryAddSingleton<MessageEnricherPipeline>();

        // Register default enrichers
        if (options.EnableHistoryWindow)
        {
            services.TryAddSingleton<IMessageEnricher>(sp =>
                new HistoryWindowEnricher(
                    sp.GetRequiredService<ILogger<HistoryWindowEnricher>>(),
                    options.HistoryWindowOptions));
        }

        if (options.EnableRelevancy)
        {
            services.TryAddSingleton<IMessageEnricher>(sp =>
                new RelevancyEnricher(
                    sp.GetRequiredService<ILogger<RelevancyEnricher>>(),
                    options.RelevancyOptions));
        }

        if (options.EnableFileList)
        {
            services.TryAddSingleton<IMessageEnricher>(sp =>
                new FileListEnricher(
                    sp.GetRequiredService<ILogger<FileListEnricher>>(),
                    options.FileListOptions));
        }

        if (options.EnableStore)
        {
            services.TryAddSingleton<IMessageEnricher>(sp =>
                new StoreEnricher(
                    sp.GetRequiredService<ILogger<StoreEnricher>>(),
                    options.StoreOptions));
        }

        return services;
    }

    /// <summary>
    /// Adds a custom message enricher
    /// </summary>
    public static IServiceCollection AddMessageEnricher<T>(this IServiceCollection services)
        where T : class, IMessageEnricher
    {
        services.TryAddSingleton<IMessageEnricher, T>();
        return services;
    }
}

/// <summary>
/// Configuration options for the enricher pipeline
/// </summary>
public class EnricherPipelineOptions
{
    /// <summary>
    /// Enable history window enricher
    /// </summary>
    public bool EnableHistoryWindow { get; set; } = true;

    /// <summary>
    /// Enable relevancy enricher
    /// </summary>
    public bool EnableRelevancy { get; set; } = true;

    /// <summary>
    /// Enable file list enricher
    /// </summary>
    public bool EnableFileList { get; set; } = true;

    /// <summary>
    /// Enable store enricher
    /// </summary>
    public bool EnableStore { get; set; } = true;

    /// <summary>
    /// History window enricher options
    /// </summary>
    public HistoryWindowOptions HistoryWindowOptions { get; set; } = new();

    /// <summary>
    /// Relevancy enricher options
    /// </summary>
    public RelevancyOptions RelevancyOptions { get; set; } = new();

    /// <summary>
    /// File list enricher options
    /// </summary>
    public FileListOptions FileListOptions { get; set; } = new();

    /// <summary>
    /// Store enricher options
    /// </summary>
    public StoreOptions StoreOptions { get; set; } = new();
}
