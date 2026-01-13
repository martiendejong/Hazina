using System.ComponentModel.DataAnnotations;

namespace Hazina.API.Search.Models;

public class SearchRequest
{
    [Required]
    [StringLength(1000, MinimumLength = 1)]
    public string Query { get; set; } = string.Empty;

    [Range(1, 100)]
    public int? TopK { get; set; } = 10;

    [Range(0.0, 1.0)]
    public double? MinSimilarity { get; set; } = 0.7;

    public bool IncludeGraphContext { get; set; } = true;

    public bool IncludeCitations { get; set; } = true;
}

public class SearchResponse
{
    public string Answer { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public List<SourceDocument> Sources { get; set; } = new();
    public List<string> ReasoningPath { get; set; } = new();
    public SearchMetadata Metadata { get; set; } = new();
}

public class SourceDocument
{
    public string DocumentId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public double RelevanceScore { get; set; }
    public string Excerpt { get; set; } = string.Empty;
}

public class SearchMetadata
{
    public int TotalDocumentsSearched { get; set; }
    public long SearchTimeMs { get; set; }
}
