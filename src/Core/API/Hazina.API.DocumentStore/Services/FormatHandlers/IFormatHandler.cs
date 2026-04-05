using Hazina.API.DocumentStore.Models;

namespace Hazina.API.DocumentStore.Services.FormatHandlers;

/// <summary>
/// Interface for document format handlers
/// </summary>
public interface IFormatHandler
{
    /// <summary>
    /// Check if this handler can process the given format
    /// </summary>
    bool CanHandle(string mimeType, string extension);

    /// <summary>
    /// Extract text content from the document
    /// </summary>
    Task<ExtractedContent> ExtractAsync(Stream stream, string filename);
}
