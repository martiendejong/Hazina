namespace Hazina.Tools.ContextCompression.Interfaces;

/// <summary>
/// Interface for counting tokens in text
/// </summary>
public interface ITokenCounter
{
    /// <summary>
    /// Count tokens in text
    /// </summary>
    /// <param name="text">Text to count</param>
    /// <returns>Token count</returns>
    int Count(string text);

    /// <summary>
    /// Truncate text to fit within max tokens
    /// </summary>
    /// <param name="text">Text to truncate</param>
    /// <param name="maxTokens">Maximum number of tokens</param>
    /// <returns>Truncated text</returns>
    string Truncate(string text, int maxTokens);

    /// <summary>
    /// Split text into chunks of specified token size
    /// </summary>
    /// <param name="text">Text to split</param>
    /// <param name="chunkSize">Size of each chunk in tokens</param>
    /// <param name="overlap">Number of tokens to overlap between chunks</param>
    /// <returns>List of text chunks</returns>
    List<string> ChunkByTokens(string text, int chunkSize, int overlap = 0);
}
