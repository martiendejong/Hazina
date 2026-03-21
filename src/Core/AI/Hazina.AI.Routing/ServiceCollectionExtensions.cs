using Hazina.AI.Routing.Interfaces;
using Hazina.AI.Routing.Persistence;
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
    /// <param name="useDatabase">Use EF Core database persistence (default: in-memory)</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddAgentRouting(this IServiceCollection services, bool useDatabase = false)
    {
        // Register reputation store (in-memory by default, EF Core optional)
        if (useDatabase)
        {
            // TODO: Implement EfAgentReputationStore when database requirements are defined
            throw new NotImplementedException("EF Core persistence not yet implemented. Use useDatabase: false.");
        }
        else
        {
            services.AddSingleton<IAgentReputationStore, InMemoryAgentReputationStore>();
        }

        services.AddSingleton<IAgentRoutingService, AgentRoutingService>();
        services.AddSingleton<IPolymathDelegationService, PolymathDelegationService>();
        services.AddSingleton<IModularityAnalysisService, ModularityAnalysisService>();
        return services;
    }
}
