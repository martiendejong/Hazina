using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// File-based implementation of IQueryableMetadataStore.
/// Stores metadata as individual JSON files and maintains an in-memory index for fast queries.
/// The index is rebuilt on startup by scanning the metadata folder.
/// </summary>
public class QueryableMetadataFileStore : IQueryableMetadataStore
{
    private readonly string _rootFolder;
    private readonly ConcurrentDictionary<string, DocumentMetadata> _index = new();
    private readonly SemaphoreSlim _indexLock = new(1, 1);
    private bool _indexLoaded = false;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public QueryableMetadataFileStore(string rootFolder)
    {
        _rootFolder = rootFolder;
        Directory.CreateDirectory(_rootFolder);
    }

    #region Index Management

    /// <summary>
    /// Ensures the in-memory index is loaded from disk.
    /// Called lazily on first query operation.
    /// </summary>
    private async Task EnsureIndexLoadedAsync()
    {
        if (_indexLoaded) return;

        await _indexLock.WaitAsync();
        try
        {
            if (_indexLoaded) return;

            var files = Directory.GetFiles(_rootFolder, "*.metadata.json");
            foreach (var file in files)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file);
                    var metadata = JsonSerializer.Deserialize<DocumentMetadata>(json, _jsonOptions);
                    if (metadata != null && !string.IsNullOrEmpty(metadata.Id))
                    {
                        _index[metadata.Id] = metadata;
                    }
                }
                catch
                {
                    // Skip corrupted files
                }
            }

            _indexLoaded = true;
        }
        finally
        {
            _indexLock.Release();
        }
    }

    /// <summary>
    /// Force reload the index from disk.
    /// </summary>
    public async Task RefreshIndexAsync()
    {
        await _indexLock.WaitAsync();
        try
        {
            _index.Clear();
            _indexLoaded = false;
        }
        finally
        {
            _indexLock.Release();
        }

        await EnsureIndexLoadedAsync();
    }

    #endregion

    #region IDocumentMetadataStore (base interface)

    private string GetMetadataPath(string id)
    {
        var sanitized = id.Replace('/', '_').Replace('\\', '_').Replace(':', '_');
        return Path.Combine(_rootFolder, $"{sanitized}.metadata.json");
    }

    public async Task<bool> Store(string id, DocumentMetadata metadata)
    {
        try
        {
            // Ensure ID is set
            metadata.Id = id;

            var path = GetMetadataPath(id);
            var json = JsonSerializer.Serialize(metadata, _jsonOptions);
            await File.WriteAllTextAsync(path, json);

            // Update in-memory index
            _index[id] = metadata;

            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<DocumentMetadata?> Get(string id)
    {
        // Try in-memory index first
        if (_index.TryGetValue(id, out var cached))
        {
            return cached;
        }

        try
        {
            var path = GetMetadataPath(id);
            if (!File.Exists(path)) return null;

            var json = await File.ReadAllTextAsync(path);
            var metadata = JsonSerializer.Deserialize<DocumentMetadata>(json, _jsonOptions);

            // Cache in index
            if (metadata != null)
            {
                _index[id] = metadata;
            }

            return metadata;
        }
        catch
        {
            return null;
        }
    }

    public Task<bool> Remove(string id)
    {
        try
        {
            var path = GetMetadataPath(id);
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            // Remove from index
            _index.TryRemove(id, out _);

            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    public Task<bool> Exists(string id)
    {
        if (_index.ContainsKey(id))
        {
            return Task.FromResult(true);
        }

        var path = GetMetadataPath(id);
        return Task.FromResult(File.Exists(path));
    }

    #endregion

    #region IQueryableMetadataStore (query methods)

    public async Task<List<DocumentMetadata>> QueryAsync(
        MetadataFilter filter,
        CancellationToken cancellationToken = default)
    {
        await EnsureIndexLoadedAsync();

        var results = ApplyFilter(_index.Values, filter)
            .Skip(filter.Offset)
            .Take(filter.Limit)
            .ToList();

        return results;
    }

    public async Task<List<string>> GetMatchingIdsAsync(
        MetadataFilter filter,
        CancellationToken cancellationToken = default)
    {
        await EnsureIndexLoadedAsync();

        var results = ApplyFilter(_index.Values, filter)
            .Skip(filter.Offset)
            .Take(filter.Limit)
            .Select(m => m.Id)
            .ToList();

        return results;
    }

    public async Task<int> CountAsync(
        MetadataFilter filter,
        CancellationToken cancellationToken = default)
    {
        await EnsureIndexLoadedAsync();

        var count = ApplyFilter(_index.Values, filter).Count();
        return count;
    }

    public async Task<List<DocumentMetadata>> SearchTextAsync(
        string searchText,
        MetadataFilter? filter = null,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        await EnsureIndexLoadedAsync();

        var query = _index.Values.AsEnumerable();

        // Apply metadata filter first
        if (filter != null)
        {
            query = ApplyFilter(query, filter);
        }

        // Then apply text search
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var searchLower = searchText.ToLowerInvariant();
            query = query.Where(m =>
                (m.SearchableText?.ToLowerInvariant().Contains(searchLower) ?? false) ||
                (m.Summary?.ToLowerInvariant().Contains(searchLower) ?? false) ||
                (m.OriginalPath?.ToLowerInvariant().Contains(searchLower) ?? false) ||
                m.Tags.Any(t => t.ToLowerInvariant().Contains(searchLower)) ||
                m.CustomMetadata.Values.Any(v => v.ToLowerInvariant().Contains(searchLower))
            );

            // Order by relevance
            query = query.OrderByDescending(m => ScoreRelevance(m, searchLower));
        }

        var results = query.Take(limit).ToList();
        return results;
    }

    #endregion

    #region Private Helpers

    private IEnumerable<DocumentMetadata> ApplyFilter(
        IEnumerable<DocumentMetadata> query,
        MetadataFilter filter)
    {
        if (filter.MimeType != null)
        {
            query = query.Where(m => m.MimeType == filter.MimeType);
        }

        if (filter.MimeTypePrefix != null)
        {
            query = query.Where(m => m.MimeType.StartsWith(filter.MimeTypePrefix));
        }

        if (filter.PathPattern != null)
        {
            var pattern = WildcardToRegex(filter.PathPattern);
            query = query.Where(m => Regex.IsMatch(m.OriginalPath ?? "", pattern, RegexOptions.IgnoreCase));
        }

        if (filter.CreatedAfter.HasValue)
        {
            query = query.Where(m => m.Created >= filter.CreatedAfter.Value);
        }

        if (filter.CreatedBefore.HasValue)
        {
            query = query.Where(m => m.Created <= filter.CreatedBefore.Value);
        }

        if (filter.IsBinary.HasValue)
        {
            query = query.Where(m => m.IsBinary == filter.IsBinary.Value);
        }

        if (filter.Tags != null && filter.Tags.Count > 0)
        {
            // Must have ALL tags
            query = query.Where(m => filter.Tags.All(t => m.Tags.Contains(t, StringComparer.OrdinalIgnoreCase)));
        }

        if (filter.AnyTags != null && filter.AnyTags.Count > 0)
        {
            // Must have ANY of the tags
            query = query.Where(m => filter.AnyTags.Any(t => m.Tags.Contains(t, StringComparer.OrdinalIgnoreCase)));
        }

        if (filter.CustomMetadata != null && filter.CustomMetadata.Count > 0)
        {
            foreach (var kv in filter.CustomMetadata)
            {
                query = query.Where(m =>
                    m.CustomMetadata.TryGetValue(kv.Key, out var value) &&
                    value.Equals(kv.Value, StringComparison.OrdinalIgnoreCase));
            }
        }

        return query;
    }

    private double ScoreRelevance(DocumentMetadata meta, string searchLower)
    {
        double score = 0;

        // Exact match in summary gets highest score
        if (meta.Summary?.ToLowerInvariant() == searchLower)
            score += 10;
        else if (meta.Summary?.ToLowerInvariant().Contains(searchLower) ?? false)
            score += 5;

        // Match in searchable text
        if (meta.SearchableText?.ToLowerInvariant().Contains(searchLower) ?? false)
            score += 3;

        // Match in tags
        if (meta.Tags.Any(t => t.Equals(searchLower, StringComparison.OrdinalIgnoreCase)))
            score += 4;
        else if (meta.Tags.Any(t => t.ToLowerInvariant().Contains(searchLower)))
            score += 2;

        // Match in path
        if (meta.OriginalPath?.ToLowerInvariant().Contains(searchLower) ?? false)
            score += 1;

        return score;
    }

    private string WildcardToRegex(string pattern)
    {
        return "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
    }

    #endregion
}
