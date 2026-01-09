using Hazina.Store.FactsStore.Implementations;
using Hazina.Store.FactsStore.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Hazina.Store.FactsStore.Extensions;

/// <summary>
/// Extension methods for registering FactsStore services with DI container
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add SQLite-based facts store to the service collection
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="databasePath">Path to SQLite database file</param>
    /// <param name="initializeOnStartup">Whether to initialize the database schema on startup (default: true)</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddSqliteFactsStore(
        this IServiceCollection services,
        string databasePath,
        bool initializeOnStartup = true)
    {
        services.AddSingleton<IFactsStore>(provider =>
        {
            var store = new SqliteFactsStore(databasePath);

            if (initializeOnStartup)
            {
                // Initialize schema synchronously during startup
                store.InitializeAsync().GetAwaiter().GetResult();
            }

            return store;
        });

        return services;
    }

    /// <summary>
    /// Add SQLite-based facts store with configuration from environment/appsettings
    /// Looks for "FactsStore:DatabasePath" in configuration
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Configuration containing FactsStore:DatabasePath</param>
    /// <param name="initializeOnStartup">Whether to initialize the database schema on startup (default: true)</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddSqliteFactsStore(
        this IServiceCollection services,
        Microsoft.Extensions.Configuration.IConfiguration configuration,
        bool initializeOnStartup = true)
    {
        var databasePath = configuration["FactsStore:DatabasePath"]
            ?? throw new InvalidOperationException("FactsStore:DatabasePath not found in configuration");

        return services.AddSqliteFactsStore(databasePath, initializeOnStartup);
    }
}
