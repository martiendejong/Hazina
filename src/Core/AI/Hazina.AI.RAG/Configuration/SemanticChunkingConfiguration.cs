using Hazina.AI.RAG.Embeddings;
using Microsoft.Extensions.Configuration;

namespace Hazina.AI.RAG.Configuration;

/// <summary>
/// Configuration loader for semantic chunking from appsettings.json
/// </summary>
public static class SemanticChunkingConfiguration
{
    /// <summary>
    /// Load semantic chunking options from configuration
    /// Expected section: "DocumentStore:SemanticChunking"
    /// </summary>
    public static SemanticChunkingOptions LoadFromConfiguration(IConfiguration configuration)
    {
        var options = new SemanticChunkingOptions();

        var section = configuration.GetSection("DocumentStore:SemanticChunking");
        if (!section.Exists())
        {
            // No configuration found, return defaults
            return options;
        }

        // Core settings
        options.Enabled = section.GetValue("Enabled", options.Enabled);

        var modeStr = section.GetValue<string?>("Mode");
        if (!string.IsNullOrEmpty(modeStr) && Enum.TryParse<SemanticChunkingMode>(modeStr, ignoreCase: true, out var mode))
        {
            options.Mode = mode;
        }

        // Sentence splitting
        var sentenceSection = section.GetSection("SentenceSplitting");
        if (sentenceSection.Exists())
        {
            options.SentenceSplitPattern = sentenceSection.GetValue("Pattern", options.SentenceSplitPattern) ?? options.SentenceSplitPattern;
            options.MinSentenceLength = sentenceSection.GetValue("MinLength", options.MinSentenceLength);
            options.MaxSentenceLength = sentenceSection.GetValue<int?>("MaxLength");
        }

        // Similarity detection
        var simSection = section.GetSection("SimilarityDetection");
        if (simSection.Exists())
        {
            options.SimilarityThreshold = simSection.GetValue("Threshold", options.SimilarityThreshold);
            options.SoftSimilarityThreshold = simSection.GetValue("SoftThreshold", options.SoftSimilarityThreshold);
            options.UseRollingAverage = simSection.GetValue("UseRollingAverage", options.UseRollingAverage);
            options.RollingWindowSize = simSection.GetValue("RollingWindowSize", options.RollingWindowSize);
        }

        // Chunk size constraints
        var sizeSection = section.GetSection("ChunkSizeConstraints");
        if (sizeSection.Exists())
        {
            options.MinChunkSize = sizeSection.GetValue("MinSize", options.MinChunkSize);
            options.MaxChunkSize = sizeSection.GetValue("MaxSize", options.MaxChunkSize);
            options.ParagraphBoundaryTolerance = sizeSection.GetValue<int?>("ParagraphBoundaryTolerance");
        }

        // Metadata extraction
        var metaSection = section.GetSection("Metadata");
        if (metaSection.Exists())
        {
            options.ExtractMetadata = metaSection.GetValue("Extract", options.ExtractMetadata);

            var tierStr = metaSection.GetValue<string?>("Tier");
            if (!string.IsNullOrEmpty(tierStr) && Enum.TryParse<MetadataExtractionTier>(tierStr, ignoreCase: true, out var tier))
            {
                options.MetadataTier = tier;
            }

            options.UseLLMMetadata = metaSection.GetValue("UseLLM", options.UseLLMMetadata);
            options.LLMMetadataModel = metaSection.GetValue<string?>("LLMModel");
            options.BatchLLMMetadata = metaSection.GetValue("BatchLLMCalls", options.BatchLLMMetadata);
            options.MaxLLMBatchSize = metaSection.GetValue("MaxBatchSize", options.MaxLLMBatchSize);
            options.TopKeywordsCount = metaSection.GetValue("TopKeywordsCount", options.TopKeywordsCount);
        }

        // Advanced options
        var advSection = section.GetSection("Advanced");
        if (advSection.Exists())
        {
            options.UseHierarchicalChunking = advSection.GetValue("UseHierarchical", options.UseHierarchicalChunking);
            options.HierarchicalSectionSize = advSection.GetValue("HierarchicalSectionSize", options.HierarchicalSectionSize);

            var fallbackStr = advSection.GetValue<string?>("FallbackStrategy");
            if (!string.IsNullOrEmpty(fallbackStr) && Enum.TryParse<ChunkingStrategy>(fallbackStr, ignoreCase: true, out var fallback))
            {
                options.FallbackStrategy = fallback;
            }

            options.CacheEmbeddings = advSection.GetValue("CacheEmbeddings", options.CacheEmbeddings);

            var contentTypeStr = advSection.GetValue<string?>("ContentType");
            if (!string.IsNullOrEmpty(contentTypeStr) && Enum.TryParse<DocumentContentType>(contentTypeStr, ignoreCase: true, out var contentType))
            {
                options.ContentType = contentType;
            }
        }

        return options;
    }

    /// <summary>
    /// Load text chunking options from configuration (including semantic options)
    /// Expected section: "DocumentStore"
    /// </summary>
    public static TextChunkingOptions LoadTextChunkingOptions(IConfiguration configuration)
    {
        var options = new TextChunkingOptions();

        var section = configuration.GetSection("DocumentStore");
        if (!section.Exists())
        {
            return options;
        }

        // Default chunking strategy
        var strategyStr = section.GetValue<string?>("DefaultChunkingStrategy");
        if (!string.IsNullOrEmpty(strategyStr) && Enum.TryParse<ChunkingStrategy>(strategyStr, ignoreCase: true, out var strategy))
        {
            options.Strategy = strategy;
        }

        // Basic options
        options.ChunkSize = section.GetValue("ChunkSize", options.ChunkSize);
        options.OverlapSize = section.GetValue("OverlapSize", options.OverlapSize);

        // Semantic options (if strategy is Semantic or always load for potential use)
        options.SemanticOptions = LoadFromConfiguration(configuration);

        return options;
    }
}
