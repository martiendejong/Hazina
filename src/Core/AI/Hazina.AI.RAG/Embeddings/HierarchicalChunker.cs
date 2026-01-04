using Microsoft.Extensions.Logging;

namespace Hazina.AI.RAG.Embeddings;

/// <summary>
/// Hierarchical chunking for large documents
/// Splits large documents into sections, then chunks each section independently
/// Improves performance and quality for documents >5000 characters
/// </summary>
public class HierarchicalChunker
{
    private readonly SemanticSimilarityChunker _semanticChunker;
    private readonly ILogger<HierarchicalChunker>? _logger;

    public HierarchicalChunker(
        SemanticSimilarityChunker semanticChunker,
        ILogger<HierarchicalChunker>? logger = null)
    {
        _semanticChunker = semanticChunker ?? throw new ArgumentNullException(nameof(semanticChunker));
        _logger = logger;
    }

    /// <summary>
    /// Chunk large documents hierarchically
    /// </summary>
    public async Task<List<TextChunk>> ChunkAsync(
        string text,
        SemanticChunkingOptions options,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new List<TextChunk>();

        // Use hierarchical chunking only for large documents
        if (text.Length < options.HierarchicalThreshold)
        {
            _logger?.LogDebug("Document size {Size} below hierarchical threshold {Threshold}, using standard chunking",
                text.Length, options.HierarchicalThreshold);
            return await _semanticChunker.ChunkAsync(text, options, ct);
        }

        _logger?.LogInformation("Using hierarchical chunking for large document ({Size} chars)", text.Length);

        try
        {
            // 1. Split into sections
            var sections = SplitIntoSections(text, options);
            _logger?.LogDebug("Split into {Count} sections", sections.Count);

            // 2. Chunk each section independently
            var allChunks = new List<TextChunk>();
            int globalChunkIndex = 0;

            foreach (var section in sections)
            {
                try
                {
                    var sectionChunks = await _semanticChunker.ChunkAsync(section.Text, options, ct);

                    // Update chunk indices and positions
                    foreach (var chunk in sectionChunks)
                    {
                        chunk.Index = globalChunkIndex++;
                        chunk.StartPosition += section.StartPosition;
                        chunk.EndPosition += section.StartPosition;

                        // Add hierarchical metadata
                        chunk.Metadata["section_index"] = section.Index;
                        chunk.Metadata["section_title"] = section.Title ?? "Section " + (section.Index + 1);
                        chunk.Metadata["is_hierarchical"] = true;

                        allChunks.Add(chunk);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to chunk section {Index}, skipping", section.Index);
                }
            }

            _logger?.LogInformation("Hierarchical chunking complete: {Sections} sections -> {Chunks} chunks",
                sections.Count, allChunks.Count);

            return allChunks;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Hierarchical chunking failed, falling back to standard chunking");
            return await _semanticChunker.ChunkAsync(text, options, ct);
        }
    }

    /// <summary>
    /// Split text into sections based on structural markers
    /// </summary>
    private List<DocumentSection> SplitIntoSections(string text, SemanticChunkingOptions options)
    {
        var sections = new List<DocumentSection>();

        // Detect content type to determine section markers
        var contentType = options.ContentType ?? ContentTypeDetector.Detect(text);

        switch (contentType)
        {
            case DocumentContentType.Markdown:
                sections = SplitMarkdownSections(text);
                break;

            case DocumentContentType.Code:
                sections = SplitCodeSections(text);
                break;

            case DocumentContentType.TechnicalDoc:
                sections = SplitTechnicalDocSections(text);
                break;

            default:
                sections = SplitGenericSections(text, options.HierarchicalSectionSize);
                break;
        }

        // Ensure sections don't exceed maximum size
        var finalSections = new List<DocumentSection>();
        foreach (var section in sections)
        {
            if (section.Text.Length > options.HierarchicalSectionSize * 2)
            {
                // Split large sections further
                var subsections = SplitGenericSections(section.Text, options.HierarchicalSectionSize);
                foreach (var subsection in subsections)
                {
                    subsection.StartPosition += section.StartPosition;
                    subsection.Title = section.Title + " (Part " + (subsection.Index + 1) + ")";
                    finalSections.Add(subsection);
                }
            }
            else
            {
                finalSections.Add(section);
            }
        }

        // Re-index sections
        for (int i = 0; i < finalSections.Count; i++)
        {
            finalSections[i].Index = i;
        }

        return finalSections;
    }

    /// <summary>
    /// Split markdown document into sections by headers
    /// </summary>
    private List<DocumentSection> SplitMarkdownSections(string text)
    {
        var sections = new List<DocumentSection>();
        var lines = text.Split('\n');

        int currentPosition = 0;
        int sectionStart = 0;
        string? currentTitle = null;
        var sectionLines = new List<string>();

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            // Check for markdown header (# Header)
            if (line.TrimStart().StartsWith("#"))
            {
                // Save previous section
                if (sectionLines.Count > 0)
                {
                    var sectionText = string.Join("\n", sectionLines);
                    sections.Add(new DocumentSection
                    {
                        Index = sections.Count,
                        Text = sectionText,
                        StartPosition = sectionStart,
                        Title = currentTitle
                    });

                    sectionStart = currentPosition;
                    sectionLines.Clear();
                }

                // Extract title
                currentTitle = line.TrimStart('#').Trim();
            }

            sectionLines.Add(line);
            currentPosition += line.Length + 1; // +1 for newline
        }

        // Add final section
        if (sectionLines.Count > 0)
        {
            var sectionText = string.Join("\n", sectionLines);
            sections.Add(new DocumentSection
            {
                Index = sections.Count,
                Text = sectionText,
                StartPosition = sectionStart,
                Title = currentTitle
            });
        }

        return sections.Count > 0 ? sections : new List<DocumentSection>
        {
            new DocumentSection { Index = 0, Text = text, StartPosition = 0 }
        };
    }

    /// <summary>
    /// Split code document into sections by class/function definitions
    /// </summary>
    private List<DocumentSection> SplitCodeSections(string text)
    {
        var sections = new List<DocumentSection>();
        var lines = text.Split('\n');

        int currentPosition = 0;
        int sectionStart = 0;
        string? currentTitle = null;
        var sectionLines = new List<string>();

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.TrimStart();

            // Check for class/function/method definitions
            bool isDefinition = trimmed.StartsWith("class ") ||
                              trimmed.StartsWith("public class ") ||
                              trimmed.StartsWith("interface ") ||
                              trimmed.Contains(" class ") ||
                              System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^(public|private|protected)?\s*(static|async)?\s*\w+\s+\w+\s*\(");

            if (isDefinition && sectionLines.Count > 5) // Minimum section size
            {
                // Save previous section
                var sectionText = string.Join("\n", sectionLines);
                sections.Add(new DocumentSection
                {
                    Index = sections.Count,
                    Text = sectionText,
                    StartPosition = sectionStart,
                    Title = currentTitle
                });

                sectionStart = currentPosition;
                sectionLines.Clear();
                currentTitle = trimmed.Length > 50 ? trimmed.Substring(0, 50) + "..." : trimmed;
            }

            sectionLines.Add(line);
            currentPosition += line.Length + 1;
        }

        // Add final section
        if (sectionLines.Count > 0)
        {
            var sectionText = string.Join("\n", sectionLines);
            sections.Add(new DocumentSection
            {
                Index = sections.Count,
                Text = sectionText,
                StartPosition = sectionStart,
                Title = currentTitle
            });
        }

        return sections.Count > 0 ? sections : new List<DocumentSection>
        {
            new DocumentSection { Index = 0, Text = text, StartPosition = 0 }
        };
    }

    /// <summary>
    /// Split technical documentation into sections
    /// </summary>
    private List<DocumentSection> SplitTechnicalDocSections(string text)
    {
        // Try markdown splitting first (many technical docs use markdown)
        var markdownSections = SplitMarkdownSections(text);
        if (markdownSections.Count > 1)
            return markdownSections;

        // Fallback to generic splitting
        return SplitGenericSections(text, 5000);
    }

    /// <summary>
    /// Split text into generic sections by size
    /// </summary>
    private List<DocumentSection> SplitGenericSections(string text, int sectionSize)
    {
        var sections = new List<DocumentSection>();
        var paragraphs = text.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);

        var currentSection = new System.Text.StringBuilder();
        int currentPosition = 0;
        int sectionStart = 0;
        int sectionIndex = 0;

        foreach (var para in paragraphs)
        {
            if (currentSection.Length + para.Length > sectionSize && currentSection.Length > 0)
            {
                // Save current section
                sections.Add(new DocumentSection
                {
                    Index = sectionIndex++,
                    Text = currentSection.ToString().Trim(),
                    StartPosition = sectionStart,
                    Title = null
                });

                currentSection.Clear();
                sectionStart = currentPosition;
            }

            currentSection.Append(para);
            currentSection.Append("\n\n");
            currentPosition += para.Length + 2;
        }

        // Add final section
        if (currentSection.Length > 0)
        {
            sections.Add(new DocumentSection
            {
                Index = sectionIndex,
                Text = currentSection.ToString().Trim(),
                StartPosition = sectionStart,
                Title = null
            });
        }

        return sections.Count > 0 ? sections : new List<DocumentSection>
        {
            new DocumentSection { Index = 0, Text = text, StartPosition = 0 }
        };
    }
}

/// <summary>
/// Represents a section of a document
/// </summary>
public class DocumentSection
{
    public int Index { get; set; }
    public string Text { get; set; } = string.Empty;
    public int StartPosition { get; set; }
    public string? Title { get; set; }
}
