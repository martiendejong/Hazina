using System.Collections.Concurrent;
using System.Text.Json;

namespace Hazina.App.HazinaCoder.Core.Vision;

/// <summary>
/// Cache for vision analysis results
/// Prevents re-analyzing same images with same queries
/// </summary>
public class VisionCache
{
    private readonly ConcurrentDictionary<string, CachedVisionResult> _cache = new();
    private readonly string _cacheDirectory;
    private readonly TimeSpan _defaultTtl;
    private readonly int _maxCacheSize;

    public VisionCache(
        string? cacheDirectory = null,
        TimeSpan? defaultTtl = null,
        int maxCacheSize = 1000)
    {
        _cacheDirectory = cacheDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".hazinacoder",
            "vision-cache"
        );
        _defaultTtl = defaultTtl ?? TimeSpan.FromHours(24);
        _maxCacheSize = maxCacheSize;

        Directory.CreateDirectory(_cacheDirectory);

        // Load existing cache from disk
        LoadCacheFromDisk();
    }

    /// <summary>
    /// Get cached result
    /// </summary>
    public async Task<VisionAnalysisResult?> GetAsync(string cacheKey)
    {
        if (_cache.TryGetValue(cacheKey, out var cached))
        {
            if (!cached.IsExpired)
            {
                cached.LastAccessed = DateTime.UtcNow;
                cached.HitCount++;
                return cached.Result;
            }
            else
            {
                // Remove expired entry
                _cache.TryRemove(cacheKey, out _);
                await DeleteCacheFileAsync(cacheKey);
            }
        }

        return null;
    }

    /// <summary>
    /// Set cached result
    /// </summary>
    public async Task SetAsync(string cacheKey, VisionAnalysisResult result, TimeSpan? ttl = null)
    {
        var cached = new CachedVisionResult
        {
            Key = cacheKey,
            Result = result,
            Created = DateTime.UtcNow,
            LastAccessed = DateTime.UtcNow,
            Ttl = ttl ?? _defaultTtl,
            HitCount = 0
        };

        _cache[cacheKey] = cached;

        // Persist to disk
        await SaveCacheFileToDiskAsync(cacheKey, cached);

        // Evict if over size
        if (_cache.Count > _maxCacheSize)
        {
            await EvictLeastRecentlyUsedAsync();
        }
    }

    /// <summary>
    /// Invalidate cache entry
    /// </summary>
    public async Task InvalidateAsync(string cacheKey)
    {
        _cache.TryRemove(cacheKey, out _);
        await DeleteCacheFileAsync(cacheKey);
    }

    /// <summary>
    /// Clear all cache
    /// </summary>
    public async Task ClearAllAsync()
    {
        _cache.Clear();

        // Delete all cache files
        if (Directory.Exists(_cacheDirectory))
        {
            var files = Directory.GetFiles(_cacheDirectory, "*.json");
            foreach (var file in files)
            {
                try
                {
                    File.Delete(file);
                }
                catch
                {
                    // Ignore deletion errors
                }
            }
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Get cache statistics
    /// </summary>
    public VisionCacheStatistics GetStatistics()
    {
        var totalHits = _cache.Values.Sum(v => v.HitCount);
        var totalEntries = _cache.Count;
        var hitRate = totalEntries > 0 ? (double)totalHits / totalEntries : 0;

        var cacheSize = 0L;
        if (Directory.Exists(_cacheDirectory))
        {
            var files = Directory.GetFiles(_cacheDirectory, "*.json");
            cacheSize = files.Sum(f => new FileInfo(f).Length);
        }

        return new VisionCacheStatistics
        {
            TotalEntries = totalEntries,
            TotalHits = totalHits,
            HitRate = hitRate,
            CacheSizeBytes = cacheSize,
            MaxCacheSize = _maxCacheSize,
            CacheDirectory = _cacheDirectory
        };
    }

    /// <summary>
    /// Load cache from disk on startup
    /// </summary>
    private void LoadCacheFromDisk()
    {
        if (!Directory.Exists(_cacheDirectory))
            return;

        var files = Directory.GetFiles(_cacheDirectory, "*.json");
        foreach (var file in files)
        {
            try
            {
                var json = File.ReadAllText(file);
                var cached = JsonSerializer.Deserialize<CachedVisionResult>(json);

                if (cached != null && !cached.IsExpired)
                {
                    _cache[cached.Key] = cached;
                }
                else
                {
                    // Delete expired cache file
                    File.Delete(file);
                }
            }
            catch
            {
                // Ignore corrupted cache files
            }
        }
    }

    /// <summary>
    /// Save cache entry to disk
    /// </summary>
    private async Task SaveCacheFileToDiskAsync(string cacheKey, CachedVisionResult cached)
    {
        try
        {
            var fileName = $"{cacheKey}.json";
            var filePath = Path.Combine(_cacheDirectory, fileName);

            var json = JsonSerializer.Serialize(cached, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(filePath, json);
        }
        catch
        {
            // Ignore disk write errors
        }
    }

    /// <summary>
    /// Delete cache file from disk
    /// </summary>
    private async Task DeleteCacheFileAsync(string cacheKey)
    {
        try
        {
            var fileName = $"{cacheKey}.json";
            var filePath = Path.Combine(_cacheDirectory, fileName);

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch
        {
            // Ignore deletion errors
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Evict least recently used entries
    /// </summary>
    private async Task EvictLeastRecentlyUsedAsync()
    {
        var evictCount = Math.Max(1, _cache.Count / 10); // Evict 10%
        var toRemove = _cache
            .OrderBy(kv => kv.Value.LastAccessed)
            .Take(evictCount)
            .Select(kv => kv.Key)
            .ToList();

        foreach (var key in toRemove)
        {
            _cache.TryRemove(key, out _);
            await DeleteCacheFileAsync(key);
        }
    }
}

/// <summary>
/// Cached vision result wrapper
/// </summary>
public class CachedVisionResult
{
    public string Key { get; set; } = string.Empty;
    public VisionAnalysisResult Result { get; set; } = null!;
    public DateTime Created { get; set; }
    public DateTime LastAccessed { get; set; }
    public TimeSpan Ttl { get; set; }
    public int HitCount { get; set; }

    public bool IsExpired => DateTime.UtcNow - Created > Ttl;
    public TimeSpan Age => DateTime.UtcNow - Created;
}

/// <summary>
/// Vision cache statistics
/// </summary>
public class VisionCacheStatistics
{
    public int TotalEntries { get; set; }
    public int TotalHits { get; set; }
    public double HitRate { get; set; }
    public long CacheSizeBytes { get; set; }
    public int MaxCacheSize { get; set; }
    public string CacheDirectory { get; set; } = string.Empty;

    public string CacheSizeFormatted => CacheSizeBytes switch
    {
        < 1024 => $"{CacheSizeBytes} B",
        < 1024 * 1024 => $"{CacheSizeBytes / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{CacheSizeBytes / (1024.0 * 1024.0):F1} MB",
        _ => $"{CacheSizeBytes / (1024.0 * 1024.0 * 1024.0):F1} GB"
    };

    public override string ToString()
    {
        return $"Vision Cache: {TotalEntries} entries, {HitRate:P1} hit rate, {CacheSizeFormatted} on disk";
    }
}
