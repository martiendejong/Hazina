using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Hazina.Store.EmbeddingStore;

/// <summary>
/// File-based embedding store using JSON serialization with atomic writes and checksum validation.
/// Suitable for small to medium datasets (less than 10k embeddings).
/// This is the refactored version replacing EmbeddingFileStore.
/// </summary>
public class EmbeddingJsonFileStore : IEmbeddingStore, IEnumerableEmbeddingStore, IVectorSearchStore, IBatchEmbeddingStore
{
    private readonly string _filePath;
    private readonly object _lock = new object();
    private List<EmbeddingInfo> _embeddings;
    private string? _fileChecksum;

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

            // Validate checksum if checksum file exists
            var checksumPath = _filePath + ".sha256";
            if (File.Exists(checksumPath))
            {
                var storedChecksum = File.ReadAllText(checksumPath).Trim();
                var actualChecksum = ComputeSha256(json);

                if (storedChecksum != actualChecksum)
                {
                    Console.WriteLine($"WARNING: Checksum mismatch for {_filePath}. File may be corrupted.");
                    Console.WriteLine($"  Expected: {storedChecksum}");
                    Console.WriteLine($"  Actual:   {actualChecksum}");
                }
                else
                {
                    _fileChecksum = actualChecksum;
                }
            }

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

    /// <summary>
    /// Computes SHA256 checksum of a string.
    /// </summary>
    private static string ComputeSha256(string data)
    {
        var bytes = Encoding.UTF8.GetBytes(data);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Saves embeddings to file using atomic write-temp-rename pattern with checksum validation.
    /// </summary>
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

            // Compute checksum
            var checksum = ComputeSha256(json);

            // ATOMIC WRITE PATTERN: Write to temp file first
            var tempPath = _filePath + ".tmp";
            var checksumPath = _filePath + ".sha256";
            var tempChecksumPath = checksumPath + ".tmp";

            try
            {
                // Write data to temp file
                await File.WriteAllTextAsync(tempPath, json);

                // Write checksum to temp file
                await File.WriteAllTextAsync(tempChecksumPath, checksum);

                // Atomic rename: move temp files to final destination
                File.Move(tempPath, _filePath, overwrite: true);
                File.Move(tempChecksumPath, checksumPath, overwrite: true);

                _fileChecksum = checksum;
            }
            catch
            {
                // Clean up temp files if something went wrong
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
                if (File.Exists(tempChecksumPath))
                    File.Delete(tempChecksumPath);

                throw;
            }
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

    #region IBatchEmbeddingStore Implementation

    public async Task<int> StoreBatchAsync(
        IEnumerable<(string key, Embedding embedding, string checksum)> batch,
        CancellationToken cancellationToken = default)
    {
        if (batch == null)
            throw new ArgumentNullException(nameof(batch));

        var count = 0;
        var batchList = batch.ToList();

        lock (_lock)
        {
            foreach (var (key, embedding, checksum) in batchList)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (string.IsNullOrEmpty(key) || embedding == null)
                    continue;

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

                count++;
            }
        }

        // Single atomic write for the entire batch
        await SaveToFileAsync();
        return count;
    }

    public Task<List<EmbeddingInfo>> GetBatchAsync(
        IEnumerable<string> keys,
        CancellationToken cancellationToken = default)
    {
        if (keys == null)
            throw new ArgumentNullException(nameof(keys));

        var keySet = new HashSet<string>(keys);
        List<EmbeddingInfo> results;

        lock (_lock)
        {
            results = _embeddings
                .Where(e => keySet.Contains(e.Key))
                .ToList();
        }

        return Task.FromResult(results);
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
