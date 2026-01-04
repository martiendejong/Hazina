using System.Text;
using Hazina.Store.EmbeddingStore;
using Microsoft.Extensions.Logging;

namespace Hazina.AI.RAG.Embeddings;

/// <summary>
/// Splits text into chunks for embedding generation
/// Supports multiple strategies including semantic similarity-based chunking
/// </summary>
public class TextChunker
{
    private readonly TextChunkingOptions _options;
    private readonly IEmbeddingGenerator? _embeddingGenerator;
    private readonly ILLMClient? _llmClient;
    private readonly ILogger<TextChunker>? _logger;

    public TextChunker(TextChunkingOptions? options = null)
    {
        _options = options ?? new TextChunkingOptions();
    }

    public TextChunker(
        IEmbeddingGenerator? embeddingGenerator,
        TextChunkingOptions? options = null,
        ILogger<TextChunker>? logger = null)
    {
        _embeddingGenerator = embeddingGenerator;
        _options = options ?? new TextChunkingOptions();
        _logger = logger;
    }

    public TextChunker(
        IEmbeddingGenerator? embeddingGenerator,
        ILLMClient? llmClient,
        TextChunkingOptions? options = null,
        ILogger<TextChunker>? logger = null)
    {
        _embeddingGenerator = embeddingGenerator;
        _llmClient = llmClient;
        _options = options ?? new TextChunkingOptions();
        _logger = logger;
    }

    /// <summary>
    /// Split text into chunks with overlap (synchronous)
    /// Note: Semantic chunking requires async API - use ChunkTextAsync for semantic strategy
    /// </summary>
    public List<TextChunk> ChunkText(string text, Dictionary<string, object>? metadata = null)
    {
        var chunks = new List<TextChunk>();

        if (string.IsNullOrWhiteSpace(text))
            return chunks;

        switch (_options.Strategy)
        {
            case ChunkingStrategy.FixedSize:
                chunks = ChunkByFixedSize(text, metadata);
                break;
            case ChunkingStrategy.Sentence:
                chunks = ChunkBySentence(text, metadata);
                break;
            case ChunkingStrategy.Paragraph:
                chunks = ChunkByParagraph(text, metadata);
                break;
            case ChunkingStrategy.Semantic:
                chunks = ChunkBySemantic(text, metadata);
                break;
        }

        return chunks;
    }

    /// <summary>
    /// Split text into chunks asynchronously (supports semantic chunking)
    /// </summary>
    /// <param name="text">Text to chunk</param>
    /// <param name="options">Chunking options (overrides constructor options if provided)</param>
    /// <param name="metadata">Base metadata to include in all chunks</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of text chunks</returns>
    public async Task<List<TextChunk>> ChunkTextAsync(
        string text,
        TextChunkingOptions? options = null,
        Dictionary<string, object>? metadata = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new List<TextChunk>();

        var effectiveOptions = options ?? _options;

        // Semantic chunking requires async and embeddings
        if (effectiveOptions.Strategy == ChunkingStrategy.Semantic)
        {
            if (_embeddingGenerator == null)
            {
                _logger?.LogWarning(
                    "Semantic chunking requested but no embedding generator provided. " +
                    "Falling back to {FallbackStrategy}",
                    effectiveOptions.SemanticOptions?.FallbackStrategy ?? ChunkingStrategy.Paragraph);

                // Fallback to paragraph chunking
                effectiveOptions = new TextChunkingOptions
                {
                    Strategy = effectiveOptions.SemanticOptions?.FallbackStrategy ?? ChunkingStrategy.Paragraph,
                    ChunkSize = effectiveOptions.ChunkSize,
                    OverlapSize = effectiveOptions.OverlapSize
                };

                return ChunkText(text, metadata);
            }

            // Use semantic similarity chunker
            var semanticOptions = effectiveOptions.SemanticOptions ?? new SemanticChunkingOptions();
            var semanticChunker = new SemanticSimilarityChunker(_embeddingGenerator, _llmClient);

            List<TextChunk> chunks;

            // Use hierarchical chunking for large documents if enabled
            if (semanticOptions.UseHierarchicalChunking && text.Length >= semanticOptions.HierarchicalThreshold)
            {
                _logger?.LogDebug("Using hierarchical chunking for large document ({Length} chars)", text.Length);
                var hierarchicalChunker = new HierarchicalChunker(semanticChunker);
                chunks = await hierarchicalChunker.ChunkAsync(text, semanticOptions, ct);
            }
            else
            {
                chunks = await semanticChunker.ChunkAsync(text, semanticOptions, ct);
            }

            // Merge base metadata if provided
            if (metadata != null)
            {
                foreach (var chunk in chunks)
                {
                    foreach (var kvp in metadata)
                    {
                        chunk.Metadata[kvp.Key] = kvp.Value;
                    }
                }
            }

            return chunks;
        }

        // For non-semantic strategies, use synchronous chunking
        return ChunkText(text, metadata);
    }

    private List<TextChunk> ChunkByFixedSize(string text, Dictionary<string, object>? metadata)
    {
        var chunks = new List<TextChunk>();
        int position = 0;
        int chunkIndex = 0;

        while (position < text.Length)
        {
            int chunkSize = Math.Min(_options.ChunkSize, text.Length - position);

            // Try to find a good breaking point (space, newline)
            if (position + chunkSize < text.Length)
            {
                int breakPoint = text.LastIndexOfAny(new[] { ' ', '\n', '\r', '.', ',', ';' }, position + chunkSize, chunkSize / 2);
                if (breakPoint > position)
                {
                    chunkSize = breakPoint - position + 1;
                }
            }

            var chunkText = text.Substring(position, chunkSize).Trim();

            chunks.Add(new TextChunk
            {
                Text = chunkText,
                Index = chunkIndex++,
                StartPosition = position,
                EndPosition = position + chunkSize,
                Metadata = metadata != null ? new Dictionary<string, object>(metadata) : new()
            });

            // Move forward with overlap
            position += chunkSize - _options.OverlapSize;
        }

        return chunks;
    }

    private List<TextChunk> ChunkBySentence(string text, Dictionary<string, object>? metadata)
    {
        var chunks = new List<TextChunk>();
        var sentences = SplitIntoSentences(text);

        var currentChunk = new StringBuilder();
        int currentPosition = 0;
        int chunkIndex = 0;
        int chunkStart = 0;

        foreach (var sentence in sentences)
        {
            if (currentChunk.Length + sentence.Length > _options.ChunkSize && currentChunk.Length > 0)
            {
                // Create chunk
                chunks.Add(new TextChunk
                {
                    Text = currentChunk.ToString().Trim(),
                    Index = chunkIndex++,
                    StartPosition = chunkStart,
                    EndPosition = currentPosition,
                    Metadata = metadata != null ? new Dictionary<string, object>(metadata) : new()
                });

                currentChunk.Clear();
                chunkStart = currentPosition;
            }

            currentChunk.Append(sentence);
            currentPosition += sentence.Length;
        }

        // Add final chunk
        if (currentChunk.Length > 0)
        {
            chunks.Add(new TextChunk
            {
                Text = currentChunk.ToString().Trim(),
                Index = chunkIndex,
                StartPosition = chunkStart,
                EndPosition = currentPosition,
                Metadata = metadata != null ? new Dictionary<string, object>(metadata) : new()
            });
        }

        return chunks;
    }

    private List<TextChunk> ChunkByParagraph(string text, Dictionary<string, object>? metadata)
    {
        var chunks = new List<TextChunk>();
        var paragraphs = text.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);

        int position = 0;
        int chunkIndex = 0;

        foreach (var para in paragraphs)
        {
            if (para.Length > _options.ChunkSize)
            {
                // Split large paragraphs
                var subChunks = ChunkByFixedSize(para, metadata);
                foreach (var subChunk in subChunks)
                {
                    subChunk.Index = chunkIndex++;
                    subChunk.StartPosition += position;
                    subChunk.EndPosition += position;
                    chunks.Add(subChunk);
                }
            }
            else
            {
                chunks.Add(new TextChunk
                {
                    Text = para.Trim(),
                    Index = chunkIndex++,
                    StartPosition = position,
                    EndPosition = position + para.Length,
                    Metadata = metadata != null ? new Dictionary<string, object>(metadata) : new()
                });
            }

            position += para.Length + 2; // Account for paragraph separator
        }

        return chunks;
    }

    private List<TextChunk> ChunkBySemantic(string text, Dictionary<string, object>? metadata)
    {
        // Synchronous semantic chunking not supported (requires embeddings/async)
        // Use ChunkTextAsync() for semantic chunking
        _logger?.LogInformation(
            "Semantic chunking requires async API. Use ChunkTextAsync() for LLM-powered chunking. " +
            "Falling back to sentence-based chunking.");

        return ChunkBySentence(text, metadata);
    }

    private List<string> SplitIntoSentences(string text)
    {
        var sentences = new List<string>();
        var sentenceEndings = new[] { '.', '!', '?' };

        int start = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (sentenceEndings.Contains(text[i]))
            {
                // Check if it's really a sentence ending
                bool isEnd = true;

                // Not an abbreviation (e.g., "Dr.", "Mr.")
                if (i > 0 && char.IsUpper(text[i - 1]))
                    isEnd = false;

                // Followed by space and capital letter
                if (i + 2 < text.Length && (!char.IsWhiteSpace(text[i + 1]) || !char.IsUpper(text[i + 2])))
                    isEnd = false;

                if (isEnd || i == text.Length - 1)
                {
                    sentences.Add(text.Substring(start, i - start + 1));
                    start = i + 1;
                }
            }
        }

        // Add remaining text
        if (start < text.Length)
        {
            sentences.Add(text.Substring(start));
        }

        return sentences;
    }
}

/// <summary>
/// Text chunk with metadata
/// </summary>
public class TextChunk
{
    public string Text { get; set; } = string.Empty;
    public int Index { get; set; }
    public int StartPosition { get; set; }
    public int EndPosition { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Text chunking options
/// </summary>
public class TextChunkingOptions
{
    /// <summary>
    /// Chunking strategy to use
    /// Default: FixedSize (backwards compatible)
    /// </summary>
    public ChunkingStrategy Strategy { get; set; } = ChunkingStrategy.FixedSize;

    /// <summary>
    /// Chunk size in characters (for FixedSize, Sentence, Paragraph strategies)
    /// Default: 1000
    /// </summary>
    public int ChunkSize { get; set; } = 1000;

    /// <summary>
    /// Overlap size in characters (for FixedSize strategy)
    /// Default: 200
    /// </summary>
    public int OverlapSize { get; set; } = 200;

    /// <summary>
    /// Semantic chunking options (only used when Strategy = Semantic)
    /// null = use default semantic options
    /// </summary>
    public SemanticChunkingOptions? SemanticOptions { get; set; } = null;
}

/// <summary>
/// Chunking strategies
/// </summary>
public enum ChunkingStrategy
{
    FixedSize,
    Sentence,
    Paragraph,
    Semantic
}
