using Hazina.API.Search.Integration;

namespace Hazina.API.Search.Extensions;

/// <summary>
/// Factory for creating DocumentStore instances
/// </summary>
public interface IDocumentStoreFactory
{
    IDocumentStore CreateStore(string connectionString);
}

public class DocumentStoreFactory : IDocumentStoreFactory
{
    public IDocumentStore CreateStore(string connectionString)
    {
        // Create a new DocumentStore instance with the specified connection string
        // TODO: Use real Hazina DocumentStore when available
        return new DocumentStore();
    }
}

/// <summary>
/// Factory for creating EmbeddingStore instances
/// </summary>
public interface IEmbeddingStoreFactory
{
    IEmbeddingStore CreateStore(string connectionString);
}

public class EmbeddingStoreFactory : IEmbeddingStoreFactory
{
    public IEmbeddingStore CreateStore(string connectionString)
    {
        // Create a new EmbeddingStore instance with the specified connection string
        // TODO: Use real Hazina EmbeddingStore when available
        return new EmbeddingStore();
    }
}

/// <summary>
/// Factory for creating RAGEngine instances
/// </summary>
public interface IRAGEngineFactory
{
    IRAGEngine CreateEngine(RAGEngineConfig config);
}

public class RAGEngineFactory : IRAGEngineFactory
{
    public IRAGEngine CreateEngine(RAGEngineConfig config)
    {
        // Create a new RAGEngine instance with the specified configuration
        // TODO: Use real Hazina RAGEngine when available
        return new RAGEngine(config.EmbeddingStore, config.DocumentStore);
    }
}

/// <summary>
/// Configuration for RAG Engine
/// </summary>
public class RAGEngineConfig
{
    public IDocumentStore DocumentStore { get; set; } = null!;
    public IEmbeddingStore EmbeddingStore { get; set; } = null!;
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";
    public string LLMModel { get; set; } = "gpt-4o";
    public int TopK { get; set; } = 5;
    public float MinRelevanceScore { get; set; } = 0.7f;
}
