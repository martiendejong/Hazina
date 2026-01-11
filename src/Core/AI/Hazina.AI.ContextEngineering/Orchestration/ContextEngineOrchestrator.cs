using Hazina.AI.ContextEngineering.Configuration;
using Hazina.AI.ContextEngineering.Fusion;
using Hazina.AI.ContextEngineering.Interfaces;
using Hazina.AI.ContextEngineering.Models;
using Hazina.AI.ContextEngineering.Packing;
using Microsoft.Extensions.Logging;

namespace Hazina.AI.ContextEngineering.Orchestration;

/// <summary>
/// Main orchestrator for the Context Engineering system
/// Coordinates retrieval, fusion, and packing
/// </summary>
public class ContextEngineOrchestrator : IContextEngine
{
    private readonly ILogger<ContextEngineOrchestrator> _logger;
    private readonly FusionEngine _fusionEngine;
    private readonly IContextPacker _packer;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Creates a new context engine orchestrator
    /// </summary>
    public ContextEngineOrchestrator(
        ILogger<ContextEngineOrchestrator> logger,
        FusionEngine fusionEngine,
        IContextPacker packer,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _fusionEngine = fusionEngine;
        _packer = packer;
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    public Task<string> GetContextAsync(
        string query,
        float[]? queryEmbedding = null,
        CancellationToken cancellationToken = default)
    {
        return GetContextAsync(query, ContextEngineConfig.Default, queryEmbedding, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<string> GetContextAsync(
        string query,
        ContextEngineConfig config,
        float[]? queryEmbedding = null,
        CancellationToken cancellationToken = default)
    {
        // Validate configuration
        var errors = config.Validate();
        if (errors.Any())
        {
            throw new InvalidOperationException(
                $"Invalid configuration: {string.Join(", ", errors)}");
        }

        _logger.LogInformation(
            "Starting context engineering for query: {Query} with config: {Config}",
            query,
            config.Name ?? "Custom");

        // Build retrieval query
        var retrievalQuery = new RetrievalQuery
        {
            QueryText = query,
            QueryEmbedding = queryEmbedding,
            Tags = config.RetrievalPolicy.Tags,
            Types = config.RetrievalPolicy.Types,
            MetadataFilters = config.RetrievalPolicy.MetadataFilters
        };

        // Execute retrievers based on policy
        var retrieverResults = new Dictionary<string, List<RetrievalResult>>();
        var weights = new Dictionary<string, double>();

        // Semantic retrieval
        if (config.RetrievalPolicy.SemanticEnabled)
        {
            var semanticRetriever = _serviceProvider.GetService(typeof(IContextRetriever)) as IContextRetriever;
            if (semanticRetriever != null)
            {
                retrievalQuery.TopK = config.RetrievalPolicy.SemanticTopK;
                var results = await semanticRetriever.RetrieveAsync(retrievalQuery, cancellationToken);
                retrieverResults["semantic"] = results;
                weights["semantic"] = config.RetrievalPolicy.SemanticWeight;

                _logger.LogDebug("Semantic retrieval: {Count} results", results.Count);
            }
        }

        // Facts retrieval
        if (config.RetrievalPolicy.FactsEnabled)
        {
            var factRetriever = GetRetrieverByType("facts");
            if (factRetriever != null)
            {
                retrievalQuery.TopK = config.RetrievalPolicy.FactsTopK;
                var results = await factRetriever.RetrieveAsync(retrievalQuery, cancellationToken);
                retrieverResults["facts"] = results;
                weights["facts"] = config.RetrievalPolicy.FactsWeight;

                _logger.LogDebug("Facts retrieval: {Count} results", results.Count);
            }
        }

        // Metadata retrieval
        if (config.RetrievalPolicy.MetadataEnabled)
        {
            var metadataRetriever = GetRetrieverByType("metadata");
            if (metadataRetriever != null)
            {
                retrievalQuery.TopK = config.RetrievalPolicy.MetadataTopK;
                var results = await metadataRetriever.RetrieveAsync(retrievalQuery, cancellationToken);
                retrieverResults["metadata"] = results;
                weights["metadata"] = config.RetrievalPolicy.MetadataWeight;

                _logger.LogDebug("Metadata retrieval: {Count} results", results.Count);
            }
        }

        // ID lookup
        if (config.RetrievalPolicy.LookupEnabled && config.RetrievalPolicy.LookupIds?.Any() == true)
        {
            var lookupRetriever = GetRetrieverByType("lookup");
            if (lookupRetriever != null)
            {
                retrievalQuery.Ids = config.RetrievalPolicy.LookupIds;
                var results = await lookupRetriever.RetrieveAsync(retrievalQuery, cancellationToken);
                retrieverResults["lookup"] = results;
                weights["lookup"] = 1.0; // Lookups get full weight

                _logger.LogDebug("Lookup retrieval: {Count} results", results.Count);
            }
        }

        if (!retrieverResults.Any())
        {
            _logger.LogWarning("No retrieval results obtained");
            return string.Empty;
        }

        // Fuse results
        var fusedResults = _fusionEngine.Fuse(
            retrieverResults,
            weights,
            config.ScoringPolicy.Strategy,
            config.FinalTopK);

        _logger.LogInformation("Fusion complete: {Count} final results", fusedResults.Count);

        // Apply boosting if configured
        if (config.ScoringPolicy.UseTagBoost)
        {
            ApplyTagBoost(fusedResults, retrievalQuery.Tags, config.ScoringPolicy.TagBoostPower);
        }

        if (config.ScoringPolicy.UseRecencyBoost)
        {
            ApplyRecencyBoost(fusedResults, config.ScoringPolicy);
        }

        // Apply minimum score filter
        if (config.ScoringPolicy.MinimumScore > 0)
        {
            fusedResults = fusedResults
                .Where(r => r.Score >= config.ScoringPolicy.MinimumScore)
                .ToList();

            _logger.LogDebug(
                "After minimum score filter ({MinScore}): {Count} results",
                config.ScoringPolicy.MinimumScore,
                fusedResults.Count);
        }

        // Pack into final context
        var context = await _packer.PackAsync(
            fusedResults,
            query,
            config.PackingPolicy,
            cancellationToken);

        _logger.LogInformation(
            "Context engineering complete: {Tokens} tokens",
            _packer.EstimateTokens(context));

        return context;
    }

    private IContextRetriever? GetRetrieverByType(string type)
    {
        // This is a simplified implementation
        // In production, you'd use named services or a factory pattern
        return _serviceProvider.GetService(typeof(IContextRetriever)) as IContextRetriever;
    }

    private void ApplyTagBoost(List<RetrievalResult> results, List<string>? queryTags, double boostPower)
    {
        if (queryTags == null || !queryTags.Any())
            return;

        foreach (var result in results)
        {
            if (result.Tags == null || !result.Tags.Any())
                continue;

            var matchingTags = result.Tags.Intersect(queryTags, StringComparer.OrdinalIgnoreCase).Count();
            if (matchingTags > 0)
            {
                var boost = Math.Pow(1 + (matchingTags * 0.1), boostPower);
                result.Score *= boost;
            }
        }

        // Re-sort after boosting
        results.Sort((a, b) => b.Score.CompareTo(a.Score));
    }

    private void ApplyRecencyBoost(List<RetrievalResult> results, ScoringPolicy policy)
    {
        var now = DateTime.UtcNow;
        var maxAge = TimeSpan.FromDays(policy.RecencyMaxAgeDays);

        foreach (var result in results)
        {
            if (result.Metadata == null || !result.Metadata.ContainsKey("created"))
                continue;

            var createdValue = result.Metadata["created"]?.ToString();
            if (!string.IsNullOrEmpty(createdValue) && DateTime.TryParse(createdValue, out var created))
            {
                var age = now - created;
                if (age <= maxAge)
                {
                    var ageFactor = 1.0 - (age.TotalDays / policy.RecencyMaxAgeDays);
                    var boost = 1.0 + (ageFactor * (1.0 - policy.RecencyDecayFactor));
                    result.Score *= boost;
                }
            }
        }

        // Re-sort after boosting
        results.Sort((a, b) => b.Score.CompareTo(a.Score));
    }
}
