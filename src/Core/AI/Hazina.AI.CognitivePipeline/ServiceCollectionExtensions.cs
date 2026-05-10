using Hazina.AI.CognitivePipeline.Core;
using Hazina.AI.CognitivePipeline.Stages;
using Microsoft.Extensions.DependencyInjection;

namespace Hazina.AI.CognitivePipeline;

/// <summary>
/// Dependency injection extensions for the SCP Cognitive Pipeline.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add the SCP Cognitive Pipeline with all 6 stages.
    /// Requires <see cref="GroundTruth.IGroundTruthStore"/> to be registered separately.
    /// </summary>
    public static IServiceCollection AddCognitivePipeline(this IServiceCollection services)
    {
        // Register the pipeline orchestrator
        services.AddTransient<ICognitivePipeline, Core.CognitivePipeline>();

        // Register all 6 stages (order determined by ISCPStage.Order property)
        services.AddTransient<ISCPStage, SensoryStage>();        // Order 1: Sensory Input
        services.AddTransient<ISCPStage, OrganizationStage>();   // Order 2: Organization
        services.AddTransient<ISCPStage, NoiseFilterStage>();    // Order 3: Noise Filter
        services.AddTransient<ISCPStage, ReflectiveStage>();     // Order 4: Reflective
        services.AddTransient<ISCPStage, DecisionStage>();       // Order 5: Decision
        services.AddTransient<ISCPStage, MemoryStage>();         // Order 6: Memory

        return services;
    }

    /// <summary>
    /// Add the SCP Cognitive Pipeline with custom configuration.
    /// </summary>
    public static IServiceCollection AddCognitivePipeline(
        this IServiceCollection services,
        Action<CognitivePipelineConfig> configure)
    {
        services.AddCognitivePipeline();

        var config = new CognitivePipelineConfig();
        configure(config);
        services.AddSingleton(config);

        return services;
    }
}
