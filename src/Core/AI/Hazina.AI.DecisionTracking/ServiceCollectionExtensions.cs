using Microsoft.Extensions.DependencyInjection;

namespace Hazina.AI.DecisionTracking;

/// <summary>
/// Dependency injection extensions for DecisionTracking
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add decision tracking services
    /// </summary>
    public static IServiceCollection AddDecisionTracking(this IServiceCollection services, string? basePath = null)
    {
        services.AddSingleton<IDecisionAuditor>(sp =>
        {
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<DecisionAuditorService>>();
            return new DecisionAuditorService(logger, basePath);
        });

        return services;
    }
}
