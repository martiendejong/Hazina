using Hazina.Tools.ContextCompression.Interfaces;
using System.Text;

namespace Hazina.Tools.ContextCompression.Utilities;

/// <summary>
/// Token counter implementation using character-based approximation
/// </summary>
/// <remarks>
/// This uses a simplified approximation: ~4 characters per token
/// For production use, integrate with tiktoken via SharpToken or TiktokenSharp
/// </remarks>
public class TokenCounter : ITokenCounter
{
    private readonly string _modelName;
    private const double CHARS_PER_TOKEN = 4.0;

    /// <summary>
    /// Create a new token counter with default model (gpt-4)
    /// </summary>
    public TokenCounter() : this("gpt-4")
    {
    }

    /// <summary>
    /// Create a new token counter
    /// </summary>
    /// <param name="modelName">Model name (e.g., "gpt-4", "gpt-3.5-turbo")</param>
    public TokenCounter(string modelName)
    {
        if (string.IsNullOrEmpty(modelName))
            throw new ArgumentException("Model name cannot be null or empty", nameof(modelName));

        _modelName = modelName;
    }

    /// <summary>
    /// Count tokens in text using character-based approximation
    /// </summary>
    public int Count(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        // Approximation: ~4 characters per token
        return (int)Math.Ceiling(text.Length / CHARS_PER_TOKEN);
    }

    /// <summary>
    /// Truncate text to fit within max tokens
    /// </summary>
    public string Truncate(string text, int maxTokens)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        var estimatedTokens = Count(text);

        if (estimatedTokens <= maxTokens)
            return text;

        // Calculate approximate character limit
        var charLimit = (int)(maxTokens * CHARS_PER_TOKEN);

        if (text.Length <= charLimit)
            return text;

        return text.Substring(0, charLimit);
    }

    /// <summary>
    /// Split text into chunks of specified token size
    /// </summary>
    public List<string> ChunkByTokens(string text, int chunkSize, int overlap = 0)
    {
        if (string.IsNullOrEmpty(text))
            return new List<string>();

        if (overlap >= chunkSize)
            throw new ArgumentException("Overlap must be less than chunk size", nameof(overlap));

        var chunks = new List<string>();
        var charChunkSize = (int)(chunkSize * CHARS_PER_TOKEN);
        var charOverlap = (int)(overlap * CHARS_PER_TOKEN);
        var stride = charChunkSize - charOverlap;

        for (int i = 0; i < text.Length; i += stride)
        {
            var remainingLength = text.Length - i;
            var chunkLength = Math.Min(charChunkSize, remainingLength);

            if (chunkLength > 0)
            {
                chunks.Add(text.Substring(i, chunkLength));
            }

            // Break if we've reached the end
            if (i + charChunkSize >= text.Length)
                break;
        }

        return chunks;
    }

    /// <summary>
    /// Get the model name used for tokenization
    /// </summary>
    public string ModelName => _modelName;
}
