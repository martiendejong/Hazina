using Hazina.AI.ContextEngineering.Configuration;
using Hazina.AI.ContextEngineering.Models;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Hazina.AI.ContextEngineering.Packing;

/// <summary>
/// Packs retrieval results into formatted context based on policy
/// </summary>
public class ContextPacker : IContextPacker
{
    private readonly ILogger<ContextPacker> _logger;
    private readonly SectionFormatter _sectionFormatter;
    private readonly TokenBudgetManager _tokenManager;

    /// <summary>
    /// Creates a new context packer
    /// </summary>
    public ContextPacker(ILogger<ContextPacker> logger)
    {
        _logger = logger;
        _sectionFormatter = new SectionFormatter();
        _tokenManager = new TokenBudgetManager();
    }

    /// <inheritdoc />
    public async Task<string> PackAsync(
        List<RetrievalResult> results,
        string query,
        PackingPolicy policy,
        CancellationToken cancellationToken = default)
    {
        var sections = new Dictionary<string, string>();

        // Build sections based on policy
        foreach (var sectionName in policy.Sections)
        {
            var sectionContent = sectionName switch
            {
                "facts" => await _sectionFormatter.FormatFactsAsync(results, policy, cancellationToken),
                "metadata" => await _sectionFormatter.FormatMetadataAsync(results, policy, cancellationToken),
                "tags" => await _sectionFormatter.FormatTagsAsync(results, policy, cancellationToken),
                "chunks" => await _sectionFormatter.FormatChunksAsync(results, policy, cancellationToken),
                "query" => await _sectionFormatter.FormatQueryAsync(query, policy, cancellationToken),
                _ => string.Empty
            };

            if (!string.IsNullOrWhiteSpace(sectionContent))
            {
                sections[sectionName] = sectionContent;
            }
        }

        // Assemble sections into final context
        var context = AssembleSections(sections, policy);

        // Apply token budget if needed
        if (policy.TrimToFit)
        {
            context = _tokenManager.TrimToFit(context, sections, policy, EstimateTokens);
        }
        else
        {
            var tokens = EstimateTokens(context);
            if (tokens > policy.MaxTokens)
            {
                throw new InvalidOperationException(
                    $"Context exceeds MaxTokens ({tokens} > {policy.MaxTokens}). " +
                    $"Enable TrimToFit or increase MaxTokens.");
            }
        }

        _logger.LogInformation(
            "Packed context: {Tokens} tokens, {Sections} sections",
            EstimateTokens(context),
            sections.Count);

        return context;
    }

    /// <inheritdoc />
    public int EstimateTokens(string text)
    {
        // Simple estimation: ~4 characters per token (GPT-style)
        // For production, use a proper tokenizer like tiktoken
        if (string.IsNullOrEmpty(text))
            return 0;

        return (int)Math.Ceiling(text.Length / 4.0);
    }

    private string AssembleSections(Dictionary<string, string> sections, PackingPolicy policy)
    {
        var sb = new StringBuilder();
        bool first = true;

        foreach (var sectionName in policy.Sections)
        {
            if (!sections.ContainsKey(sectionName))
                continue;

            if (!first)
            {
                sb.Append(policy.SectionSeparator);
            }

            var header = string.Format(policy.SectionHeaderFormat, sectionName.ToUpperInvariant());
            sb.AppendLine(header);
            sb.Append(sections[sectionName]);

            first = false;
        }

        return sb.ToString();
    }
}
