using System.ComponentModel.DataAnnotations;

namespace Hazina.API.DocumentStore.Models;

/// <summary>
/// Search request model
/// </summary>
public class SearchRequest
{
    [Required]
    [StringLength(1000, MinimumLength = 1)]
    public string Query { get; set; } = string.Empty;

    [Range(1, 100)]
    public int? TopK { get; set; } = 10;

    [Range(0.0, 1.0)]
    public float? MinRelevanceScore { get; set; } = 0.7f;

    public bool IncludeSourceDocuments { get; set; } = true;
    public bool UseGenerativeAnswer { get; set; } = true;
    public string? SystemPrompt { get; set; }
}

/// <summary>
/// Search response model
/// </summary>
public class SearchResponse
{
    public string Query { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public List<SourceDocument> SourceDocuments { get; set; } = new();
    public SearchMetadata Metadata { get; set; } = new();
}

/// <summary>
/// Source document in search results
/// </summary>
public class SourceDocument
{
    public Guid DocumentId { get; set; }
    public int ChunkIndex { get; set; }
    public string Text { get; set; } = string.Empty;
    public float RelevanceScore { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Search metadata
/// </summary>
public class SearchMetadata
{
    public int TotalResults { get; set; }
    public int ProcessingTimeMs { get; set; }
    public string Model { get; set; } = string.Empty;
    public int TokensUsed { get; set; }
}
