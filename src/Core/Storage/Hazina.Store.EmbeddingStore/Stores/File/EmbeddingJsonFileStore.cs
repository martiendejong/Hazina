using System.Text.Json;
using System.Security.Cryptography;
using System.Text;

namespace Hazina.Store.EmbeddingStore;

/// <summary>
/// File-based embedding store using JSON serialization with schema versioning and atomic writes.
/// Suitable for small to medium datasets (less than 10k embeddings).
/// This is the refactored version replacing EmbeddingFileStore.
/// </summary>
/// <remarks>
/// Features:
/// - Schema versioning for backward compatibility
/// - Atomic write-then-rename to prevent corruption
/// - Automatic backup before overwrites
/// - Batch operations for efficient bulk updates
/// - Integrity verification and repair utilities
/// </remarks>
public class EmbeddingJsonFileStore : IEmbeddingStore, IEnumerableEmbeddingStore, IVectorSearchStore, IBatchEmbeddingStore
{
    private readonly string _filePath;
    private readonly object _lock = new object();
    private List<EmbeddingInfo> _embeddings;
    private const int CurrentSchemaVersion = 1;

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

            // Try versioned format first
            try
            {
                var format = JsonSerializer.Deserialize<EmbeddingFileFormat>(json);
                if (format != null && format.Version <= CurrentSchemaVersion)
                {
                    Console.WriteLine($"[EmbeddingJsonFileStore] Loaded {format.Embeddings.Count} embeddings from versioned format (v{format.Version})");
                    return format.Embeddings;
                }
            }
            catch
            {
                // Fall back to legacy format
            }

            // Fall back to legacy list format (backward compatibility)
            var embeddings = JsonSerializer.Deserialize<List<EmbeddingInfo>>(json);
            if (embeddings != null)
            {
                Console.WriteLine($"[EmbeddingJsonFileStore] Loaded {embeddings.Count} embeddings from legacy format");
                return embeddings;
            }

            return new List<EmbeddingInfo>();
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

            // Create versioned format with metadata
            var format = new EmbeddingFileFormat
            {
                Version = CurrentSchemaVersion,
                Embeddings = _embeddings,
                Metadata = new EmbeddingFileMetadata
                {
                    LastModified = DateTime.UtcNow,
                    Count = _embeddings.Count
                }
            };

            var json = JsonSerializer.Serialize(format, new JsonSerializerOptions
            {
                WriteIndented = true // Makes file human-readable
            });

            // Atomic write: write to temp file, then rename
            // This prevents corruption if write is interrupted
            var tempPath = _filePath + ".tmp";
            var backupPath = _filePath + ".bak";

            // Write to temp file
            await File.WriteAllTextAsync(tempPath, json);

            // Create backup of existing file (if exists)
            if (File.Exists(_filePath))
            {
                File.Copy(_filePath, backupPath, overwrite: true);
            }

            // Atomic rename
            File.Move(tempPath, _filePath, overwrite: true);

            // Clean up old backup (keep only most recent)
            // Note: We keep the .bak file for safety, can be manually cleaned
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

    #region IBatchEmbeddingStore Implementation

    public async Task<int> StoreBatchAsync(
        IEnumerable<(string key, Embedding embedding, string checksum)> batch,
        CancellationToken cancellationToken = default)
    {
        if (batch == null)
            throw new ArgumentNullException(nameof(batch));

        var items = batch.ToList();
        if (items.Count == 0)
            return 0;

        int stored = 0;
        lock (_lock)
        {
            foreach (var (key, embedding, checksum) in items)
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
                stored++;
            }
        }

        // Single atomic write for entire batch
        await SaveToFileAsync();
        return stored;
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

    #region Compaction and Integrity

    /// <summary>
    /// Removes embeddings for files that no longer exist in the specified directory.
    /// </summary>
    /// <param name="baseDirectory">Base directory to check file existence against</param>
    /// <returns>Number of orphaned embeddings removed</returns>
    public async Task<int> CompactAsync(string baseDirectory)
    {
        if (string.IsNullOrEmpty(baseDirectory))
            throw new ArgumentException("Base directory cannot be null or empty", nameof(baseDirectory));

        if (!Directory.Exists(baseDirectory))
            throw new DirectoryNotFoundException($"Base directory not found: {baseDirectory}");

        int removed = 0;
        List<string> toRemove = new();

        lock (_lock)
        {
            foreach (var embedding in _embeddings)
            {
                var filePath = Path.Combine(baseDirectory, embedding.Key);
                if (!File.Exists(filePath))
                {
                    toRemove.Add(embedding.Key);
                }
            }

            foreach (var key in toRemove)
            {
                var existing = _embeddings.FirstOrDefault(e => e.Key == key);
                if (existing != null)
                {
                    _embeddings.Remove(existing);
                    removed++;
                }
            }
        }

        if (removed > 0)
        {
            await SaveToFileAsync();
            Console.WriteLine($"[EmbeddingJsonFileStore] Compacted {removed} orphaned embeddings");
        }

        return removed;
    }

    /// <summary>
    /// Verifies integrity of all embeddings and returns list of issues found.
    /// </summary>
    /// <returns>List of integrity issues (empty if all OK)</returns>
    public Task<List<string>> VerifyIntegrityAsync()
    {
        var issues = new List<string>();

        lock (_lock)
        {
            // Check for duplicate keys
            var duplicates = _embeddings
                .GroupBy(e => e.Key)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            foreach (var dup in duplicates)
            {
                issues.Add($"Duplicate key found: {dup}");
            }

            // Check for null or empty keys
            var emptyKeys = _embeddings
                .Where(e => string.IsNullOrWhiteSpace(e.Key))
                .ToList();

            if (emptyKeys.Any())
            {
                issues.Add($"Found {emptyKeys.Count} embeddings with null or empty keys");
            }

            // Check for null embedding data
            var nullData = _embeddings
                .Where(e => e.Data == null || e.Data.Count == 0)
                .Select(e => e.Key)
                .ToList();

            foreach (var key in nullData)
            {
                issues.Add($"Null or empty embedding data for key: {key}");
            }

            // Check for inconsistent embedding dimensions
            var dimensions = _embeddings
                .Where(e => e.Data != null && e.Data.Count > 0)
                .GroupBy(e => e.Data.Count)
                .ToList();

            if (dimensions.Count > 1)
            {
                var dimStr = string.Join(", ", dimensions.Select(g => $"{g.Key} ({g.Count()} embeddings)"));
                issues.Add($"Inconsistent embedding dimensions found: {dimStr}");
            }
        }

        return Task.FromResult(issues);
    }

    /// <summary>
    /// Repairs integrity issues by removing invalid entries.
    /// </summary>
    /// <returns>Number of entries removed</returns>
    public async Task<int> RepairAsync()
    {
        int removed = 0;
        List<EmbeddingInfo> toRemove = new();

        lock (_lock)
        {
            // Remove embeddings with null or empty keys
            toRemove.AddRange(_embeddings.Where(e => string.IsNullOrWhiteSpace(e.Key)));

            // Remove embeddings with null or empty data
            toRemove.AddRange(_embeddings.Where(e => e.Data == null || e.Data.Count == 0));

            // Remove duplicates (keep first occurrence)
            var seen = new HashSet<string>();
            foreach (var embedding in _embeddings.ToList())
            {
                if (!seen.Add(embedding.Key))
                {
                    toRemove.Add(embedding);
                }
            }

            foreach (var item in toRemove)
            {
                if (_embeddings.Remove(item))
                    removed++;
            }
        }

        if (removed > 0)
        {
            await SaveToFileAsync();
            Console.WriteLine($"[EmbeddingJsonFileStore] Repaired {removed} invalid entries");
        }

        return removed;
    }

    #endregion
}
