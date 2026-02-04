using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Hazina.App.HazinaCoder.Core.Documentation;

/// <summary>
/// Automatically indexes documentation on first run.
/// Scans README, docs/, and code comments to build knowledge base.
/// </summary>
/// <remarks>
/// Improvement #38: Auto-Index Documentation
///
/// Indexes:
/// - README.md files
/// - All markdown in docs/ directories
/// - XML documentation comments
/// - TODO/FIXME comments
/// - Architecture decision records (ADRs)
/// </remarks>
public class AutoIndexer
{
    private readonly List<DocumentEntry> _index = new();
    private readonly List<string> _indexedPaths = new();

    /// <summary>
    /// Index a repository
    /// </summary>
    public int IndexRepository(string repoPath)
    {
        if (!Directory.Exists(repoPath))
        {
            Console.WriteLine($"[AutoIndex] Path not found: {repoPath}");
            return 0;
        }

        Console.WriteLine($"[AutoIndex] Indexing repository: {repoPath}");
        int count = 0;

        // Index README
        var readmePath = Path.Combine(repoPath, "README.md");
        if (File.Exists(readmePath))
        {
            IndexMarkdown(readmePath);
            count++;
        }

        // Index docs/ directory
        var docsPath = Path.Combine(repoPath, "docs");
        if (Directory.Exists(docsPath))
        {
            var mdFiles = Directory.GetFiles(docsPath, "*.md", SearchOption.AllDirectories);
            foreach (var file in mdFiles)
            {
                IndexMarkdown(file);
                count++;
            }
        }

        // TODO: Index code comments (requires Roslyn)
        // TODO: Index TODO/FIXME comments
        // TODO: Generate embeddings for semantic search

        Console.WriteLine($"[AutoIndex] Indexed {count} documents");
        _indexedPaths.Add(repoPath);
        return count;
    }

    /// <summary>
    /// Index a markdown file
    /// </summary>
    private void IndexMarkdown(string filePath)
    {
        try
        {
            var content = File.ReadAllText(filePath);
            var entry = new DocumentEntry
            {
                FilePath = filePath,
                Type = DocumentType.Markdown,
                Content = content,
                Title = ExtractTitle(content),
                IndexedAt = DateTime.UtcNow
            };

            _index.Add(entry);
            Console.WriteLine($"  Indexed: {Path.GetFileName(filePath)}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Error indexing {filePath}: {ex.Message}");
        }
    }

    /// <summary>
    /// Extract title from markdown
    /// </summary>
    private string ExtractTitle(string markdown)
    {
        // Find first # heading
        var lines = markdown.Split('\n');
        var titleLine = lines.FirstOrDefault(l => l.TrimStart().StartsWith("# "));
        return titleLine?.TrimStart().TrimStart('#').Trim() ?? "Untitled";
    }

    /// <summary>
    /// Search indexed documents
    /// </summary>
    public IEnumerable<DocumentEntry> Search(string query)
    {
        // TODO: Use embeddings for semantic search
        // For now, simple keyword search
        var keywords = query.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return _index.Where(doc =>
            keywords.Any(k =>
                doc.Title.Contains(k, StringComparison.OrdinalIgnoreCase) ||
                doc.Content.Contains(k, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(doc => CalculateRelevance(doc, keywords));
    }

    /// <summary>
    /// Calculate relevance score
    /// </summary>
    private int CalculateRelevance(DocumentEntry doc, string[] keywords)
    {
        int score = 0;
        var contentLower = doc.Content.ToLower();
        var titleLower = doc.Title.ToLower();

        foreach (var keyword in keywords)
        {
            // Title matches worth more
            if (titleLower.Contains(keyword))
                score += 10;

            // Content matches
            var matches = contentLower.Split(keyword).Length - 1;
            score += matches;
        }

        return score;
    }

    /// <summary>
    /// Get index statistics
    /// </summary>
    public IndexStats GetStats()
    {
        return new IndexStats
        {
            TotalDocuments = _index.Count,
            MarkdownFiles = _index.Count(d => d.Type == DocumentType.Markdown),
            CodeFiles = _index.Count(d => d.Type == DocumentType.Code),
            IndexedRepositories = _indexedPaths.Count
        };
    }
}

/// <summary>
/// Single indexed document
/// </summary>
public class DocumentEntry
{
    public string FilePath { get; set; } = string.Empty;
    public DocumentType Type { get; set; }
    public string Content { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime IndexedAt { get; set; }
}

public enum DocumentType
{
    Markdown,
    Code,
    Comment,
    README
}

public class IndexStats
{
    public int TotalDocuments { get; set; }
    public int MarkdownFiles { get; set; }
    public int CodeFiles { get; set; }
    public int IndexedRepositories { get; set; }
}
