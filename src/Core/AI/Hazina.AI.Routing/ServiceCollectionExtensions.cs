using Hazina.AI.Routing.Interfaces;
using Hazina.AI.Routing.Persistence;
using Hazina.AI.Routing.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Hazina.AI.Routing;

/// <summary>
/// Extension methods for registering Hazina.AI.Routing services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds intelligent AI agent routing services with in-memory reputation storage (default).
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddAgentRouting(this IServiceCollection services)
    {
        return services.AddAgentRoutingServices(useDatabase: false);
    }

    /// <summary>
    /// Adds intelligent AI agent routing services to the DI container.
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="useDatabase">Use EF Core database persistence (requires DbContext to be registered separately)</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddAgentRoutingServices(this IServiceCollection services, bool useDatabase = false)
    {
        if (useDatabase)
        {
            services.AddScoped<IAgentReputationStore, EfAgentReputationStore>();
            services.AddScoped<IAgentRoutingService, AgentRoutingService>();
        }
        else
        {
            services.AddSingleton<IAgentReputationStore, InMemoryAgentReputationStore>();
            services.AddSingleton<IAgentRoutingService, AgentRoutingService>();
        }

        return services;
    }

    /// <summary>
    /// Adds intelligent AI agent routing services with EF Core database persistence.
    /// Configures both the DbContext and reputation store in one call.
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configureDbContext">Action to configure the DbContext options (provider, connection string)</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddAgentRouting(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureDbContext)
    {
        services.AddDbContext<AgentRoutingDbContext>(configureDbContext);
        return services.AddAgentRoutingServices(useDatabase: true);
    }
}
