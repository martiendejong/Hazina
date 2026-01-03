using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// In-memory implementation of IQueryableMetadataStore.
/// Extends DocumentMetadataMemoryStore with query capabilities.
/// Suitable for development, testing, and small datasets.
/// </summary>
public class QueryableMetadataMemoryStore : IQueryableMetadataStore
{
    private readonly ConcurrentDictionary<string, DocumentMetadata> _metadata = new();

    #region IDocumentMetadataStore (base interface - unchanged)

    public Task<bool> Store(string id, DocumentMetadata metadata)
    {
        _metadata[id] = metadata;
        return Task.FromResult(true);
    }

    public Task<DocumentMetadata?> Get(string id)
    {
        _metadata.TryGetValue(id, out var metadata);
        return Task.FromResult(metadata);
    }

    public Task<bool> Remove(string id)
    {
        _metadata.TryRemove(id, out _);
        return Task.FromResult(true);
    }

    public Task<bool> Exists(string id)
    {
        return Task.FromResult(_metadata.ContainsKey(id));
    }

    #endregion

    #region IQueryableMetadataStore (new query methods)

    public Task<List<DocumentMetadata>> QueryAsync(
        MetadataFilter filter,
        CancellationToken cancellationToken = default)
    {
        var results = ApplyFilter(_metadata.Values, filter)
            .Skip(filter.Offset)
            .Take(filter.Limit)
            .ToList();

        return Task.FromResult(results);
    }

    public Task<List<string>> GetMatchingIdsAsync(
        MetadataFilter filter,
        CancellationToken cancellationToken = default)
    {
        var results = ApplyFilter(_metadata.Values, filter)
            .Skip(filter.Offset)
            .Take(filter.Limit)
            .Select(m => m.Id)
            .ToList();

        return Task.FromResult(results);
    }

    public Task<int> CountAsync(
        MetadataFilter filter,
        CancellationToken cancellationToken = default)
    {
        var count = ApplyFilter(_metadata.Values, filter).Count();
        return Task.FromResult(count);
    }

    public Task<List<DocumentMetadata>> SearchTextAsync(
        string searchText,
        MetadataFilter? filter = null,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var query = _metadata.Values.AsEnumerable();

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

            // Order by relevance (simple scoring: exact matches first, then partial)
            query = query.OrderByDescending(m => ScoreRelevance(m, searchLower));
        }

        var results = query.Take(limit).ToList();
        return Task.FromResult(results);
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
