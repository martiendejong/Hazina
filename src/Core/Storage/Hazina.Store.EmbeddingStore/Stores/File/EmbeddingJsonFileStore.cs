using System.Text.Json;

namespace DevGPT.Store.EmbeddingStore;

/// <summary>
/// File-based embedding store using JSON serialization.
/// Suitable for small to medium datasets (< 10k embeddings).
/// This is the refactored version replacing EmbeddingFileStore.
/// </summary>
public class EmbeddingJsonFileStore : IEmbeddingStore, IEnumerableEmbeddingStore, IVectorSearchStore
{
    private readonly string _filePath;
    private readonly object _lock = new object();
    private List<EmbeddingInfo> _embeddings;

    /// <summary>
    /// Creates a new EmbeddingJsonFileStore.
    /// </summary>
    /// <param name="filePath">Path to the JSON file for storing embeddings</param>
    public EmbeddingJsonFileStore(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

        _filePath = filePath;
        _embeddings = LoadFromFile();
    }

    private List<EmbeddingInfo> LoadFromFile()
    {
        if (!File.Exists(_filePath))
            return new List<EmbeddingInfo>();

        try
        {
            var json = File.ReadAllText(_filePath);
            var embeddings = JsonSerializer.Deserialize<List<EmbeddingInfo>>(json);
            return embeddings ?? new List<EmbeddingInfo>();
        }
        catch (Exception ex)
        {
            // Log error but don't fail - return empty list
            Console.WriteLine($"Warning: Failed to load embeddings from {_filePath}: {ex.Message}");
            return new List<EmbeddingInfo>();
        }
    }

    private async Task SaveToFileAsync()
    {
        try
        {
            // Ensure directory exists
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(_embeddings, new JsonSerializerOptions
            {
                WriteIndented = true // Makes file human-readable
            });

            await File.WriteAllTextAsync(_filePath, json);
        }
        catch (Exception ex)
        {
            throw new IOException($"Failed to save embeddings to {_filePath}", ex);
        }
    }

    /// <summary>
    /// Reloads embeddings from disk. Useful if the file was modified externally.
    /// </summary>
    public void Reload()
    {
        lock (_lock)
        {
            _embeddings = LoadFromFile();
        }
    }

    #region IEmbeddingStore Implementation

    public async Task<bool> StoreAsync(string key, Embedding embedding, string checksum)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));

        if (embedding == null)
            throw new ArgumentNullException(nameof(embedding));

        lock (_lock)
        {
            var existing = _embeddings.FirstOrDefault(e => e.Key == key);
            if (existing != null)
            {
                // Update existing
                existing.Checksum = checksum;
                existing.Data = embedding;
            }
            else
            {
                // Add new
                _embeddings.Add(new EmbeddingInfo(key, checksum, embedding));
            }
        }

        await SaveToFileAsync();
        return true;
    }

    public Task<EmbeddingInfo?> GetAsync(string key)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));

        lock (_lock)
        {
            var embedding = _embeddings.FirstOrDefault(e => e.Key == key);
            return Task.FromResult(embedding);
        }
    }

    public async Task<bool> RemoveAsync(string key)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));

        bool removed;
        lock (_lock)
        {
            var existing = _embeddings.FirstOrDefault(e => e.Key == key);
            if (existing == null)
                return false;

            removed = _embeddings.Remove(existing);
        }

        if (removed)
            await SaveToFileAsync();

        return removed;
    }

    public Task<bool> ExistsAsync(string key)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));

        lock (_lock)
        {
            return Task.FromResult(_embeddings.Any(e => e.Key == key));
        }
    }

    #endregion

    #region IEnumerableEmbeddingStore Implementation

    public async IAsyncEnumerable<EmbeddingInfo> GetAllAsync(CancellationToken cancellationToken = default)
    {
        List<EmbeddingInfo> snapshot;
        lock (_lock)
        {
            snapshot = _embeddings.ToList(); // Create snapshot to avoid holding lock
        }

        foreach (var embedding in snapshot)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return embedding;
        }
    }

    #endregion

    #region IVectorSearchStore Implementation

    /// <summary>
    /// In-memory vector search using cosine similarity.
    /// Suitable for small datasets. For large datasets, use PgVectorStore.
    /// </summary>
    public Task<List<ScoredEmbedding>> SearchSimilarAsync(
        Embedding queryEmbedding,
        int topK = 10,
        double minSimilarity = 0.0,
        CancellationToken cancellationToken = default)
    {
        if (queryEmbedding == null)
            throw new ArgumentNullException(nameof(queryEmbedding));

        if (topK <= 0)
            throw new ArgumentException("topK must be positive", nameof(topK));

        List<ScoredEmbedding> results;
        lock (_lock)
        {
            results = _embeddings
                .Select(info =>
                {
                    var similarity = info.Data.CosineSimilarity(queryEmbedding);
                    return new ScoredEmbedding
                    {
                        Info = info,
                        Similarity = similarity
                    };
                })
                .Where(scored => scored.Similarity >= minSimilarity)
                .OrderByDescending(scored => scored.Similarity)
                .Take(topK)
                .ToList();
        }

        return Task.FromResult(results);
    }

    #endregion
}
