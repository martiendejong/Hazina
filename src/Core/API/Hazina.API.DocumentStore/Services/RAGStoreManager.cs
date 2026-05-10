using Hazina.API.DocumentStore.Configuration;
using Hazina.API.DocumentStore.Data;
using Hazina.API.DocumentStore.Integration;
using Hazina.API.DocumentStore.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hazina.API.DocumentStore.Services;

/// <summary>
/// Manages RAG store lifecycle and operations using real Hazina framework
/// </summary>
public class RAGStoreManager : IDisposable
{
    private readonly RAGStoreRepository _repository;
    private readonly IHazinaStoreFactory _storeFactory;
    private readonly DocumentProcessor _documentProcessor;
    private readonly ILogger<RAGStoreManager> _logger;
    private readonly DocumentStoreApiOptions _options;

    // Cache of active Hazina store instances
    private readonly Dictionary<Guid, HazinaStoreInstance> _storeInstances = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public RAGStoreManager(
        RAGStoreRepository repository,
        IHazinaStoreFactory storeFactory,
        DocumentProcessor documentProcessor,
        ILogger<RAGStoreManager> logger,
        IOptions<DocumentStoreApiOptions> options)
    {
        _repository = repository;
        _storeFactory = storeFactory;
        _documentProcessor = documentProcessor;
        _logger = logger;
        _options = options.Value;
    }

    /// <summary>
    /// Create a new RAG store
    /// </summary>
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

        // Create the store in the repository (SQLite metadata)
        await _repository.CreateAsync(store);

        // Create and cache the Hazina store instance
        await GetOrCreateStoreInstanceAsync(store);

        _logger.LogInformation("Created RAG store {StoreId} with name {Name}", store.Id, name);

        return store;
    }

    /// <summary>
    /// Get a RAG store by ID
    /// </summary>
    public async Task<RAGStore?> GetStoreAsync(Guid storeId)
    {
        return await _repository.GetByIdAsync(storeId);
    }

    /// <summary>
    /// Get a RAG store by name
    /// </summary>
    public async Task<RAGStore?> GetStoreByNameAsync(string name)
    {
        return await _repository.GetByNameAsync(name);
    }

    /// <summary>
    /// Get all RAG stores
    /// </summary>
    public async Task<List<RAGStore>> GetAllStoresAsync()
    {
        return await _repository.GetAllAsync();
    }

    /// <summary>
    /// Update RAG store configuration
    /// </summary>
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

        // Clear cached store instance to force recreation with new config
        await _lock.WaitAsync();
        try
        {
            if (_storeInstances.TryGetValue(storeId, out var instance))
            {
                instance.Dispose();
                _storeInstances.Remove(storeId);
            }
        }
        finally
        {
            _lock.Release();
        }

        _logger.LogInformation("Updated configuration for RAG store {StoreId}", storeId);

        return store;
    }

    /// <summary>
    /// Delete a RAG store
    /// </summary>
    public async Task<bool> DeleteStoreAsync(Guid storeId)
    {
        var store = await _repository.GetByIdAsync(storeId);
        if (store == null)
        {
            return false;
        }

        // Remove from cache and dispose
        await _lock.WaitAsync();
        try
        {
            if (_storeInstances.TryGetValue(storeId, out var instance))
            {
                instance.Dispose();
                _storeInstances.Remove(storeId);
            }
        }
        finally
        {
            _lock.Release();
        }

        // Delete from repository
        await _repository.DeleteAsync(storeId);

        // Clean up data directory
        var dataPath = GetStoreDataPath(store);
        if (Directory.Exists(dataPath))
        {
            try
            {
                Directory.Delete(dataPath, recursive: true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete data directory for store {StoreId}", storeId);
            }
        }

        _logger.LogInformation("Deleted RAG store {StoreId}", storeId);

        return true;
    }

    /// <summary>
    /// Add a document to a RAG store
    /// </summary>
    public async Task<string> AddDocumentAsync(
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

        var instance = await GetOrCreateStoreInstanceAsync(store);

        // Process the document to extract text and chunks
        var (content, chunks) = await _documentProcessor.ProcessDocumentAsync(
            stream, filename, mimeType, store.Config);

        // Generate document ID
        var documentId = $"{storeId:N}/{Guid.NewGuid():N}/{filename}";

        // Store the document using Hazina DocumentStore
        var metadata = new Dictionary<string, string>
        {
            ["filename"] = filename,
            ["mimeType"] = mimeType,
            ["storeId"] = storeId.ToString(),
            ["pageCount"] = content.PageCount.ToString(),
            ["chunkCount"] = chunks.Count.ToString()
        };

        foreach (var kvp in content.Metadata)
        {
            metadata[kvp.Key] = kvp.Value?.ToString() ?? string.Empty;
        }

        var stored = await instance.DocumentStore.Store(documentId, content.Text, metadata, split: true);

        if (!stored)
        {
            throw new Exception($"Failed to store document {filename}");
        }

        // Update statistics
        await _repository.UpdateStatisticsAsync(storeId);

        _logger.LogInformation("Added document {DocumentId} to RAG store {StoreId}", documentId, storeId);

        return documentId;
    }

    /// <summary>
    /// Add plain text to a RAG store
    /// </summary>
    public async Task<string> AddTextAsync(Guid storeId, string text, Dictionary<string, object>? metadata = null)
    {
        var store = await GetStoreAsync(storeId);
        if (store == null)
        {
            throw new KeyNotFoundException($"RAG store {storeId} not found");
        }

        var instance = await GetOrCreateStoreInstanceAsync(store);

        // Process the text
        var (content, chunks) = await _documentProcessor.ProcessTextAsync(text, store.Config);

        // Generate document ID
        var documentId = $"{storeId:N}/{Guid.NewGuid():N}/text-document";

        // Prepare metadata
        var docMetadata = new Dictionary<string, string>
        {
            ["type"] = "text",
            ["storeId"] = storeId.ToString(),
            ["chunkCount"] = chunks.Count.ToString()
        };

        if (metadata != null)
        {
            foreach (var kvp in metadata)
            {
                docMetadata[kvp.Key] = kvp.Value?.ToString() ?? string.Empty;
            }
        }

        // Store using Hazina DocumentStore
        var stored = await instance.DocumentStore.Store(documentId, text, docMetadata, split: true);

        if (!stored)
        {
            throw new Exception("Failed to store text document");
        }

        await _repository.UpdateStatisticsAsync(storeId);

        _logger.LogInformation("Added text document {DocumentId} to RAG store {StoreId}", documentId, storeId);

        return documentId;
    }

    /// <summary>
    /// Get the Hazina store instance for a RAG store
    /// </summary>
    public async Task<HazinaStoreInstance> GetStoreInstanceAsync(Guid storeId)
    {
        var store = await _repository.GetByIdAsync(storeId);
        if (store == null)
        {
            throw new KeyNotFoundException($"RAG store {storeId} not found");
        }

        return await GetOrCreateStoreInstanceAsync(store);
    }

    /// <summary>
    /// Get a document by ID from a RAG store
    /// </summary>
    public async Task<DocumentInfo?> GetDocumentAsync(Guid storeId, string documentId)
    {
        var store = await GetStoreAsync(storeId);
        if (store == null)
        {
            return null;
        }

        var instance = await GetOrCreateStoreInstanceAsync(store);

        try
        {
            var content = await instance.DocumentStore.Get(documentId);
            var docMetadata = await instance.DocumentStore.GetMetadata(documentId);

            return new DocumentInfo
            {
                DocumentId = documentId,
                StoreId = storeId,
                Content = content,
                Filename = ExtractFilenameFromPath(docMetadata?.OriginalPath) ?? ExtractFilenameFromId(documentId),
                MimeType = docMetadata?.MimeType ?? "application/octet-stream",
                Metadata = docMetadata?.CustomMetadata ?? new Dictionary<string, string>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Document {DocumentId} not found in store {StoreId}", documentId, storeId);
            return null;
        }
    }

    /// <summary>
    /// List all documents in a RAG store
    /// </summary>
    public async Task<List<DocumentInfo>> ListDocumentsAsync(Guid storeId, int skip = 0, int take = 20)
    {
        var store = await GetStoreAsync(storeId);
        if (store == null)
        {
            throw new KeyNotFoundException($"RAG store {storeId} not found");
        }

        var instance = await GetOrCreateStoreInstanceAsync(store);
        var documents = new List<DocumentInfo>();

        try
        {
            var keys = await instance.DocumentStore.List("", recursive: true);

            foreach (var key in keys.Skip(skip).Take(take))
            {
                try
                {
                    var docMetadata = await instance.DocumentStore.GetMetadata(key);
                    documents.Add(new DocumentInfo
                    {
                        DocumentId = key,
                        StoreId = storeId,
                        Filename = ExtractFilenameFromPath(docMetadata?.OriginalPath) ?? ExtractFilenameFromId(key),
                        MimeType = docMetadata?.MimeType ?? "application/octet-stream",
                        Metadata = docMetadata?.CustomMetadata ?? new Dictionary<string, string>()
                    });
                }
                catch
                {
                    // Skip documents that can't be read
                    documents.Add(new DocumentInfo
                    {
                        DocumentId = key,
                        StoreId = storeId,
                        Filename = ExtractFilenameFromId(key),
                        MimeType = "unknown"
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error listing documents in store {StoreId}", storeId);
        }

        return documents;
    }

    /// <summary>
    /// Get total document count in a store
    /// </summary>
    public async Task<int> GetDocumentCountAsync(Guid storeId)
    {
        var store = await GetStoreAsync(storeId);
        if (store == null)
        {
            return 0;
        }

        var instance = await GetOrCreateStoreInstanceAsync(store);

        try
        {
            var keys = await instance.DocumentStore.List("", recursive: true);
            return keys.Count;
        }
        catch
        {
            return 0;
        }
    }

    private static string ExtractFilenameFromId(string documentId)
    {
        // Document IDs are in format: {storeId:N}/{docId:N}/{filename}
        var parts = documentId.Split('/');
        return parts.Length >= 3 ? parts[^1] : documentId;
    }

    private static string? ExtractFilenameFromPath(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return null;
        return Path.GetFileName(path);
    }

    /// <summary>
    /// Get or create a Hazina store instance
    /// </summary>
    private async Task<HazinaStoreInstance> GetOrCreateStoreInstanceAsync(RAGStore store)
    {
        await _lock.WaitAsync();
        try
        {
            if (_storeInstances.TryGetValue(store.Id, out var cachedInstance))
            {
                return cachedInstance;
            }

            // Create new Hazina store instance
            var config = new HazinaStoreConfig
            {
                StoreName = $"store_{store.Id:N}",
                BaseDataPath = _options.FileStoragePath,
                EmbeddingDimensions = _options.EmbeddingDimensions,
                DefaultTopK = store.Config.TopK,
                DefaultMinSimilarity = store.Config.MinRelevanceScore
            };

            var instance = _storeFactory.CreateStore(config);
            _storeInstances[store.Id] = instance;

            _logger.LogInformation("Created Hazina store instance for RAG store {StoreId}", store.Id);

            return instance;
        }
        finally
        {
            _lock.Release();
        }
    }

    private string GetStoreDataPath(RAGStore store)
    {
        return Path.Combine(_options.FileStoragePath, $"store_{store.Id:N}");
    }

    public void Dispose()
    {
        foreach (var instance in _storeInstances.Values)
        {
            instance.Dispose();
        }
        _storeInstances.Clear();
        _lock.Dispose();
    }
}
