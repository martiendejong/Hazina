using Hazina.AI.RAG.Utilities;
using Hazina.Store.EmbeddingStore;
using Microsoft.Extensions.Logging;

namespace Hazina.AI.RAG.Embeddings;

/// <summary>
/// Semantic chunking using embedding similarity to detect topic boundaries
/// Cost-efficient: Uses embeddings instead of expensive LLM calls
/// Cost: Near-zero if embeddings are reused for retrieval
/// </summary>
public class SemanticSimilarityChunker
{
    private readonly IEmbeddingGenerator _embeddingGenerator;
    private readonly ILogger<SemanticSimilarityChunker>? _logger;
    private readonly BasicMetadataExtractor _metadataExtractor;

    public SemanticSimilarityChunker(
        IEmbeddingGenerator embeddingGenerator,
        ILogger<SemanticSimilarityChunker>? logger = null)
    {
        _embeddingGenerator = embeddingGenerator ?? throw new ArgumentNullException(nameof(embeddingGenerator));
        _logger = logger;
        _metadataExtractor = new BasicMetadataExtractor();
    }

    /// <summary>
    /// Chunk text using semantic similarity analysis
    /// </summary>
    public async Task<List<TextChunk>> ChunkAsync(
        string text,
        SemanticChunkingOptions options,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new List<TextChunk>();

        if (!options.Enabled)
        {
            _logger?.LogWarning("Semantic chunking is disabled. Falling back to {Strategy}", options.FallbackStrategy);
            return FallbackChunk(text, options);
        }

        try
        {
            _logger?.LogInformation("Starting semantic similarity chunking. Text length: {Length}", text.Length);

            // 1. Split into sentences
            var sentences = SplitIntoSentences(text, options);
            _logger?.LogDebug("Split into {Count} sentences", sentences.Count);

            if (sentences.Count == 0)
                return new List<TextChunk>();

            // If only one sentence, return it as single chunk
            if (sentences.Count == 1)
            {
                return new List<TextChunk>
                {
                    CreateSingleChunk(text, 0, options)
                };
            }

            // 2. Generate embeddings for all sentences (batch for efficiency)
            var embeddings = await GenerateSentenceEmbeddingsAsync(sentences, ct);
            _logger?.LogDebug("Generated {Count} embeddings", embeddings.Count);

            // 3. Calculate pairwise similarities
            var similarities = CalculatePairwiseSimilarities(embeddings);
            _logger?.LogDebug("Calculated {Count} similarity scores", similarities.Count);

            // 4. Detect semantic boundaries
            var boundaries = DetectSemanticBoundaries(similarities, sentences, options);
            _logger?.LogInformation("Detected {Count} chunk boundaries", boundaries.Count - 1);

            // 5. Create chunks from boundaries
            var chunks = CreateChunksFromBoundaries(sentences, boundaries, text, options);
            _logger?.LogInformation("Created {Count} chunks", chunks.Count);

            // 6. Extract metadata (if enabled)
            if (options.ExtractMetadata)
            {
                EnrichWithBasicMetadata(chunks, options);
            }

            return chunks;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Semantic chunking failed. Falling back to {Strategy}", options.FallbackStrategy);
            return FallbackChunk(text, options);
        }
    }

    /// <summary>
    /// Split text into sentences using configured pattern
    /// </summary>
    private List<string> SplitIntoSentences(string text, SemanticChunkingOptions options)
    {
        return SentenceSplitter.Split(
            text,
            pattern: options.SentenceSplitPattern,
            minLength: options.MinSentenceLength,
            maxLength: options.MaxSentenceLength
        );
    }

    /// <summary>
    /// Generate embeddings for all sentences in batch
    /// </summary>
    private async Task<List<Embedding>> GenerateSentenceEmbeddingsAsync(
        List<string> sentences,
        CancellationToken ct)
    {
        try
        {
            // Batch generation is more efficient
            return await _embeddingGenerator.GenerateBatchAsync(sentences, ct);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Batch embedding generation failed, falling back to individual generation");

            // Fallback to individual generation
            var embeddings = new List<Embedding>();
            foreach (var sentence in sentences)
            {
                try
                {
                    var embedding = await _embeddingGenerator.GenerateAsync(sentence, ct);
                    embeddings.Add(embedding);
                }
                catch (Exception sentenceEx)
                {
                    _logger?.LogWarning(sentenceEx, "Failed to generate embedding for sentence, using zero vector");
                    // Use zero vector as placeholder
                    embeddings.Add(new Embedding(new double[_embeddingGenerator.Dimensions]));
                }
            }
            return embeddings;
        }
    }

    /// <summary>
    /// Calculate cosine similarity between adjacent sentences
    /// </summary>
    private List<double> CalculatePairwiseSimilarities(List<Embedding> embeddings)
    {
        var similarities = new List<double>();

        for (int i = 0; i < embeddings.Count - 1; i++)
        {
            try
            {
                var similarity = embeddings[i].CosineSimilarity(embeddings[i + 1]);
                similarities.Add(similarity);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to calculate similarity at index {Index}, using default 0.5", i);
                similarities.Add(0.5); // Default mid-value
            }
        }

        return similarities;
    }

    /// <summary>
    /// Detect semantic boundaries based on similarity drops
    /// </summary>
    private List<int> DetectSemanticBoundaries(
        List<double> similarities,
        List<string> sentences,
        SemanticChunkingOptions options)
    {
        var boundaries = new List<int> { 0 }; // Always start at 0

        if (sentences.Count <= 1)
        {
            boundaries.Add(sentences.Count);
            return boundaries;
        }

        // Optional: Apply rolling average to smooth out noise
        var smoothedSimilarities = options.UseRollingAverage
            ? ApplyRollingAverage(similarities, options.RollingWindowSize)
            : similarities;

        int currentChunkStart = 0;
        int currentChunkLength = sentences[0].Length;

        for (int i = 0; i < smoothedSimilarities.Count; i++)
        {
            var similarity = smoothedSimilarities[i];
            var nextSentenceLength = i + 1 < sentences.Count ? sentences[i + 1].Length : 0;

            // Calculate cumulative chunk length if we continue
            var projectedLength = currentChunkLength + nextSentenceLength;

            // Boundary conditions:
            // 1. Exceeds max chunk size (force split)
            // 2. Strong topic shift (similarity < threshold) AND meets min size
            // 3. Soft topic shift (similarity < soft threshold) AND chunk is reasonably sized

            bool exceeds MaxSize = projectedLength > options.MaxChunkSize;
            bool isStrongShift = similarity < options.SimilarityThreshold;
            bool isSoftShift = similarity < options.SoftSimilarityThreshold;
            bool meetsMinSize = currentChunkLength >= options.MinChunkSize;

            if (exceedsMaxSize || (isStrongShift && meetsMinSize) || (isSoftShift && currentChunkLength > options.MaxChunkSize * 0.7))
            {
                // Create boundary here
                boundaries.Add(i + 1);
                _logger?.LogDebug("Boundary at sentence {Index}: similarity={Similarity:F2}, length={Length}",
                    i + 1, similarity, currentChunkLength);

                currentChunkStart = i + 1;
                currentChunkLength = nextSentenceLength;
            }
            else
            {
                // Continue current chunk
                currentChunkLength += nextSentenceLength + 1; // +1 for space
            }
        }

        // Always add final boundary
        if (boundaries[^1] != sentences.Count)
        {
            boundaries.Add(sentences.Count);
        }

        return boundaries;
    }

    /// <summary>
    /// Apply rolling average to similarity scores to smooth out noise
    /// </summary>
    private List<double> ApplyRollingAverage(List<double> similarities, int windowSize)
    {
        if (windowSize <= 1 || similarities.Count <= windowSize)
            return similarities;

        var smoothed = new List<double>();
        int halfWindow = windowSize / 2;

        for (int i = 0; i < similarities.Count; i++)
        {
            int start = Math.Max(0, i - halfWindow);
            int end = Math.Min(similarities.Count - 1, i + halfWindow);

            var window = similarities.Skip(start).Take(end - start + 1);
            var average = window.Average();
            smoothed.Add(average);
        }

        return smoothed;
    }

    /// <summary>
    /// Create text chunks from detected boundaries
    /// </summary>
    private List<TextChunk> CreateChunksFromBoundaries(
        List<string> sentences,
        List<int> boundaries,
        string originalText,
        SemanticChunkingOptions options)
    {
        var chunks = new List<TextChunk>();
        int currentPosition = 0;

        for (int i = 0; i < boundaries.Count - 1; i++)
        {
            var startIdx = boundaries[i];
            var endIdx = boundaries[i + 1];

            // Get sentences for this chunk
            var chunkSentences = sentences.Skip(startIdx).Take(endIdx - startIdx).ToList();
            var chunkText = string.Join(" ", chunkSentences);

            chunks.Add(new TextChunk
            {
                Text = chunkText,
                Index = i,
                StartPosition = currentPosition,
                EndPosition = currentPosition + chunkText.Length,
                Metadata = new Dictionary<string, object>
                {
                    ["chunking_strategy"] = "semantic_similarity",
                    ["chunking_mode"] = options.Mode.ToString(),
                    ["sentence_count"] = chunkSentences.Count,
                    ["boundary_type"] = "semantic",
                    ["similarity_threshold"] = options.SimilarityThreshold
                }
            });

            currentPosition += chunkText.Length + 1; // +1 for space between chunks
        }

        return chunks;
    }

    /// <summary>
    /// Enrich chunks with basic metadata (keywords, summaries, stats)
    /// </summary>
    private void EnrichWithBasicMetadata(List<TextChunk> chunks, SemanticChunkingOptions options)
    {
        foreach (var chunk in chunks)
        {
            try
            {
                var basicMetadata = _metadataExtractor.ExtractMetadata(chunk.Text);

                // Merge basic metadata into chunk metadata
                foreach (var kvp in basicMetadata)
                {
                    chunk.Metadata[kvp.Key] = kvp.Value;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to extract metadata for chunk {Index}", chunk.Index);
            }
        }
    }

    /// <summary>
    /// Create a single chunk from entire text
    /// </summary>
    private TextChunk CreateSingleChunk(string text, int index, SemanticChunkingOptions options)
    {
        var chunk = new TextChunk
        {
            Text = text,
            Index = index,
            StartPosition = 0,
            EndPosition = text.Length,
            Metadata = new Dictionary<string, object>
            {
                ["chunking_strategy"] = "semantic_similarity",
                ["chunking_mode"] = options.Mode.ToString(),
                ["sentence_count"] = 1,
                ["boundary_type"] = "single"
            }
        };

        if (options.ExtractMetadata)
        {
            var metadata = _metadataExtractor.ExtractMetadata(text);
            foreach (var kvp in metadata)
            {
                chunk.Metadata[kvp.Key] = kvp.Value;
            }
        }

        return chunk;
    }

    /// <summary>
    /// Fallback to simpler chunking strategy if semantic chunking fails
    /// </summary>
    private List<TextChunk> FallbackChunk(string text, SemanticChunkingOptions options)
    {
        _logger?.LogInformation("Using fallback chunking strategy: {Strategy}", options.FallbackStrategy);

        var textChunker = new TextChunker(new TextChunkingOptions
        {
            Strategy = options.FallbackStrategy,
            ChunkSize = options.MaxChunkSize,
            OverlapSize = 0
        });

        var fallbackChunks = textChunker.ChunkText(text);

        // Add metadata indicating fallback was used
        foreach (var chunk in fallbackChunks)
        {
            chunk.Metadata["chunking_strategy"] = "fallback";
            chunk.Metadata["fallback_from"] = "semantic_similarity";
            chunk.Metadata["fallback_reason"] = "semantic_chunking_failed";
        }

        return fallbackChunks;
    }
}
