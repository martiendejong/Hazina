using Microsoft.Extensions.DependencyInjection;

namespace Hazina.AI.OpenCode;

/// <summary>
/// Extension methods for registering OpenCode services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add OpenCode multi-agent orchestration services to the dependency injection container
    /// </summary>
    public static IServiceCollection AddOpenCodeOrchestration(this IServiceCollection services)
    {
        // Register HTTP client factory if not already registered
        services.AddHttpClient();

        // Register usage tracker as singleton (persistent across requests)
        services.AddSingleton<UsageTracker>();

        // Register OpenCode service
        services.AddSingleton<IOpenCodeService, OpenCodeService>();

        return services;
    }
}
