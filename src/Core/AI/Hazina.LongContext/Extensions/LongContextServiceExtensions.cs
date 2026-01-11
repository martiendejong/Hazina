using Hazina.LongContext.Configuration;
using Hazina.LongContext.Execution;
using Hazina.LongContext.Interfaces;
using Hazina.LongContext.Planning;
using Hazina.LongContext.Providers;
using Hazina.LongContext.Strategies;
using Microsoft.Extensions.DependencyInjection;

namespace Hazina.LongContext.Extensions;

/// <summary>
/// Dependency injection extensions for long-context services
/// </summary>
public static class LongContextServiceExtensions
{
    /// <summary>
    /// Add long-context orchestration services to the DI container
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configure">Optional configuration action</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddLongContext(
        this IServiceCollection services,
        Action<LongContextOptions>? configure = null)
    {
        // Register options
        var options = new LongContextOptions();
        configure?.Invoke(options);

        // Validate options
        var errors = options.Validate();
        if (errors.Any())
        {
            throw new InvalidOperationException(
                $"LongContext configuration is invalid:\n{string.Join("\n", errors)}");
        }

        services.AddSingleton(options);

        // Register core services
        services.AddSingleton<IContextShardProvider, ContextEngineeringShardProvider>();
        services.AddSingleton<IQueryPlanner, SimpleQueryPlanner>();
        services.AddSingleton<IQueryNodeExecutor, QueryNodeExecutor>();
        services.AddSingleton<ILongContextStrategy, SingleShotStrategy>();

        return services;
    }
}
