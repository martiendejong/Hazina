namespace Hazina.AI.RAG.Graph.Pipeline;

using Hazina.AI.RAG.Configuration;
using Hazina.AI.RAG.Graph.Models;
using Hazina.AI.RAG.Graph.Storage;
using Microsoft.Extensions.Logging;

/// <summary>
/// Entity normalization and deduplication service.
/// </summary>
public class EntityNormalizationService : IEntityNormalizer
{
    private readonly ILogger<EntityNormalizationService> _logger;
    private readonly GraphRAGConfig _config;

    public EntityNormalizationService(
        ILogger<EntityNormalizationService> logger,
        GraphRAGConfig config)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public async Task<GraphEntity> NormalizeEntityAsync(
        GraphEntity entity,
        IGraphStore graphStore,
        CancellationToken cancellationToken = default)
    {
        if (entity == null)
        {
            throw new ArgumentNullException(nameof(entity));
        }

        if (graphStore == null)
        {
            throw new ArgumentNullException(nameof(graphStore));
        }

        try
        {
            // Strategy selection based on configuration
            var normalizedEntity = _config.NormalizationStrategy switch
            {
                EntityNormalizationStrategy.ExactMatch => await NormalizeByExactMatchAsync(entity, graphStore, cancellationToken),
                EntityNormalizationStrategy.FuzzyMatch => await NormalizeByFuzzyMatchAsync(entity, graphStore, cancellationToken),
                EntityNormalizationStrategy.EmbeddingSimilarity => await NormalizeByEmbeddingSimilarityAsync(entity, graphStore, cancellationToken),
                EntityNormalizationStrategy.LLMBased => entity, // Defer LLM-based to future enhancement
                _ => entity
            };

            return normalizedEntity;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error normalizing entity {EntityName}", entity.Name);
            return entity; // Return original on error
        }
    }

    private async Task<GraphEntity> NormalizeByExactMatchAsync(
        GraphEntity entity,
        IGraphStore graphStore,
        CancellationToken cancellationToken)
    {
        // Try to find exact match by name and type
        var existingEntities = await graphStore.SearchEntitiesByNameAsync(
            entity.Name,
            entity.Type,
            cancellationToken);

        if (existingEntities.Count > 0)
        {
            var existing = existingEntities[0];
            _logger.LogDebug("Found exact match for entity {Name}, merging", entity.Name);
            return MergeEntities(existing, entity);
        }

        return entity;
    }

    private async Task<GraphEntity> NormalizeByFuzzyMatchAsync(
        GraphEntity entity,
        IGraphStore graphStore,
        CancellationToken cancellationToken)
    {
        // Search for similar entities by name (case-insensitive, partial match)
        var candidates = await graphStore.SearchEntitiesByNameAsync(
            entity.Name,
            entity.Type,
            cancellationToken);

        foreach (var candidate in candidates)
        {
            var similarity = CalculateStringSimilarity(entity.Name, candidate.Name);
            if (similarity >= _config.EntitySimilarityThreshold)
            {
                _logger.LogDebug("Found fuzzy match for entity {Name} -> {ExistingName} (similarity: {Similarity})",
                    entity.Name, candidate.Name, similarity);
                return MergeEntities(candidate, entity);
            }
        }

        // Check aliases
        if (entity.Aliases != null && entity.Aliases.Count > 0)
        {
            foreach (var alias in entity.Aliases)
            {
                var aliasMatches = await graphStore.SearchEntitiesByNameAsync(
                    alias,
                    entity.Type,
                    cancellationToken);

                if (aliasMatches.Count > 0)
                {
                    _logger.LogDebug("Found match via alias {Alias} for entity {Name}",
                        alias, entity.Name);
                    return MergeEntities(aliasMatches[0], entity);
                }
            }
        }

        return entity;
    }

    private async Task<GraphEntity> NormalizeByEmbeddingSimilarityAsync(
        GraphEntity entity,
        IGraphStore graphStore,
        CancellationToken cancellationToken)
    {
        if (entity.Embedding == null || entity.Embedding.Length == 0)
        {
            // Fallback to fuzzy match if no embedding
            return await NormalizeByFuzzyMatchAsync(entity, graphStore, cancellationToken);
        }

        var similarEntities = await graphStore.SearchEntitiesByEmbeddingAsync(
            entity.Embedding,
            topK: 5,
            cancellationToken: cancellationToken);

        foreach (var similar in similarEntities)
        {
            if (similar.Type == entity.Type)
            {
                // Calculate cosine similarity (simplified - assumes already normalized vectors)
                var similarity = CalculateCosineSimilarity(entity.Embedding, similar.Embedding);

                if (similarity >= _config.EntitySimilarityThreshold)
                {
                    _logger.LogDebug("Found embedding match for entity {Name} -> {ExistingName} (similarity: {Similarity})",
                        entity.Name, similar.Name, similarity);
                    return MergeEntities(similar, entity);
                }
            }
        }

        return entity;
    }

    private GraphEntity MergeEntities(GraphEntity existing, GraphEntity newEntity)
    {
        // Merge properties
        foreach (var prop in newEntity.Properties)
        {
            if (!existing.Properties.ContainsKey(prop.Key))
            {
                existing.Properties[prop.Key] = prop.Value;
            }
        }

        // Merge source documents
        if (!existing.SourceDocuments.Contains(newEntity.SourceDocuments[0]))
        {
            existing.SourceDocuments.AddRange(newEntity.SourceDocuments);
        }

        // Merge aliases
        if (newEntity.Aliases != null && newEntity.Aliases.Count > 0)
        {
            existing.Aliases ??= new List<string>();
            foreach (var alias in newEntity.Aliases)
            {
                if (!existing.Aliases.Contains(alias, StringComparer.OrdinalIgnoreCase))
                {
                    existing.Aliases.Add(alias);
                }
            }
        }

        // Add new entity name as alias if different
        if (!existing.Name.Equals(newEntity.Name, StringComparison.OrdinalIgnoreCase))
        {
            existing.Aliases ??= new List<string>();
            if (!existing.Aliases.Contains(newEntity.Name, StringComparer.OrdinalIgnoreCase))
            {
                existing.Aliases.Add(newEntity.Name);
            }
        }

        // Increment mention count
        existing.MentionCount++;

        // Update confidence (weighted average)
        existing.Confidence = (existing.Confidence * (existing.MentionCount - 1) + newEntity.Confidence) / existing.MentionCount;

        // Update timestamp
        existing.LastUpdated = DateTime.UtcNow;

        return existing;
    }

    private double CalculateStringSimilarity(string str1, string str2)
    {
        if (string.IsNullOrEmpty(str1) || string.IsNullOrEmpty(str2))
        {
            return 0.0;
        }

        // Case-insensitive comparison
        str1 = str1.ToLowerInvariant();
        str2 = str2.ToLowerInvariant();

        // Exact match
        if (str1 == str2)
        {
            return 1.0;
        }

        // Levenshtein distance
        var distance = LevenshteinDistance(str1, str2);
        var maxLength = Math.Max(str1.Length, str2.Length);
        return 1.0 - ((double)distance / maxLength);
    }

    private int LevenshteinDistance(string str1, string str2)
    {
        var m = str1.Length;
        var n = str2.Length;
        var dp = new int[m + 1, n + 1];

        for (var i = 0; i <= m; i++)
        {
            dp[i, 0] = i;
        }

        for (var j = 0; j <= n; j++)
        {
            dp[0, j] = j;
        }

        for (var i = 1; i <= m; i++)
        {
            for (var j = 1; j <= n; j++)
            {
                var cost = str1[i - 1] == str2[j - 1] ? 0 : 1;
                dp[i, j] = Math.Min(
                    Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1),
                    dp[i - 1, j - 1] + cost);
            }
        }

        return dp[m, n];
    }

    private double CalculateCosineSimilarity(float[]? vec1, float[]? vec2)
    {
        if (vec1 == null || vec2 == null || vec1.Length != vec2.Length || vec1.Length == 0)
        {
            return 0.0;
        }

        double dotProduct = 0.0;
        double magnitude1 = 0.0;
        double magnitude2 = 0.0;

        for (var i = 0; i < vec1.Length; i++)
        {
            dotProduct += vec1[i] * vec2[i];
            magnitude1 += vec1[i] * vec1[i];
            magnitude2 += vec2[i] * vec2[i];
        }

        if (magnitude1 == 0.0 || magnitude2 == 0.0)
        {
            return 0.0;
        }

        return dotProduct / (Math.Sqrt(magnitude1) * Math.Sqrt(magnitude2));
    }
}
