using Hazina.AI.RAG.Embeddings.Models;
using Microsoft.Extensions.Logging;

namespace Hazina.AI.RAG.Embeddings;

/// <summary>
/// Extracts rich metadata from text chunks using LLM
/// Premium tier: ~$0.0001 per chunk (using gpt-4o-mini)
/// Provides: summaries, topics, section types, importance scores, relationships
/// </summary>
public class LLMMetadataExtractor
{
    private readonly ILLMClient _llmClient;
    private readonly ILogger<LLMMetadataExtractor>? _logger;
    private readonly SemanticChunkingOptions _options;

    public LLMMetadataExtractor(
        ILLMClient llmClient,
        SemanticChunkingOptions options)
    {
        _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = null;
    }

    public LLMMetadataExtractor(
        ILLMClient llmClient,
        SemanticChunkingOptions options,
        ILogger<LLMMetadataExtractor>? logger)
    {
        _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
    }

    /// <summary>
    /// Extract metadata for a single chunk using LLM
    /// </summary>
    public async Task<ChunkMetadata?> ExtractMetadataAsync(
        string chunkText,
        CancellationToken ct = default)
    {
        try
        {
            var prompt = BuildSingleChunkPrompt(chunkText);

            var messages = new List<HazinaChatMessage>
            {
                new(HazinaMessageRole.System, "You are an expert at analyzing text and extracting structured metadata."),
                new(HazinaMessageRole.User, prompt)
            };

            var response = await _llmClient.GetResponse<ChunkMetadata>(
                messages,
                toolsContext: null,
                images: null,
                ct
            );

            return response.Result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to extract LLM metadata for chunk");
            return null;
        }
    }

    /// <summary>
    /// Extract metadata for multiple chunks in a batch (more efficient)
    /// Reduces latency and can reduce costs by combining multiple chunks in one call
    /// </summary>
    public async Task<List<ChunkMetadata>> ExtractBatchMetadataAsync(
        List<TextChunk> chunks,
        CancellationToken ct = default)
    {
        if (chunks == null || chunks.Count == 0)
            return new List<ChunkMetadata>();

        try
        {
            // Determine batch size
            var batchSize = _options.MaxLLMBatchSize;
            var results = new List<ChunkMetadata>();

            // Process in batches
            for (int i = 0; i < chunks.Count; i += batchSize)
            {
                var batch = chunks.Skip(i).Take(batchSize).ToList();
                var batchResults = await ProcessBatchAsync(batch, ct);
                results.AddRange(batchResults);

                _logger?.LogDebug(
                    "Processed metadata batch {BatchNum}/{TotalBatches} ({Count} chunks)",
                    (i / batchSize) + 1,
                    (chunks.Count + batchSize - 1) / batchSize,
                    batch.Count
                );
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Batch metadata extraction failed");
            return new List<ChunkMetadata>();
        }
    }

    /// <summary>
    /// Process a single batch of chunks
    /// </summary>
    private async Task<List<ChunkMetadata>> ProcessBatchAsync(
        List<TextChunk> chunks,
        CancellationToken ct)
    {
        try
        {
            var prompt = BuildBatchPrompt(chunks);

            var messages = new List<HazinaChatMessage>
            {
                new(HazinaMessageRole.System, "You are an expert at analyzing text and extracting structured metadata."),
                new(HazinaMessageRole.User, prompt)
            };

            var response = await _llmClient.GetResponse<BatchChunkMetadata>(
                messages,
                toolsContext: null,
                images: null,
                ct
            );

            if (response.Result?.Chunks != null && response.Result.Chunks.Count == chunks.Count)
            {
                return response.Result.Chunks;
            }
            else
            {
                _logger?.LogWarning(
                    "Batch response chunk count mismatch. Expected {Expected}, got {Actual}",
                    chunks.Count,
                    response.Result?.Chunks?.Count ?? 0
                );

                // Fallback: extract individually
                return await ExtractIndividuallyAsync(chunks, ct);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Batch processing failed, falling back to individual extraction");
            return await ExtractIndividuallyAsync(chunks, ct);
        }
    }

    /// <summary>
    /// Fallback: extract metadata for each chunk individually
    /// </summary>
    private async Task<List<ChunkMetadata>> ExtractIndividuallyAsync(
        List<TextChunk> chunks,
        CancellationToken ct)
    {
        var results = new List<ChunkMetadata>();

        foreach (var chunk in chunks)
        {
            var metadata = await ExtractMetadataAsync(chunk.Text, ct);
            if (metadata != null)
            {
                results.Add(metadata);
            }
            else
            {
                // Add default metadata if extraction fails
                results.Add(CreateDefaultMetadata(chunk.Text));
            }
        }

        return results;
    }

    /// <summary>
    /// Build prompt for single chunk metadata extraction
    /// </summary>
    private string BuildSingleChunkPrompt(string chunkText)
    {
        var maxLength = Math.Min(chunkText.Length, 2000); // Truncate if too long
        var truncatedText = chunkText.Length > maxLength
            ? chunkText.Substring(0, maxLength) + "..."
            : chunkText;

        return $@"Analyze the following text chunk and extract structured metadata:

CHUNK TEXT:
{truncatedText}

EXTRACT:
1. Summary: A concise one-sentence summary of the main idea
2. Topic: The primary topic or subject matter (2-4 words)
3. Keywords: 3-5 key terms or concepts (as a list)
4. SectionType: The type of content from this list: {string.Join(", ", SectionTypes.All)}
5. ImportanceScore: Rate importance 1-10 (10 = most important/central to understanding)
6. ReferencedTopics: Other topics or concepts this chunk references (list, can be empty)

Provide accurate, specific metadata that will help with retrieval and understanding.";
    }

    /// <summary>
    /// Build prompt for batch metadata extraction
    /// </summary>
    private string BuildBatchPrompt(List<TextChunk> chunks)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("Analyze the following text chunks and extract structured metadata for EACH chunk.");
        sb.AppendLine();
        sb.AppendLine($"Number of chunks: {chunks.Count}");
        sb.AppendLine();

        for (int i = 0; i < chunks.Count; i++)
        {
            var chunk = chunks[i];
            var maxLength = Math.Min(chunk.Text.Length, 500); // Truncate for batch
            var truncatedText = chunk.Text.Length > maxLength
                ? chunk.Text.Substring(0, maxLength) + "..."
                : chunk.Text;

            sb.AppendLine($"=== CHUNK {i + 1} ===");
            sb.AppendLine(truncatedText);
            sb.AppendLine();
        }

        sb.AppendLine("For EACH chunk, extract:");
        sb.AppendLine("1. Summary: One-sentence summary");
        sb.AppendLine("2. Topic: Primary topic (2-4 words)");
        sb.AppendLine("3. Keywords: 3-5 key terms");
        sb.AppendLine($"4. SectionType: One of: {string.Join(", ", SectionTypes.All.Take(8))}... (see full list in schema)");
        sb.AppendLine("5. ImportanceScore: 1-10 (10 = most important)");
        sb.AppendLine("6. ReferencedTopics: Related topics (list, can be empty)");
        sb.AppendLine();
        sb.AppendLine($"Return metadata for all {chunks.Count} chunks in order.");

        return sb.ToString();
    }

    /// <summary>
    /// Create default metadata when LLM extraction fails
    /// </summary>
    private ChunkMetadata CreateDefaultMetadata(string chunkText)
    {
        var firstSentence = chunkText.Length > 100
            ? chunkText.Substring(0, 97) + "..."
            : chunkText;

        return new ChunkMetadata
        {
            Summary = firstSentence,
            Topic = "Unknown",
            Keywords = new List<string>(),
            SectionType = SectionTypes.Other,
            ImportanceScore = 5,
            ReferencedTopics = new List<string>()
        };
    }

    /// <summary>
    /// Enrich text chunks with LLM-generated metadata (in-place)
    /// </summary>
    public async Task EnrichChunksAsync(
        List<TextChunk> chunks,
        CancellationToken ct = default)
    {
        if (chunks == null || chunks.Count == 0)
            return;

        _logger?.LogInformation("Enriching {Count} chunks with LLM metadata", chunks.Count);

        List<ChunkMetadata> metadataList;

        if (_options.BatchLLMMetadata && chunks.Count > 1)
        {
            // Batch extraction
            metadataList = await ExtractBatchMetadataAsync(chunks, ct);
        }
        else
        {
            // Individual extraction
            metadataList = new List<ChunkMetadata>();
            foreach (var chunk in chunks)
            {
                var metadata = await ExtractMetadataAsync(chunk.Text, ct);
                metadataList.Add(metadata ?? CreateDefaultMetadata(chunk.Text));
            }
        }

        // Apply metadata to chunks
        for (int i = 0; i < Math.Min(chunks.Count, metadataList.Count); i++)
        {
            var chunk = chunks[i];
            var metadata = metadataList[i];

            chunk.Metadata["llm_summary"] = metadata.Summary;
            chunk.Metadata["llm_topic"] = metadata.Topic;
            chunk.Metadata["llm_keywords"] = metadata.Keywords;
            chunk.Metadata["llm_section_type"] = metadata.SectionType;
            chunk.Metadata["llm_importance_score"] = metadata.ImportanceScore;
            chunk.Metadata["llm_referenced_topics"] = metadata.ReferencedTopics;
        }

        _logger?.LogInformation("Successfully enriched {Count} chunks with LLM metadata", chunks.Count);
    }
}
