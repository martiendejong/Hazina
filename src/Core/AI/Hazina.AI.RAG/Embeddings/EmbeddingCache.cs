using System.Security.Cryptography;
using System.Text;

namespace Hazina.AI.RAG.Embeddings;

/// <summary>
/// In-memory cache for sentence embeddings to avoid redundant API calls
/// Significant cost savings when processing documents with repeated text
/// Key: Content hash (SHA256)
/// Value: Embedding vector
/// </summary>
public class EmbeddingCache
{
    private readonly Dictionary<string, Embedding> _cache = new();
    private readonly object _lock = new();
    private int _hits = 0;
    private int _misses = 0;

    /// <summary>
    /// Try to get a cached embedding for the given text
    /// </summary>
    public bool TryGet(string text, out Embedding? embedding)
    {
        var hash = ComputeHash(text);

        lock (_lock)
        {
            if (_cache.TryGetValue(hash, out var cachedEmbedding))
            {
                _hits++;
                embedding = cachedEmbedding;
                return true;
            }

            _misses++;
            embedding = null;
            return false;
        }
    }

    /// <summary>
    /// Add an embedding to the cache
    /// </summary>
    public void Add(string text, Embedding embedding)
    {
        var hash = ComputeHash(text);

        lock (_lock)
        {
            _cache[hash] = embedding;
        }
    }

    /// <summary>
    /// Get or add an embedding (atomic operation)
    /// </summary>
    public Embedding GetOrAdd(string text, Func<string, Embedding> generator)
    {
        if (TryGet(text, out var cached) && cached != null)
        {
            return cached;
        }

        var embedding = generator(text);
        Add(text, embedding);
        return embedding;
    }

    /// <summary>
    /// Get or add an embedding async (atomic operation)
    /// </summary>
    public async Task<Embedding> GetOrAddAsync(
        string text,
        Func<string, Task<Embedding>> generatorAsync)
    {
        if (TryGet(text, out var cached) && cached != null)
        {
            return cached;
        }

        var embedding = await generatorAsync(text);
        Add(text, embedding);
        return embedding;
    }

    /// <summary>
    /// Clear all cached embeddings
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _cache.Clear();
            _hits = 0;
            _misses = 0;
        }
    }

    /// <summary>
    /// Get cache statistics
    /// </summary>
    public CacheStats GetStats()
    {
        lock (_lock)
        {
            return new CacheStats
            {
                CachedItems = _cache.Count,
                Hits = _hits,
                Misses = _misses,
                HitRate = _hits + _misses > 0 ? (double)_hits / (_hits + _misses) : 0.0
            };
        }
    }

    /// <summary>
    /// Compute SHA256 hash of text for cache key
    /// </summary>
    private static string ComputeHash(string text)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(text);
        var hashBytes = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hashBytes);
    }

    /// <summary>
    /// Remove least recently used items if cache exceeds max size
    /// </summary>
    public void Trim(int maxSize)
    {
        lock (_lock)
        {
            if (_cache.Count <= maxSize) return;

            var itemsToRemove = _cache.Count - maxSize;
            var keysToRemove = _cache.Keys.Take(itemsToRemove).ToList();

            foreach (var key in keysToRemove)
            {
                _cache.Remove(key);
            }
        }
    }
}

/// <summary>
/// Cache statistics
/// </summary>
public class CacheStats
{
    public int CachedItems { get; set; }
    public int Hits { get; set; }
    public int Misses { get; set; }
    public double HitRate { get; set; }

    public override string ToString()
    {
        return $"Cache: {CachedItems} items, {Hits} hits, {Misses} misses, {HitRate:P1} hit rate";
    }
}
