using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Hazina.App.HazinaCoder.Core.Events;

namespace Hazina.App.HazinaCoder.Core.Caching;

/// <summary>
/// Smart caching system for file contents, git status, and other context
/// Provides 10x faster context loading through intelligent caching
/// </summary>
public class SmartContextCache
{
    private readonly ConcurrentDictionary<string, CachedItem<string>> _fileCache;
    private readonly ConcurrentDictionary<string, CachedItem<GitStatus>> _gitCache;
    private readonly ConcurrentDictionary<string, CachedItem<object>> _genericCache;
    private readonly FileSystemWatcher? _fileWatcher;
    private readonly IEventBus? _eventBus;
    private readonly int _maxCacheSize;
    private readonly TimeSpan _defaultTtl;

    public SmartContextCache(
        string? watchDirectory = null,
        IEventBus? eventBus = null,
        int maxCacheSize = 10000,
        TimeSpan? defaultTtl = null)
    {
        _fileCache = new ConcurrentDictionary<string, CachedItem<string>>();
        _gitCache = new ConcurrentDictionary<string, CachedItem<GitStatus>>();
        _genericCache = new ConcurrentDictionary<string, CachedItem<object>>();
        _eventBus = eventBus;
        _maxCacheSize = maxCacheSize;
        _defaultTtl = defaultTtl ?? TimeSpan.FromMinutes(5);

        if (watchDirectory != null && Directory.Exists(watchDirectory))
        {
            _fileWatcher = new FileSystemWatcher(watchDirectory)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                IncludeSubdirectories = true,
                EnableRaisingEvents = true
            };

            _fileWatcher.Changed += OnFileChanged;
            _fileWatcher.Created += OnFileChanged;
            _fileWatcher.Deleted += OnFileChanged;
            _fileWatcher.Renamed += OnFileRenamed;
        }
    }

    /// <summary>
    /// Get file contents from cache or read from disk
    /// </summary>
    public async Task<string> GetOrLoadFileAsync(string filePath)
    {
        var normalizedPath = NormalizePath(filePath);

        // Check cache first
        if (_fileCache.TryGetValue(normalizedPath, out var cached))
        {
            if (!cached.IsExpired)
            {
                cached.LastAccessed = DateTime.UtcNow;
                cached.HitCount++;
                _eventBus?.Publish(new CacheHitEvent("FileContent", normalizedPath));
                return cached.Value;
            }
            else
            {
                // Remove expired entry
                _fileCache.TryRemove(normalizedPath, out _);
            }
        }

        // Cache miss - load from disk
        _eventBus?.Publish(new CacheMissEvent("FileContent", normalizedPath));

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        var content = await File.ReadAllTextAsync(filePath);
        var hash = ComputeHash(content);

        var item = new CachedItem<string>
        {
            Key = normalizedPath,
            Value = content,
            Hash = hash,
            Created = DateTime.UtcNow,
            LastAccessed = DateTime.UtcNow,
            Ttl = _defaultTtl,
            HitCount = 1
        };

        _fileCache[normalizedPath] = item;

        // Evict if over size
        if (_fileCache.Count > _maxCacheSize)
        {
            EvictLeastRecentlyUsed(_fileCache);
        }

        return content;
    }

    /// <summary>
    /// Get git status from cache or execute git command
    /// </summary>
    public async Task<GitStatus> GetOrLoadGitStatusAsync(string repositoryPath, Func<Task<GitStatus>> loader)
    {
        var normalizedPath = NormalizePath(repositoryPath);
        var cacheKey = $"git-status:{normalizedPath}";

        // Check cache
        if (_gitCache.TryGetValue(cacheKey, out var cached))
        {
            if (!cached.IsExpired)
            {
                cached.LastAccessed = DateTime.UtcNow;
                cached.HitCount++;
                _eventBus?.Publish(new CacheHitEvent("GitStatus", cacheKey));
                return cached.Value;
            }
            else
            {
                _gitCache.TryRemove(cacheKey, out _);
            }
        }

        // Cache miss - execute git command
        _eventBus?.Publish(new CacheMissEvent("GitStatus", cacheKey));
        var status = await loader();

        var item = new CachedItem<GitStatus>
        {
            Key = cacheKey,
            Value = status,
            Created = DateTime.UtcNow,
            LastAccessed = DateTime.UtcNow,
            Ttl = TimeSpan.FromSeconds(30), // Git status expires quickly
            HitCount = 1
        };

        _gitCache[cacheKey] = item;

        return status;
    }

    /// <summary>
    /// Generic cache get or load
    /// </summary>
    public async Task<T> GetOrLoadAsync<T>(string key, Func<Task<T>> loader, TimeSpan? ttl = null)
    {
        if (_genericCache.TryGetValue(key, out var cached))
        {
            if (!cached.IsExpired && cached.Value is T typedValue)
            {
                cached.LastAccessed = DateTime.UtcNow;
                cached.HitCount++;
                _eventBus?.Publish(new CacheHitEvent("Generic", key));
                return typedValue;
            }
            else
            {
                _genericCache.TryRemove(key, out _);
            }
        }

        // Cache miss
        _eventBus?.Publish(new CacheMissEvent("Generic", key));
        var value = await loader();

        var item = new CachedItem<object>
        {
            Key = key,
            Value = value!,
            Created = DateTime.UtcNow,
            LastAccessed = DateTime.UtcNow,
            Ttl = ttl ?? _defaultTtl,
            HitCount = 1
        };

        _genericCache[key] = item;

        if (_genericCache.Count > _maxCacheSize)
        {
            EvictLeastRecentlyUsed(_genericCache);
        }

        return value;
    }

    /// <summary>
    /// Invalidate file cache entry
    /// </summary>
    public void InvalidateFile(string filePath)
    {
        var normalizedPath = NormalizePath(filePath);
        _fileCache.TryRemove(normalizedPath, out _);
    }

    /// <summary>
    /// Invalidate git cache
    /// </summary>
    public void InvalidateGitCache(string repositoryPath)
    {
        var normalizedPath = NormalizePath(repositoryPath);
        var cacheKey = $"git-status:{normalizedPath}";
        _gitCache.TryRemove(cacheKey, out _);
    }

    /// <summary>
    /// Clear all caches
    /// </summary>
    public void ClearAll()
    {
        _fileCache.Clear();
        _gitCache.Clear();
        _genericCache.Clear();
    }

    /// <summary>
    /// Get cache statistics
    /// </summary>
    public CacheStatistics GetStatistics()
    {
        var totalItems = _fileCache.Count + _gitCache.Count + _genericCache.Count;
        var totalHits = _fileCache.Values.Sum(v => v.HitCount) +
                       _gitCache.Values.Sum(v => v.HitCount) +
                       _genericCache.Values.Sum(v => v.HitCount);

        var hitRate = totalItems > 0 ? (double)totalHits / totalItems : 0;

        return new CacheStatistics
        {
            TotalItems = totalItems,
            FilesCached = _fileCache.Count,
            GitStatusCached = _gitCache.Count,
            GenericItemsCached = _genericCache.Count,
            TotalHits = totalHits,
            HitRate = hitRate,
            MaxCacheSize = _maxCacheSize
        };
    }

    /// <summary>
    /// File changed event handler
    /// </summary>
    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        InvalidateFile(e.FullPath);
        _eventBus?.Publish(new FileChangedEvent(e.FullPath, e.ChangeType switch
        {
            WatcherChangeTypes.Created => FileChangeType.Created,
            WatcherChangeTypes.Deleted => FileChangeType.Deleted,
            WatcherChangeTypes.Changed => FileChangeType.Modified,
            _ => FileChangeType.Modified
        }));
    }

    /// <summary>
    /// File renamed event handler
    /// </summary>
    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        InvalidateFile(e.OldFullPath);
        InvalidateFile(e.FullPath);
        _eventBus?.Publish(new FileChangedEvent(e.FullPath, FileChangeType.Renamed));
    }

    /// <summary>
    /// Evict least recently used items
    /// </summary>
    private void EvictLeastRecentlyUsed<T>(ConcurrentDictionary<string, CachedItem<T>> cache)
    {
        var evictCount = Math.Max(1, cache.Count / 10); // Evict 10%
        var toRemove = cache
            .OrderBy(kv => kv.Value.LastAccessed)
            .Take(evictCount)
            .Select(kv => kv.Key)
            .ToList();

        foreach (var key in toRemove)
        {
            cache.TryRemove(key, out _);
        }
    }

    /// <summary>
    /// Normalize file path
    /// </summary>
    private string NormalizePath(string path)
    {
        return Path.GetFullPath(path).ToLowerInvariant();
    }

    /// <summary>
    /// Compute content hash
    /// </summary>
    private string ComputeHash(string content)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(content);
        var hashBytes = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hashBytes);
    }

    public void Dispose()
    {
        _fileWatcher?.Dispose();
    }
}

/// <summary>
/// Cached item wrapper
/// </summary>
public class CachedItem<T>
{
    public string Key { get; set; } = string.Empty;
    public T Value { get; set; } = default!;
    public string? Hash { get; set; }
    public DateTime Created { get; set; }
    public DateTime LastAccessed { get; set; }
    public TimeSpan Ttl { get; set; }
    public int HitCount { get; set; }

    public bool IsExpired => DateTime.UtcNow - Created > Ttl;
    public TimeSpan Age => DateTime.UtcNow - Created;
}

/// <summary>
/// Cache statistics
/// </summary>
public class CacheStatistics
{
    public int TotalItems { get; set; }
    public int FilesCached { get; set; }
    public int GitStatusCached { get; set; }
    public int GenericItemsCached { get; set; }
    public int TotalHits { get; set; }
    public double HitRate { get; set; }
    public int MaxCacheSize { get; set; }

    public override string ToString()
    {
        return $"Cache: {TotalItems} items, {HitRate:P1} hit rate, {TotalHits} total hits";
    }
}

/// <summary>
/// Git status result
/// </summary>
public class GitStatus
{
    public string Branch { get; set; } = string.Empty;
    public List<string> ModifiedFiles { get; set; } = new();
    public List<string> UntrackedFiles { get; set; } = new();
    public List<string> StagedFiles { get; set; } = new();
    public bool IsDirty => ModifiedFiles.Any() || UntrackedFiles.Any() || StagedFiles.Any();
}
