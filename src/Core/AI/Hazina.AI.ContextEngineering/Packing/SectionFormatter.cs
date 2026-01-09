using Hazina.AI.ContextEngineering.Configuration;
using Hazina.AI.ContextEngineering.Models;
using System.Text;

namespace Hazina.AI.ContextEngineering.Packing;

/// <summary>
/// Formats different section types for context packing
/// </summary>
public class SectionFormatter
{
    /// <summary>
    /// Format facts section from retrieval results
    /// </summary>
    public Task<string> FormatFactsAsync(
        List<RetrievalResult> results,
        PackingPolicy policy,
        CancellationToken cancellationToken = default)
    {
        var facts = results.Where(r => r.Source == "facts").ToList();
        if (!facts.Any())
            return Task.FromResult(string.Empty);

        var sb = new StringBuilder();
        foreach (var fact in facts)
        {
            sb.Append("- ");
            sb.Append(fact.Content);

            if (policy.IncludeScores)
            {
                sb.Append($" (score: {fact.Score:F2})");
            }

            if (policy.IncludeTags && fact.Tags?.Any() == true)
            {
                sb.Append($" [tags: {string.Join(", ", fact.Tags)}]");
            }

            sb.Append(policy.ItemSeparator);
        }

        return Task.FromResult(sb.ToString().TrimEnd());
    }

    /// <summary>
    /// Format metadata section from retrieval results
    /// </summary>
    public Task<string> FormatMetadataAsync(
        List<RetrievalResult> results,
        PackingPolicy policy,
        CancellationToken cancellationToken = default)
    {
        var metadata = results.Where(r => r.Source == "metadata").ToList();
        if (!metadata.Any())
            return Task.FromResult(string.Empty);

        var sb = new StringBuilder();
        var allTags = results.SelectMany(r => r.Tags ?? Enumerable.Empty<string>()).Distinct().ToList();

        if (allTags.Any())
        {
            sb.AppendLine($"Tags: {string.Join(", ", allTags)}");
        }

        sb.AppendLine($"Documents: {metadata.Count} relevant documents found");

        if (policy.IncludeSourceInfo)
        {
            foreach (var item in metadata.Take(5))
            {
                sb.Append($"- {item.Content}");
                if (policy.IncludeScores)
                {
                    sb.Append($" (score: {item.Score:F2})");
                }
                sb.Append(policy.ItemSeparator);
            }
        }

        return Task.FromResult(sb.ToString().TrimEnd());
    }

    /// <summary>
    /// Format tags section from retrieval results
    /// </summary>
    public Task<string> FormatTagsAsync(
        List<RetrievalResult> results,
        PackingPolicy policy,
        CancellationToken cancellationToken = default)
    {
        var allTags = results
            .SelectMany(r => r.Tags ?? Enumerable.Empty<string>())
            .GroupBy(t => t)
            .Select(g => new { Tag = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToList();

        if (!allTags.Any())
            return Task.FromResult(string.Empty);

        var sb = new StringBuilder();
        sb.AppendLine($"Relevant tags ({allTags.Count} unique):");

        foreach (var tagInfo in allTags)
        {
            sb.Append($"- {tagInfo.Tag}");
            if (policy.IncludeSourceInfo)
            {
                sb.Append($" (appears in {tagInfo.Count} results)");
            }
            sb.Append(policy.ItemSeparator);
        }

        return Task.FromResult(sb.ToString().TrimEnd());
    }

    /// <summary>
    /// Format chunks section from retrieval results
    /// </summary>
    public Task<string> FormatChunksAsync(
        List<RetrievalResult> results,
        PackingPolicy policy,
        CancellationToken cancellationToken = default)
    {
        var chunks = results.Where(r => r.Source == "semantic" || r.Source == "lookup").ToList();
        if (!chunks.Any())
            return Task.FromResult(string.Empty);

        var sb = new StringBuilder();
        foreach (var chunk in chunks)
        {
            if (policy.IncludeSourceInfo)
            {
                sb.Append($"[Source: {chunk.Source}] ");
            }

            if (policy.IncludeScores)
            {
                sb.Append($"[Score: {chunk.Score:F2}] ");
            }

            sb.Append(chunk.Content);

            if (policy.IncludeTags && chunk.Tags?.Any() == true)
            {
                sb.Append($" [Tags: {string.Join(", ", chunk.Tags)}]");
            }

            sb.Append(policy.ItemSeparator);
        }

        return Task.FromResult(sb.ToString().TrimEnd());
    }

    /// <summary>
    /// Format query section
    /// </summary>
    public Task<string> FormatQueryAsync(
        string query,
        PackingPolicy policy,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(query);
    }
}
