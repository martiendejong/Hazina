using System.Text;

namespace Hazina.AI.RAG.Embeddings;

/// <summary>
/// Splits text into chunks for embedding generation
/// </summary>
public class TextChunker
{
    private readonly TextChunkingOptions _options;

    public TextChunker(TextChunkingOptions? options = null)
    {
        _options = options ?? new TextChunkingOptions();
    }

    /// <summary>
    /// Split text into chunks with overlap
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
        // For now, use sentence-based chunking
        // In future, could use embeddings to find semantic boundaries
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
    public ChunkingStrategy Strategy { get; set; } = ChunkingStrategy.FixedSize;
    public int ChunkSize { get; set; } = 1000;
    public int OverlapSize { get; set; } = 200;
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
