using Microsoft.Extensions.DependencyInjection;

namespace Hazina.AI.TaskPrediction;

/// <summary>
/// Dependency injection extensions for TaskPrediction
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add task prediction services
    /// </summary>
    public static IServiceCollection AddTaskPrediction(this IServiceCollection services, string? modelPath = null)
    {
        services.AddSingleton<IComplexityPredictor>(sp =>
        {
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ComplexityPredictorService>>();
            return new ComplexityPredictorService(logger, modelPath);
        });

        return services;
    }
}
