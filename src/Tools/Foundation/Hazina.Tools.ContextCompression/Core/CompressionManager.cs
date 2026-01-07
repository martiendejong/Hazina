using Hazina.Tools.ContextCompression.Interfaces;
using Hazina.Tools.ContextCompression.Models;
using Hazina.Tools.ContextCompression.Strategies;
using Hazina.Tools.ContextCompression.Utilities;

namespace Hazina.Tools.ContextCompression.Core;

/// <summary>
/// Manages context compression with automatic strategy selection
/// </summary>
public class CompressionManager
{
    private readonly Dictionary<CompressionStrategy, IContextCompressor> _compressors;
    private readonly ITokenCounter _tokenCounter;
    private readonly CompressionOptions _defaultOptions;

    /// <summary>
    /// Create a new compression manager
    /// </summary>
    /// <param name="options">Default compression options</param>
    public CompressionManager(CompressionOptions? options = null)
    {
        _defaultOptions = options ?? new CompressionOptions();
        _tokenCounter = new Utilities.TokenCounter(_defaultOptions.TokenizerModel);

        // Initialize compressors
        _compressors = new Dictionary<CompressionStrategy, IContextCompressor>
        {
            [CompressionStrategy.Ast] = new AstCompressor(_tokenCounter),
            [CompressionStrategy.Diff] = new DiffCompressor(_tokenCounter),
            [CompressionStrategy.AstWithSymbols] = new AstCompressor(_tokenCounter)
        };
    }

    /// <summary>
    /// Compress context according to the request
    /// </summary>
    /// <param name="request">Compression request</param>
    /// <param name="options">Optional compression options (uses defaults if not provided)</param>
    /// <returns>Compressed context</returns>
    public async Task<CompressedContext> CompressAsync(
        CompressionRequest request,
        CompressionOptions? options = null)
    {
        var effectiveOptions = options ?? _defaultOptions;

        // Auto-select strategy if requested
        var strategy = request.Strategy == CompressionStrategy.Auto
            ? SelectStrategy(request)
            : request.Strategy;

        // Get appropriate compressor
        if (!_compressors.TryGetValue(strategy, out var compressor))
        {
            throw new NotSupportedException($"Compression strategy {strategy} is not supported");
        }

        // Compress
        var result = await compressor.CompressAsync(request, effectiveOptions);

        // Add metadata
        result.Metadata["strategy_selected"] = strategy.ToString();
        result.Metadata["files_processed"] = request.FilePaths.Count;
        result.Metadata["query"] = request.Query;

        return result;
    }

    /// <summary>
    /// Select the best compression strategy based on the request
    /// </summary>
    private CompressionStrategy SelectStrategy(CompressionRequest request)
    {
        // Single file edit with git repo -> Diff-based
        if (request.FilePaths.Count == 1 && !string.IsNullOrEmpty(request.RepositoryPath))
        {
            var gitPath = Path.Combine(request.RepositoryPath, ".git");
            if (Directory.Exists(gitPath))
            {
                return CompressionStrategy.Diff;
            }
        }

        // Architecture or structure questions -> AST
        if (IsArchitectureQuery(request.Query))
        {
            return CompressionStrategy.Ast;
        }

        // Multiple files without specific query -> AST
        if (request.FilePaths.Count > 1 && string.IsNullOrWhiteSpace(request.Query))
        {
            return CompressionStrategy.Ast;
        }

        // Default to AST for code files
        return CompressionStrategy.Ast;
    }

    /// <summary>
    /// Determine if query is about architecture or structure
    /// </summary>
    private bool IsArchitectureQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return false;

        var queryLower = query.ToLowerInvariant();

        var architectureKeywords = new[]
        {
            "architecture", "structure", "design", "pattern",
            "how does", "how is", "organize", "overview",
            "what classes", "what interfaces", "what methods",
            "class hierarchy", "inheritance", "dependencies"
        };

        return architectureKeywords.Any(keyword => queryLower.Contains(keyword));
    }

    /// <summary>
    /// Get compression statistics for a set of files
    /// </summary>
    /// <param name="filePaths">Files to analyze</param>
    /// <returns>Statistics about potential compression</returns>
    public async Task<CompressionStats> GetCompressionStatsAsync(List<string> filePaths)
    {
        var stats = new CompressionStats();
        int totalOriginalTokens = 0;

        foreach (var filePath in filePaths)
        {
            if (!File.Exists(filePath))
                continue;

            var code = await File.ReadAllTextAsync(filePath);
            var tokens = _tokenCounter.Count(code);
            totalOriginalTokens += tokens;

            stats.FileStats[filePath] = new FileCompressionStats
            {
                OriginalTokens = tokens,
                OriginalSize = code.Length
            };
        }

        stats.TotalOriginalTokens = totalOriginalTokens;
        return stats;
    }
}

/// <summary>
/// Statistics about compression potential
/// </summary>
public class CompressionStats
{
    public int TotalOriginalTokens { get; set; }
    public Dictionary<string, FileCompressionStats> FileStats { get; set; } = new();
}

/// <summary>
/// Statistics for a single file
/// </summary>
public class FileCompressionStats
{
    public int OriginalTokens { get; set; }
    public int OriginalSize { get; set; }
}
