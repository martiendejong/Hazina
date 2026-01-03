using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// File-based implementation of ITagRelevanceStore.
/// Stores tag relevance indices as JSON files.
/// </summary>
public class TagRelevanceFileStore : ITagRelevanceStore
{
    private readonly string _basePath;
    private readonly string _indexFileName = "_index.json";
    private readonly object _lock = new();
    private Dictionary<string, TagRelevanceIndexEntry>? _index;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Create a file-based tag relevance store.
    /// </summary>
    /// <param name="basePath">Directory to store tag relevance files</param>
    public TagRelevanceFileStore(string basePath)
    {
        _basePath = basePath ?? throw new ArgumentNullException(nameof(basePath));
        Directory.CreateDirectory(_basePath);
    }

    /// <summary>
    /// Store a tag relevance index.
    /// </summary>
    public async Task StoreAsync(TagRelevanceIndex index, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var filePath = GetFilePath(index.IndexId);
        var json = JsonSerializer.Serialize(index, JsonOptions);
        await File.WriteAllTextAsync(filePath, json, ct);

        // Update index
        await UpdateIndexAsync(index, ct);
    }

    /// <summary>
    /// Get index by ID.
    /// </summary>
    public async Task<TagRelevanceIndex?> GetByIdAsync(string indexId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var filePath = GetFilePath(indexId);
        if (!File.Exists(filePath))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(filePath, ct);
        return JsonSerializer.Deserialize<TagRelevanceIndex>(json, JsonOptions);
    }

    /// <summary>
    /// Get index by checksum (for cache lookup).
    /// </summary>
    public async Task<TagRelevanceIndex?> GetByChecksumAsync(string checksum, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var index = await LoadIndexAsync(ct);
        if (index.TryGetValue(checksum, out var entry))
        {
            return await GetByIdAsync(entry.IndexId, ct);
        }

        return null;
    }

    /// <summary>
    /// Get the most recent index.
    /// </summary>
    public async Task<TagRelevanceIndex?> GetLatestAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var index = await LoadIndexAsync(ct);
        if (index.Count == 0)
        {
            return null;
        }

        var latest = index.Values.OrderByDescending(e => e.Created).FirstOrDefault();
        if (latest == null)
        {
            return null;
        }

        return await GetByIdAsync(latest.IndexId, ct);
    }

    /// <summary>
    /// List all stored indices.
    /// </summary>
    public async Task<List<TagRelevanceIndex>> ListAllAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var results = new List<TagRelevanceIndex>();
        var index = await LoadIndexAsync(ct);

        foreach (var entry in index.Values.OrderByDescending(e => e.Created))
        {
            var item = await GetByIdAsync(entry.IndexId, ct);
            if (item != null)
            {
                results.Add(item);
            }
        }

        return results;
    }

    /// <summary>
    /// Delete an index by ID.
    /// </summary>
    public async Task DeleteAsync(string indexId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var filePath = GetFilePath(indexId);
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        // Update index
        var index = await LoadIndexAsync(ct);
        var toRemove = index.Where(kv => kv.Value.IndexId == indexId).Select(kv => kv.Key).ToList();
        foreach (var key in toRemove)
        {
            index.Remove(key);
        }
        await SaveIndexAsync(index, ct);
    }

    /// <summary>
    /// Delete indices older than cutoff date.
    /// </summary>
    public async Task CleanupOlderThanAsync(DateTime cutoff, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var index = await LoadIndexAsync(ct);
        var toRemove = index.Where(kv => kv.Value.Created < cutoff).ToList();

        foreach (var kv in toRemove)
        {
            var filePath = GetFilePath(kv.Value.IndexId);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            index.Remove(kv.Key);
        }

        await SaveIndexAsync(index, ct);
    }

    private string GetFilePath(string indexId)
    {
        // Sanitize the indexId for use as filename
        var safe = string.Join("_", indexId.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(_basePath, $"{safe}.json");
    }

    private string GetIndexFilePath()
    {
        return Path.Combine(_basePath, _indexFileName);
    }

    private async Task<Dictionary<string, TagRelevanceIndexEntry>> LoadIndexAsync(CancellationToken ct)
    {
        if (_index != null)
        {
            return _index;
        }

        var indexPath = GetIndexFilePath();
        if (!File.Exists(indexPath))
        {
            _index = new Dictionary<string, TagRelevanceIndexEntry>();
            return _index;
        }

        var json = await File.ReadAllTextAsync(indexPath, ct);
        _index = JsonSerializer.Deserialize<Dictionary<string, TagRelevanceIndexEntry>>(json, JsonOptions)
            ?? new Dictionary<string, TagRelevanceIndexEntry>();
        return _index;
    }

    private async Task SaveIndexAsync(Dictionary<string, TagRelevanceIndexEntry> index, CancellationToken ct)
    {
        var indexPath = GetIndexFilePath();
        var json = JsonSerializer.Serialize(index, JsonOptions);
        await File.WriteAllTextAsync(indexPath, json, ct);
        _index = index;
    }

    private async Task UpdateIndexAsync(TagRelevanceIndex item, CancellationToken ct)
    {
        var index = await LoadIndexAsync(ct);

        // Remove any existing entry for this checksum
        if (index.ContainsKey(item.ContextChecksum))
        {
            var oldId = index[item.ContextChecksum].IndexId;
            if (oldId != item.IndexId)
            {
                // Delete old file if different ID
                var oldPath = GetFilePath(oldId);
                if (File.Exists(oldPath))
                {
                    File.Delete(oldPath);
                }
            }
        }

        index[item.ContextChecksum] = new TagRelevanceIndexEntry
        {
            IndexId = item.IndexId,
            Checksum = item.ContextChecksum,
            Created = item.Created
        };

        await SaveIndexAsync(index, ct);
    }
}

/// <summary>
/// Index entry for fast checksum-based lookup.
/// </summary>
public class TagRelevanceIndexEntry
{
    public string IndexId { get; set; } = "";
    public string Checksum { get; set; } = "";
    public DateTime Created { get; set; }
}
