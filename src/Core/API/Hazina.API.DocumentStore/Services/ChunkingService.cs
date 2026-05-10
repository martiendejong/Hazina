using Hazina.API.DocumentStore.Models;

namespace Hazina.API.DocumentStore.Services;

/// <summary>
/// Service for chunking text documents
/// </summary>
public class ChunkingService
{
    public List<string> ChunkText(string text, ChunkingStrategy strategy, int chunkSize, int overlap)
    {
        return strategy switch
        {
            ChunkingStrategy.Fixed => ChunkFixed(text, chunkSize, overlap),
            ChunkingStrategy.Semantic => ChunkSemantic(text, chunkSize, overlap),
            ChunkingStrategy.SlidingWindow => ChunkSlidingWindow(text, chunkSize, overlap),
            ChunkingStrategy.Paragraph => ChunkByParagraph(text, chunkSize),
            _ => ChunkFixed(text, chunkSize, overlap)
        };
    }

    private List<string> ChunkFixed(string text, int chunkSize, int overlap)
    {
        var chunks = new List<string>();
        var position = 0;

        while (position < text.Length)
        {
            var length = Math.Min(chunkSize, text.Length - position);
            var chunk = text.Substring(position, length);
            chunks.Add(chunk.Trim());

            position += chunkSize - overlap;
        }

        return chunks.Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
    }

    private List<string> ChunkSemantic(string text, int chunkSize, int overlap)
    {
        // Split by sentences then group
        var sentences = text.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
        var chunks = new List<string>();
        var currentChunk = new List<string>();
        var currentLength = 0;

        foreach (var sentence in sentences)
        {
            var trimmed = sentence.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;

            if (currentLength + trimmed.Length > chunkSize && currentChunk.Any())
            {
                chunks.Add(string.Join(". ", currentChunk) + ".");

                // Keep last sentence for overlap
                if (overlap > 0 && currentChunk.Any())
                {
                    currentChunk = new List<string> { currentChunk.Last() };
                    currentLength = currentChunk.Last().Length;
                }
                else
                {
                    currentChunk.Clear();
                    currentLength = 0;
                }
            }

            currentChunk.Add(trimmed);
            currentLength += trimmed.Length;
        }

        if (currentChunk.Any())
        {
            chunks.Add(string.Join(". ", currentChunk) + ".");
        }

        return chunks;
    }

    private List<string> ChunkSlidingWindow(string text, int chunkSize, int overlap)
    {
        return ChunkFixed(text, chunkSize, overlap);
    }

    private List<string> ChunkByParagraph(string text, int maxSize)
    {
        var paragraphs = text.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);
        var chunks = new List<string>();
        var currentChunk = new List<string>();
        var currentLength = 0;

        foreach (var para in paragraphs)
        {
            var trimmed = para.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;

            if (currentLength + trimmed.Length > maxSize && currentChunk.Any())
            {
                chunks.Add(string.Join("\n\n", currentChunk));
                currentChunk.Clear();
                currentLength = 0;
            }

            currentChunk.Add(trimmed);
            currentLength += trimmed.Length;
        }

        if (currentChunk.Any())
        {
            chunks.Add(string.Join("\n\n", currentChunk));
        }

        return chunks;
    }
}
