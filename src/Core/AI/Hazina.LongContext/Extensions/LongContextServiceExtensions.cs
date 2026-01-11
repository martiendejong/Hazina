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
        services.AddSingleton<IQueryNodeExecutor, QueryNodeExecutor>();

        // Register both planners
        services.AddSingleton<SimpleQueryPlanner>();
        services.AddSingleton<RecursiveQueryPlanner>();

        // Register planner based on configuration
        if (options.EnableRecursiveQueries)
        {
            // Recursive mode: use RecursiveQueryPlanner (with SimpleQueryPlanner as fallback)
            services.AddSingleton<IQueryPlanner>(sp =>
            {
                var recursivePlanner = sp.GetRequiredService<RecursiveQueryPlanner>();
                return recursivePlanner;
            });

            services.AddSingleton<ILongContextStrategy, RecursiveLongContextStrategy>();
        }
        else
        {
            // Single-shot mode (default)
            services.AddSingleton<IQueryPlanner, SimpleQueryPlanner>();
            services.AddSingleton<ILongContextStrategy, SingleShotStrategy>();
        }

        return services;
    }
}
