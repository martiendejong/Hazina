using Hazina.API.Generic.Entities;
using System.ComponentModel.DataAnnotations;

namespace Hazina.Demo.GenericApi.Entities;

/// <summary>
/// A document that can be stored, retrieved, and searched semantically.
/// Demonstrates the EmbeddableEntityBase for RAG integration.
/// </summary>
public class Document : EmbeddableEntityBase
{
    /// <summary>
    /// Document title
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Document content (main body text)
    /// </summary>
    [Required]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Document type (e.g., "article", "note", "report")
    /// </summary>
    [MaxLength(50)]
    public string? DocumentType { get; set; }

    /// <summary>
    /// Comma-separated tags for categorization
    /// </summary>
    [MaxLength(500)]
    public string? Tags { get; set; }

    /// <summary>
    /// Source URL or reference
    /// </summary>
    [MaxLength(1000)]
    public string? SourceUrl { get; set; }

    /// <summary>
    /// Author name
    /// </summary>
    [MaxLength(200)]
    public string? Author { get; set; }

    /// <summary>
    /// Returns the text that will be embedded for semantic search.
    /// Combines title and content for comprehensive search.
    /// </summary>
    public override string GetSearchableText()
    {
        var parts = new List<string> { Title, Content };

        if (!string.IsNullOrWhiteSpace(Tags))
            parts.Add($"Tags: {Tags}");

        if (!string.IsNullOrWhiteSpace(Author))
            parts.Add($"Author: {Author}");

        return string.Join("\n\n", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }
}

/// <summary>
/// A simple note entity - demonstrates basic EntityBase usage
/// </summary>
public class Note : EntityBase
{
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    public string? Content { get; set; }

    [MaxLength(100)]
    public string? Category { get; set; }

    public bool IsPinned { get; set; }
}

/// <summary>
/// A tag entity for organizing documents
/// </summary>
public class Tag : EntityBase
{
    [Required]
    [MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(7)]
    public string? Color { get; set; } // Hex color like #FF5733

    public int UsageCount { get; set; }
}
