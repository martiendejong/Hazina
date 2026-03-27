using Hazina.Store.EmbeddingStore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Hazina.Store.Sqlite;

/// <summary>
/// Extension methods for registering Hazina SQLite store adapters with the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the SQLite embedding store as <see cref="IEmbeddingStore"/> and <see cref="IVectorSearchStore"/>.
    /// Suitable for local development and embedded scenarios (up to ~100K embeddings).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">SQLite connection string (e.g. "Data Source=hazina.db").</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddHazinaSqliteStore(
        this IServiceCollection services,
        string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string cannot be null or empty.", nameof(connectionString));

        services.AddSingleton<SqliteEmbeddingStore>(provider =>
        {
            var logger = provider.GetService<ILogger<SqliteEmbeddingStore>>();
            return new SqliteEmbeddingStore(connectionString, logger);
        });

        // Register the same instance under both interfaces
        services.AddSingleton<IEmbeddingStore>(
            provider => provider.GetRequiredService<SqliteEmbeddingStore>());

        services.AddSingleton<IVectorSearchStore>(
            provider => provider.GetRequiredService<SqliteEmbeddingStore>());

        return services;
    }

    /// <summary>
    /// Registers the SQLite embedding store using connection string from configuration.
    /// Reads <c>Hazina:Store:Sqlite:ConnectionString</c> from the configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddHazinaSqliteStore(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration["Hazina:Store:Sqlite:ConnectionString"]
            ?? throw new InvalidOperationException(
                "Hazina:Store:Sqlite:ConnectionString not found in configuration.");

        return services.AddHazinaSqliteStore(connectionString);
    }
}
