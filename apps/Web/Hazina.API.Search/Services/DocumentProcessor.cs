using Hazina.API.Search.Models;
using Hazina.API.Search.Services.FormatHandlers;

namespace Hazina.API.Search.Services;

/// <summary>
/// Main document processing orchestrator
/// </summary>
public class DocumentProcessor
{
    private readonly List<IFormatHandler> _formatHandlers;
    private readonly ChunkingService _chunkingService;
    private readonly ILogger<DocumentProcessor> _logger;

    public DocumentProcessor(
        IEnumerable<IFormatHandler> formatHandlers,
        ChunkingService chunkingService,
        ILogger<DocumentProcessor> logger)
    {
        _formatHandlers = formatHandlers.ToList();
        _chunkingService = chunkingService;
        _logger = logger;
    }

    public async Task<(ExtractedContent Content, List<string> Chunks)> ProcessDocumentAsync(
        Stream stream,
        string filename,
        string mimeType,
        RAGStoreConfig config)
    {
        // Find appropriate handler
        var extension = Path.GetExtension(filename).ToLowerInvariant();
        var handler = _formatHandlers.FirstOrDefault(h => h.CanHandle(mimeType, extension));

        if (handler == null)
        {
            throw new NotSupportedException($"Format not supported: {mimeType} ({extension})");
        }

        _logger.LogInformation("Processing document {Filename} with handler {Handler}",
            filename, handler.GetType().Name);

        // Extract content
        var content = await handler.ExtractAsync(stream, filename);

        // Chunk the text
        var chunks = _chunkingService.ChunkText(
            content.Text,
            config.ChunkingStrategy,
            config.ChunkSize,
            config.ChunkOverlap);

        _logger.LogInformation("Extracted {CharCount} characters, created {ChunkCount} chunks",
            content.Text.Length, chunks.Count);

        return (content, chunks);
    }

    public async Task<(ExtractedContent Content, List<string> Chunks)> ProcessTextAsync(
        string text,
        RAGStoreConfig config)
    {
        var content = new ExtractedContent
        {
            Text = text,
            MimeType = "text/plain",
            PageCount = 1,
            Metadata = new Dictionary<string, object>
            {
                ["format"] = "text",
                ["source"] = "direct-input"
            }
        };

        var chunks = _chunkingService.ChunkText(
            text,
            config.ChunkingStrategy,
            config.ChunkSize,
            config.ChunkOverlap);

        return await Task.FromResult((content, chunks));
    }
}
