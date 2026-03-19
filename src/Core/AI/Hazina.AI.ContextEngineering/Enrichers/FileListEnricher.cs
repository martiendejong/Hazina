using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Hazina.AI.ContextEngineering.Enrichers;

/// <summary>
/// Enriches context with project file list
/// </summary>
public class FileListEnricher : IMessageEnricher
{
    private readonly ILogger<FileListEnricher> _logger;
    private readonly FileListOptions _options;

    public int Priority => 30; // Execute after relevancy
    public string Name => "FileList";

    public FileListEnricher(
        ILogger<FileListEnricher> logger,
        FileListOptions? options = null)
    {
        _logger = logger;
        _options = options ?? new FileListOptions();
    }

    public Task<MessageContext> EnrichAsync(MessageContext context, CancellationToken cancellationToken = default)
    {
        if (context.Files == null || context.Files.Count == 0)
        {
            _logger.LogDebug("[FILELIST-ENRICHER] No files to process");
            return Task.FromResult(context);
        }

        var originalCount = context.Files.Count;

        // Apply file limit
        if (context.Files.Count > _options.MaxFiles)
        {
            context.Files = context.Files.Take(_options.MaxFiles).ToList();

            _logger.LogDebug(
                "[FILELIST-ENRICHER] File list truncated | Original: {Original} | Kept: {Kept}",
                originalCount,
                context.Files.Count);
        }

        // Estimate tokens (file path approximately 50 chars = ~12 tokens)
        var fileTokens = context.Files.Count * 12;
        context.EstimatedTokens += fileTokens;

        _logger.LogDebug(
            "[FILELIST-ENRICHER] File list processed | Files: {Count} | Estimated Tokens: {Tokens}",
            context.Files.Count,
            fileTokens);

        return Task.FromResult(context);
    }
}

/// <summary>
/// Configuration options for file list enricher
/// </summary>
public class FileListOptions
{
    /// <summary>
    /// Maximum number of files to include
    /// </summary>
    public int MaxFiles { get; set; } = 100;

    /// <summary>
    /// Whether to include full paths or just filenames
    /// </summary>
    public bool IncludeFullPaths { get; set; } = true;

    /// <summary>
    /// File extensions to filter (empty = all)
    /// </summary>
    public string[] FilterExtensions { get; set; } = Array.Empty<string>();
}
