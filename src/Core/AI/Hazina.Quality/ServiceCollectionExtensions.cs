using Microsoft.Extensions.DependencyInjection;

namespace Hazina.Quality;

/// <summary>
/// Dependency injection extensions for Quality
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add quality monitoring services
    /// </summary>
    public static IServiceCollection AddQualityMonitoring(this IServiceCollection services, string? basePath = null)
    {
        services.AddSingleton<IQualityGuardian>(sp =>
        {
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<QualityGuardianService>>();
            return new QualityGuardianService(logger, basePath);
        });

        return services;
    }
}
