using Hazina.LLMs;
using Hazina.Store.EmbeddingStore;
using Hazina.AI.RAG;
using Hazina.AI.RAG.Core;
using Hazina.AI.Providers.Core;

namespace Hazina.API.Search.Integration;

/// <summary>
/// Configuration for creating a Hazina RAG store
/// </summary>
public class HazinaStoreConfig
{
    public string StoreName { get; set; } = string.Empty;
    public string BaseDataPath { get; set; } = "./data";
    public int EmbeddingDimensions { get; set; } = 1536;
    public int DefaultTopK { get; set; } = 5;
    public double DefaultMinSimilarity { get; set; } = 0.7;
}

/// <summary>
/// Container for all Hazina components for a single RAG store
/// </summary>
public class HazinaStoreInstance : IDisposable
{
    public string StoreName { get; }
    public IDocumentStore DocumentStore { get; }
    public RAGEngine RAGEngine { get; }
    public IEmbeddingStore EmbeddingStore { get; }
    public IVectorSearchStore VectorSearchStore { get; }
    public EmbeddingService EmbeddingService { get; }

    public HazinaStoreInstance(
        string storeName,
        IDocumentStore documentStore,
        RAGEngine ragEngine,
        IEmbeddingStore embeddingStore,
        IVectorSearchStore vectorSearchStore,
        EmbeddingService embeddingService)
    {
        StoreName = storeName;
        DocumentStore = documentStore;
        RAGEngine = ragEngine;
        EmbeddingStore = embeddingStore;
        VectorSearchStore = vectorSearchStore;
        EmbeddingService = embeddingService;
    }

    public void Dispose()
    {
        // Cleanup resources if needed
    }
}

/// <summary>
/// Factory for creating Hazina store instances
/// </summary>
public interface IHazinaStoreFactory
{
    HazinaStoreInstance CreateStore(HazinaStoreConfig config);
}

/// <summary>
/// Implementation of Hazina store factory
/// </summary>
public class HazinaStoreFactory : IHazinaStoreFactory
{
    private readonly ILLMClient _llmClient;
    private readonly IProviderOrchestrator _orchestrator;
    private readonly ILogger<HazinaStoreFactory> _logger;

    public HazinaStoreFactory(
        ILLMClient llmClient,
        IProviderOrchestrator orchestrator,
        ILogger<HazinaStoreFactory> logger)
    {
        _llmClient = llmClient;
        _orchestrator = orchestrator;
        _logger = logger;
    }

    public HazinaStoreInstance CreateStore(HazinaStoreConfig config)
    {
        var storePath = Path.Combine(config.BaseDataPath, config.StoreName);
        Directory.CreateDirectory(storePath);

        // Create text store for raw content
        var textsFolder = Path.Combine(storePath, "texts");
        Directory.CreateDirectory(textsFolder);
        var textStore = new TextFileStore(textsFolder);

        // Create chunk store
        var chunkStore = new ChunkFileStore(Path.Combine(storePath, "chunks"));

        // Create metadata store
        var metadataStore = new DocumentMetadataFileStore(Path.Combine(storePath, "metadata"));

        // Create embedding components (new architecture)
        var embeddingGenerator = new LLMEmbeddingGenerator(_llmClient, config.EmbeddingDimensions);
        var embeddingMemoryStore = new EmbeddingMemoryStore();
        var embeddingService = new EmbeddingService(embeddingMemoryStore, embeddingGenerator);

        // Create legacy text embedding store for backward compatibility with DocumentStore
        var textEmbeddingStore = new TextEmbeddingMemoryStore(_llmClient);

        // Create DocumentStore
        var documentStore = new DocumentStore(
            textEmbeddingStore,
            textStore,
            chunkStore,
            metadataStore,
            _llmClient,
            embeddingMemoryStore,
            embeddingGenerator)
        {
            Name = config.StoreName
        };

        // Create RAGEngine with vector store
        var vectorStore = new EmbeddingVectorStore(embeddingMemoryStore, textStore);
        var ragConfig = new RAGConfig
        {
            DefaultTopK = config.DefaultTopK,
            DefaultMinSimilarity = config.DefaultMinSimilarity
        };

        var ragEngine = new RAGEngine(_orchestrator, vectorStore, null, ragConfig);

        _logger.LogInformation("Created Hazina store instance: {StoreName} at {Path}",
            config.StoreName, storePath);

        return new HazinaStoreInstance(
            config.StoreName,
            documentStore,
            ragEngine,
            embeddingMemoryStore,
            embeddingMemoryStore, // EmbeddingMemoryStore implements both interfaces
            embeddingService);
    }
}

/// <summary>
/// Adapter for EmbeddingMemoryStore to work as IVectorStore for RAGEngine
/// </summary>
public class EmbeddingVectorStore : Hazina.AI.RAG.Core.IVectorStore
{
    private readonly EmbeddingMemoryStore _embeddingStore;
    private readonly ITextStore _textStore;

    public EmbeddingVectorStore(EmbeddingMemoryStore embeddingStore, ITextStore textStore)
    {
        _embeddingStore = embeddingStore;
        _textStore = textStore;
    }

    public async Task<List<Hazina.AI.RAG.Core.VectorSearchResult>> SearchAsync(
        float[] queryEmbedding,
        int topK,
        CancellationToken cancellationToken)
    {
        var embedding = new Embedding(queryEmbedding.Select(f => (double)f));
        var results = await _embeddingStore.SearchSimilarAsync(embedding, topK, 0.0, cancellationToken);

        return results.Select(r => new Hazina.AI.RAG.Core.VectorSearchResult
        {
            Id = r.Info.Key,
            Similarity = r.Similarity,
            Metadata = r.Metadata ?? new Dictionary<string, object>()
        }).ToList();
    }

    public async Task AddAsync(string id, float[] embedding, Dictionary<string, object> metadata,
        CancellationToken cancellationToken)
    {
        var emb = new Embedding(embedding.Select(f => (double)f));
        var checksum = Checksum.CalculateChecksumFromString(id);
        await _embeddingStore.StoreAsync(id, emb, checksum);
    }
}
