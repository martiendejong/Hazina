using Hazina.Store.EmbeddingStore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Hazina.Store.PgVector;

/// <summary>
/// Extension methods for registering the Hazina PgVector store adapter with the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="PgVectorEmbeddingStore"/> as <see cref="IEmbeddingStore"/>,
    /// <see cref="IVectorSearchStore"/>, and <see cref="IBatchEmbeddingStore"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">PostgreSQL connection string.</param>
    /// <param name="dimension">
    /// Embedding vector dimension. Defaults to 1536 (OpenAI text-embedding-ada-002).
    /// </param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddHazinaPostgresStore(
        this IServiceCollection services,
        string connectionString,
        int dimension = 1536)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string cannot be null or empty.", nameof(connectionString));

        services.AddSingleton<PgVectorEmbeddingStore>(provider =>
        {
            var logger = provider.GetService<ILogger<PgVectorEmbeddingStore>>();
            return new PgVectorEmbeddingStore(connectionString, dimension, logger);
        });

        services.AddSingleton<IEmbeddingStore>(
            provider => provider.GetRequiredService<PgVectorEmbeddingStore>());

        services.AddSingleton<IVectorSearchStore>(
            provider => provider.GetRequiredService<PgVectorEmbeddingStore>());

        services.AddSingleton<IBatchEmbeddingStore>(
            provider => provider.GetRequiredService<PgVectorEmbeddingStore>());

        return services;
    }

    /// <summary>
    /// Registers <see cref="PgVectorEmbeddingStore"/> using settings from configuration.
    /// Reads <c>Hazina:Store:PgVector:ConnectionString</c> and
    /// <c>Hazina:Store:PgVector:Dimension</c> (optional, defaults to 1536).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddHazinaPostgresStore(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration["Hazina:Store:PgVector:ConnectionString"]
            ?? throw new InvalidOperationException(
                "Hazina:Store:PgVector:ConnectionString not found in configuration.");

        var dimensionStr = configuration["Hazina:Store:PgVector:Dimension"];
        var dimension = int.TryParse(dimensionStr, out var d) ? d : 1536;

        return services.AddHazinaPostgresStore(connectionString, dimension);
    }
}
