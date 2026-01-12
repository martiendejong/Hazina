using Hazina.AI.ContextEngineering.Fusion;
using Hazina.AI.ContextEngineering.Interfaces;
using Hazina.AI.ContextEngineering.Orchestration;
using Hazina.AI.ContextEngineering.Packing;
using Hazina.AI.ContextEngineering.Retrieval;
using Microsoft.Extensions.DependencyInjection;

namespace Hazina.AI.ContextEngineering.Extensions;

/// <summary>
/// Extension methods for registering Context Engineering services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add all context engineering retrievers to the service collection
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddContextEngineering(this IServiceCollection services)
    {
        // Register retrievers
        services.AddTransient<FactRetriever>();
        services.AddTransient<MetadataRetriever>();
        services.AddTransient<IdLookupRetriever>();
        services.AddTransient<SemanticRetriever>();

        // Register fusion engine
        services.AddSingleton<FusionEngine>();

        // Register packing
        services.AddSingleton<IContextPacker, ContextPacker>();

        // Register orchestrator
        services.AddScoped<IContextEngine, ContextEngineOrchestrator>();

        return services;
    }

    /// <summary>
    /// Add a specific retriever to the service collection
    /// </summary>
    /// <typeparam name="TRetriever">Retriever type</typeparam>
    /// <param name="services">Service collection</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddRetriever<TRetriever>(this IServiceCollection services)
        where TRetriever : class, IContextRetriever
    {
        services.AddTransient<IContextRetriever, TRetriever>();
        return services;
    }
}
