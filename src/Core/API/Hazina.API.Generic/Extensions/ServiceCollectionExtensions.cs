using Hazina.API.Generic.Configuration;
using Hazina.API.Generic.Entities;
using Hazina.Tools.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Hazina.API.Generic.Extensions;

/// <summary>
/// Extension methods for configuring generic API services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the generic entity API services to the DI container.
    /// </summary>
    /// <typeparam name="TContext">The DbContext type</typeparam>
    public static IServiceCollection AddGenericEntityApi<TContext>(
        this IServiceCollection services,
        Action<GenericApiOptions>? configure = null)
        where TContext : DbContext
    {
        var options = new GenericApiOptions();
        configure?.Invoke(options);

        // Register the configuration loader
        services.AddSingleton<EntityConfigurationLoader>();

        // Register DbContext if not already registered
        if (services.All(s => s.ServiceType != typeof(TContext)))
        {
            throw new InvalidOperationException(
                $"DbContext of type {typeof(TContext).Name} must be registered before calling AddGenericEntityApi");
        }

        // Register generic repository for all entity types
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

        // Auto-discover and register entities if enabled
        if (options.AutoDiscoverEntities)
        {
            var assemblies = options.EntityAssemblies.Count > 0
                ? options.EntityAssemblies
                : new List<Assembly> { Assembly.GetEntryAssembly()! };

            foreach (var assembly in assemblies)
            {
                RegisterEntitiesFromAssembly(services, assembly);
            }
        }

        return services;
    }

    /// <summary>
    /// Registers a specific entity type for the generic API
    /// </summary>
    /// <typeparam name="TEntity">The entity type</typeparam>
    public static IServiceCollection AddGenericEntity<TEntity>(this IServiceCollection services)
        where TEntity : class, IEntity
    {
        services.AddScoped<IRepository<TEntity>, Repository<TEntity>>();
        return services;
    }

    /// <summary>
    /// Registers a specific entity type with a custom repository
    /// </summary>
    /// <typeparam name="TEntity">The entity type</typeparam>
    /// <typeparam name="TRepository">The custom repository type</typeparam>
    public static IServiceCollection AddGenericEntity<TEntity, TRepository>(this IServiceCollection services)
        where TEntity : class, IEntity
        where TRepository : class, IRepository<TEntity>
    {
        services.AddScoped<IRepository<TEntity>, TRepository>();
        return services;
    }

    private static void RegisterEntitiesFromAssembly(IServiceCollection services, Assembly assembly)
    {
        var entityTypes = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t => typeof(IEntity).IsAssignableFrom(t) ||
                       t.GetInterfaces().Any(i => i.IsGenericType &&
                           i.GetGenericTypeDefinition() == typeof(IEntity<>)));

        foreach (var entityType in entityTypes)
        {
            var repositoryInterface = typeof(IRepository<>).MakeGenericType(entityType);
            var repositoryImpl = typeof(Repository<>).MakeGenericType(entityType);

            if (services.All(s => s.ServiceType != repositoryInterface))
            {
                services.AddScoped(repositoryInterface, repositoryImpl);
            }
        }
    }
}

/// <summary>
/// Options for configuring the generic entity API
/// </summary>
public class GenericApiOptions
{
    /// <summary>
    /// Whether to automatically discover and register entities implementing IEntity
    /// </summary>
    public bool AutoDiscoverEntities { get; set; } = true;

    /// <summary>
    /// Assemblies to scan for entity types. If empty, scans entry assembly.
    /// </summary>
    public List<Assembly> EntityAssemblies { get; set; } = new();

    /// <summary>
    /// Path to the entity configuration file (YAML or JSON)
    /// </summary>
    public string? ConfigurationFilePath { get; set; }

    /// <summary>
    /// Path to a directory containing entity configuration files
    /// </summary>
    public string? ConfigurationDirectoryPath { get; set; }

    /// <summary>
    /// Default page size for list operations
    /// </summary>
    public int DefaultPageSize { get; set; } = 20;

    /// <summary>
    /// Maximum page size for list operations
    /// </summary>
    public int MaxPageSize { get; set; } = 100;

    /// <summary>
    /// Whether to enable soft delete by default
    /// </summary>
    public bool EnableSoftDelete { get; set; } = true;

    /// <summary>
    /// Whether to track timestamps by default
    /// </summary>
    public bool EnableTimestamps { get; set; } = true;
}
