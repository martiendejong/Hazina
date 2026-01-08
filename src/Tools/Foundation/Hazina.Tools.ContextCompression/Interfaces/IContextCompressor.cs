using Hazina.Tools.ContextCompression.Models;

namespace Hazina.Tools.ContextCompression.Interfaces;

/// <summary>
/// Interface for context compression implementations
/// </summary>
public interface IContextCompressor
{
    /// <summary>
    /// Compress context according to the request and options
    /// </summary>
    /// <param name="request">Compression request</param>
    /// <param name="options">Compression options</param>
    /// <returns>Compressed context</returns>
    Task<CompressedContext> CompressAsync(
        CompressionRequest request,
        CompressionOptions options);

    /// <summary>
    /// Check if this compressor supports the given strategy
    /// </summary>
    /// <param name="strategy">Compression strategy</param>
    /// <returns>True if supported</returns>
    bool SupportsStrategy(CompressionStrategy strategy);
}
