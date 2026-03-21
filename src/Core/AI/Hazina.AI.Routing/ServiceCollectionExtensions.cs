using Hazina.AI.Routing.Interfaces;
using Hazina.AI.Routing.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Hazina.AI.Routing;

/// <summary>
/// Extension methods for registering Hazina.AI.Routing services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds intelligent AI agent routing services to the DI container.
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddAgentRouting(this IServiceCollection services)
    {
        services.AddSingleton<IAgentRoutingService, AgentRoutingService>();
        services.AddSingleton<IPolymathDelegationService, PolymathDelegationService>();
        services.AddSingleton<IModularityAnalysisService, ModularityAnalysisService>();
        return services;
    }
}
