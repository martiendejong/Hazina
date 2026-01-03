using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// No-operation tag scoring service for backwards compatibility.
/// Returns neutral scores (0.5) for all tags.
/// </summary>
public class NoOpTagScoringService : ITagScoringService
{
    /// <summary>
    /// Returns neutral scores for all tags (no actual scoring).
    /// </summary>
    public Task<TagRelevanceIndex> ScoreTagsAsync(
        IEnumerable<string> tags,
        string queryContext,
        CancellationToken ct = default)
    {
        var index = new TagRelevanceIndex
        {
            QueryContext = queryContext,
            ContextChecksum = ComputeChecksum(queryContext)
        };

        // All tags get neutral score
        foreach (var tag in tags)
        {
            index.TagScores[tag] = 0.5;
        }

        return Task.FromResult(index);
    }

    /// <summary>
    /// Always computes new scores (no caching in NoOp implementation).
    /// </summary>
    public Task<TagRelevanceIndex> GetOrComputeScoresAsync(
        IEnumerable<string> tags,
        string queryContext,
        TimeSpan? maxCacheAge = null,
        CancellationToken ct = default)
    {
        return ScoreTagsAsync(tags, queryContext, ct);
    }

    /// <summary>
    /// Always returns false (no caching).
    /// </summary>
    public Task<bool> HasScoresForContextAsync(string queryContext, CancellationToken ct = default)
    {
        return Task.FromResult(false);
    }

    private static string ComputeChecksum(string input)
    {
        using var md5 = MD5.Create();
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = md5.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
