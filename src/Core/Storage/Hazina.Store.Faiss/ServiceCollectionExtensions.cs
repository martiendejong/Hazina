using Hazina.Store.EmbeddingStore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Hazina.Store.Faiss;

/// <summary>
/// Extension methods for registering the Hazina FAISS store adapter with the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="FaissEmbeddingStore"/> as <see cref="IEmbeddingStore"/>
    /// and <see cref="IVectorSearchStore"/> using the default in-memory backend.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddHazinaFaissStore(
        this IServiceCollection services)
    {
        services.AddSingleton<FaissEmbeddingStore>(provider =>
        {
            var logger = provider.GetService<ILogger<FaissEmbeddingStore>>();
            return new FaissEmbeddingStore(logger);
        });

        services.AddSingleton<IEmbeddingStore>(
            provider => provider.GetRequiredService<FaissEmbeddingStore>());

        services.AddSingleton<IVectorSearchStore>(
            provider => provider.GetRequiredService<FaissEmbeddingStore>());

        return services;
    }

    /// <summary>
    /// Registers <see cref="FaissEmbeddingStore"/> using a custom <see cref="IFaissIndexBackend"/>.
    /// Use this overload to plug in native FAISS bindings or a gRPC sidecar backend.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="backend">The custom backend implementation to use.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddHazinaFaissStore(
        this IServiceCollection services,
        IFaissIndexBackend backend)
    {
        if (backend is null)
            throw new ArgumentNullException(nameof(backend));

        services.AddSingleton<FaissEmbeddingStore>(provider =>
        {
            var logger = provider.GetService<ILogger<FaissEmbeddingStore>>();
            return new FaissEmbeddingStore(backend, logger);
        });

        services.AddSingleton<IEmbeddingStore>(
            provider => provider.GetRequiredService<FaissEmbeddingStore>());

        services.AddSingleton<IVectorSearchStore>(
            provider => provider.GetRequiredService<FaissEmbeddingStore>());

        return services;
    }
}
