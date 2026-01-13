using Hazina.API.Search.Data;
using Hazina.API.Search.Extensions;
using Hazina.API.Search.Integration;
using Hazina.API.Search.Models;

namespace Hazina.API.Search.Services;

/// <summary>
/// Manages RAG store lifecycle and operations
/// </summary>
public class RAGStoreManager
{
    private readonly RAGStoreRepository _repository;
    private readonly IDocumentStoreFactory _documentStoreFactory;
    private readonly IEmbeddingStoreFactory _embeddingStoreFactory;
    private readonly IRAGEngineFactory _ragEngineFactory;
    private readonly DocumentProcessor _documentProcessor;
    private readonly EmbeddingService _embeddingService;
    private readonly ILogger<RAGStoreManager> _logger;
    private readonly IConfiguration _configuration;

    // Cache of active RAG engines per store
    private readonly Dictionary<Guid, IRAGEngine> _ragEngines = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public RAGStoreManager(
        RAGStoreRepository repository,
        IDocumentStoreFactory documentStoreFactory,
        IEmbeddingStoreFactory embeddingStoreFactory,
        IRAGEngineFactory ragEngineFactory,
        DocumentProcessor documentProcessor,
        EmbeddingService embeddingService,
        ILogger<RAGStoreManager> logger,
        IConfiguration configuration)
    {
        _repository = repository;
        _documentStoreFactory = documentStoreFactory;
        _embeddingStoreFactory = embeddingStoreFactory;
        _ragEngineFactory = ragEngineFactory;
        _documentProcessor = documentProcessor;
        _embeddingService = embeddingService;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<RAGStore> CreateStoreAsync(string name, string? description, RAGStoreConfig config)
    {
        // Check if store already exists
        if (await _repository.ExistsAsync(name))
        {
            throw new InvalidOperationException($"RAG store with name '{name}' already exists");
        }

        var store = new RAGStore
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            Config = config
        };

        config.RAGStoreId = store.Id;

        // Create the store in the repository
        await _repository.CreateAsync(store);

        // Initialize Hazina stores
        await InitializeHazinaStoresAsync(store);

        _logger.LogInformation("Created RAG store {StoreId} with name {Name}", store.Id, name);

        return store;
    }

    public async Task<RAGStore?> GetStoreAsync(Guid storeId)
    {
        return await _repository.GetByIdAsync(storeId);
    }

    public async Task<RAGStore?> GetStoreByNameAsync(string name)
    {
        return await _repository.GetByNameAsync(name);
    }

    public async Task<List<RAGStore>> GetAllStoresAsync()
    {
        return await _repository.GetAllAsync();
    }

    public async Task<RAGStore> UpdateConfigAsync(Guid storeId, RAGStoreConfig newConfig)
    {
        var store = await _repository.GetByIdAsync(storeId);
        if (store == null)
        {
            throw new KeyNotFoundException($"RAG store {storeId} not found");
        }

        // Update config
        newConfig.Id = store.Config.Id;
        newConfig.RAGStoreId = storeId;
        store.Config = newConfig;

        await _repository.UpdateAsync(store);

        // Clear cached RAG engine to force recreation with new config
        await _lock.WaitAsync();
        try
        {
            _ragEngines.Remove(storeId);
        }
        finally
        {
            _lock.Release();
        }

        _logger.LogInformation("Updated configuration for RAG store {StoreId}", storeId);

        return store;
    }

    public async Task<bool> DeleteStoreAsync(Guid storeId)
    {
        var store = await _repository.GetByIdAsync(storeId);
        if (store == null)
        {
            return false;
        }

        // Remove from cache
        await _lock.WaitAsync();
        try
        {
            _ragEngines.Remove(storeId);
        }
        finally
        {
            _lock.Release();
        }

        // Delete from Hazina stores (document and embedding stores)
        await DeleteHazinaStoresAsync(store);

        // Delete from repository
        await _repository.DeleteAsync(storeId);

        _logger.LogInformation("Deleted RAG store {StoreId}", storeId);

        return true;
    }

    public async Task<Guid> AddDocumentAsync(
        Guid storeId,
        Stream stream,
        string filename,
        string mimeType)
    {
        var store = await GetStoreAsync(storeId);
        if (store == null)
        {
            throw new KeyNotFoundException($"RAG store {storeId} not found");
        }

        // Process the document
        var (content, chunks) = await _documentProcessor.ProcessDocumentAsync(
            stream, filename, mimeType, store.Config);

        // Generate embeddings for each chunk
        var embeddings = await _embeddingService.GenerateBatchEmbeddingsAsync(
            chunks, store.Config.EmbeddingModel);

        // Get the document store for this RAG store
        var documentStore = _documentStoreFactory.CreateStore(GetStoreConnectionString(store));

        // Store document with chunks and embeddings
        var documentId = await documentStore.AddDocumentAsync(new Document
        {
            Id = Guid.NewGuid(),
            Filename = filename,
            MimeType = mimeType,
            Content = content.Text,
            Metadata = content.Metadata,
            Chunks = chunks.Select((chunk, index) => new Chunk
            {
                Text = chunk,
                Index = index,
                Embedding = embeddings[index]
            }).ToList()
        });

        // Update statistics
        await _repository.UpdateStatisticsAsync(storeId);

        _logger.LogInformation("Added document {DocumentId} to RAG store {StoreId}",
            documentId, storeId);

        return documentId;
    }

    public async Task<Guid> AddTextAsync(Guid storeId, string text, Dictionary<string, object>? metadata = null)
    {
        var store = await GetStoreAsync(storeId);
        if (store == null)
        {
            throw new KeyNotFoundException($"RAG store {storeId} not found");
        }

        // Process the text
        var (content, chunks) = await _documentProcessor.ProcessTextAsync(text, store.Config);

        // Merge metadata
        if (metadata != null)
        {
            foreach (var kvp in metadata)
            {
                content.Metadata[kvp.Key] = kvp.Value;
            }
        }

        // Generate embeddings
        var embeddings = await _embeddingService.GenerateBatchEmbeddingsAsync(
            chunks, store.Config.EmbeddingModel);

        // Store document
        var documentStore = _documentStoreFactory.CreateStore(GetStoreConnectionString(store));

        var documentId = await documentStore.AddDocumentAsync(new Document
        {
            Id = Guid.NewGuid(),
            Filename = "text-document",
            MimeType = "text/plain",
            Content = text,
            Metadata = content.Metadata,
            Chunks = chunks.Select((chunk, index) => new Chunk
            {
                Text = chunk,
                Index = index,
                Embedding = embeddings[index]
            }).ToList()
        });

        await _repository.UpdateStatisticsAsync(storeId);

        return documentId;
    }

    public async Task<IRAGEngine> GetRAGEngineAsync(Guid storeId)
    {
        await _lock.WaitAsync();
        try
        {
            if (_ragEngines.TryGetValue(storeId, out var cachedEngine))
            {
                return cachedEngine;
            }

            var store = await _repository.GetByIdAsync(storeId);
            if (store == null)
            {
                throw new KeyNotFoundException($"RAG store {storeId} not found");
            }

            // Create new RAG engine
            var documentStore = _documentStoreFactory.CreateStore(GetStoreConnectionString(store));
            var embeddingStore = _embeddingStoreFactory.CreateStore(GetStoreConnectionString(store));

            var ragEngine = _ragEngineFactory.CreateEngine(new RAGEngineConfig
            {
                DocumentStore = documentStore,
                EmbeddingStore = embeddingStore,
                EmbeddingModel = store.Config.EmbeddingModel,
                LLMModel = store.Config.LLMModel,
                TopK = store.Config.TopK,
                MinRelevanceScore = store.Config.MinRelevanceScore
            });

            _ragEngines[storeId] = ragEngine;

            return ragEngine;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task InitializeHazinaStoresAsync(RAGStore store)
    {
        var connectionString = GetStoreConnectionString(store);

        // Initialize document store
        var documentStore = _documentStoreFactory.CreateStore(connectionString);
        await documentStore.InitializeAsync();

        // Initialize embedding store
        var embeddingStore = _embeddingStoreFactory.CreateStore(connectionString);
        await embeddingStore.InitializeAsync();

        _logger.LogInformation("Initialized Hazina stores for RAG store {StoreId}", store.Id);
    }

    private async Task DeleteHazinaStoresAsync(RAGStore store)
    {
        try
        {
            var connectionString = GetStoreConnectionString(store);

            var documentStore = _documentStoreFactory.CreateStore(connectionString);
            await documentStore.DeleteAllAsync();

            var embeddingStore = _embeddingStoreFactory.CreateStore(connectionString);
            await embeddingStore.DeleteAllAsync();

            _logger.LogInformation("Deleted Hazina stores for RAG store {StoreId}", store.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting Hazina stores for RAG store {StoreId}", store.Id);
        }
    }

    private string GetStoreConnectionString(RAGStore store)
    {
        // Each RAG store gets its own isolated database schema/table prefix
        var baseConnectionString = _configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Postgres connection string not configured");

        return $"{baseConnectionString};SearchPath=ragstore_{store.Id:N}";
    }
}
