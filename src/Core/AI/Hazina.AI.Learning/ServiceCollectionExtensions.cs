using Microsoft.Extensions.DependencyInjection;

namespace Hazina.AI.Learning;

/// <summary>
/// Dependency injection extensions for Learning
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add learning services
    /// </summary>
    public static IServiceCollection AddLearning(this IServiceCollection services, string? basePath = null)
    {
        services.AddSingleton<IPatternExtractor>(sp =>
        {
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<PatternExtractorService>>();
            return new PatternExtractorService(logger, basePath);
        });

        return services;
    }
}
