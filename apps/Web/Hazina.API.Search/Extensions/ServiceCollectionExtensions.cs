namespace Hazina.API.Search.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHazinaServices(this IServiceCollection services, IConfiguration configuration)
    {
        // TODO: Add actual Hazina service registrations when implementing
        // For now, this is a placeholder for DocumentStore, EmbeddingStore, and RAGEngine

        // Example (uncomment when dependencies are available):
        // services.AddSingleton<IDocumentStore, DocumentStore>();
        // services.AddSingleton<IEmbeddingStore, EmbeddingStore>();
        // services.AddScoped<RAGEngine>();

        return services;
    }
}
