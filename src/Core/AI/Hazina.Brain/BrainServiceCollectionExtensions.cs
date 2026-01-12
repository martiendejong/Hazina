using Hazina.Brain.Infrastructure;
using Hazina.Brain.Options;
using Hazina.Brain.Repositories;
using Hazina.Brain.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Hazina.Brain;

/// <summary>
/// Extension methods for registering Hazina.Brain services with DI.
/// </summary>
public static class BrainServiceCollectionExtensions
{
    /// <summary>
    /// Add Hazina.Brain persistent memory module to the service collection.
    /// Requires IEmbeddingGenerator to be registered separately.
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configure">Optional configuration action</param>
    /// <returns>Service collection for chaining</returns>
    /// <example>
    /// <code>
    /// services.AddHazinaBrain(options =>
    /// {
    ///     options.Provider = BrainProvider.Sqlite;
    ///     options.ConnectionString = "Data Source=brain.db";
    ///     options.DistillOnObserve = true;
    ///     options.MaxEpisodesPerStore = 5000;
    ///     options.MaxFactsPerStore = 2000;
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddHazinaBrain(
        this IServiceCollection services,
        Action<BrainOptions>? configure = null)
    {
        // Register options
        if (configure != null)
        {
            services.Configure(configure);
        }
        else
        {
            services.Configure<BrainOptions>(_ => { });
        }

        // Register DbContext
        services.AddDbContext<BrainDbContext>((serviceProvider, options) =>
        {
            var brainOptions = new BrainOptions();
            configure?.Invoke(brainOptions);

            if (brainOptions.Provider == BrainProvider.Postgres)
            {
                options.UseNpgsql(brainOptions.ConnectionString);
            }
            else
            {
                options.UseSqlite(brainOptions.ConnectionString);
            }
        });

        // Register repositories
        services.AddScoped<IMemoryEpisodeRepository, MemoryEpisodeRepository>();
        services.AddScoped<IMemoryFactRepository, MemoryFactRepository>();

        // Register services
        services.AddScoped<IMemoryModule, MemoryModule>();
        services.AddScoped<IMemoryDistiller, MemoryDistiller>();

        return services;
    }
}
