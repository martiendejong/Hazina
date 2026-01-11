using System;
using System.Collections.Generic;

/// <summary>
/// Represents a logical grouping of chunks with a collective summary.
/// Used for hierarchical documents where chunks can be organized by sections.
/// </summary>
public class ChunkSet
{
    /// <summary>
    /// Unique identifier for this chunk set (e.g., "doc.txt:section:0").
    /// </summary>
    public string SetId { get; set; } = "";

    /// <summary>
    /// Parent document ID.
    /// </summary>
    public string DocumentId { get; set; } = "";

    /// <summary>
    /// List of chunk IDs belonging to this set.
    /// </summary>
    public List<string> ChunkIds { get; set; } = new();

    /// <summary>
    /// Summary describing the content of this chunk set.
    /// </summary>
    public string Summary { get; set; } = "";

    /// <summary>
    /// Zero-based index of the first chunk in this set.
    /// </summary>
    public int StartChunkIndex { get; set; }

    /// <summary>
    /// Zero-based index of the last chunk in this set.
    /// </summary>
    public int EndChunkIndex { get; set; }

    /// <summary>
    /// Optional section title from hierarchical chunking.
    /// </summary>
    public string? SectionTitle { get; set; }

    /// <summary>
    /// When this chunk set was created.
    /// </summary>
    public DateTime Created { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this chunk set was last modified.
    /// </summary>
    public DateTime LastModified { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Number of chunks in this set.
    /// </summary>
    public int ChunkCount => ChunkIds.Count;

    /// <summary>
    /// Create a chunk set from a collection of chunk IDs.
    /// </summary>
    public static ChunkSet Create(
        string documentId,
        int sectionIndex,
        IEnumerable<string> chunkIds,
        string summary,
        int startChunkIndex,
        int endChunkIndex,
        string? sectionTitle = null)
    {
        return new ChunkSet
        {
            SetId = $"{documentId}:section:{sectionIndex}",
            DocumentId = documentId,
            ChunkIds = new List<string>(chunkIds),
            Summary = summary,
            StartChunkIndex = startChunkIndex,
            EndChunkIndex = endChunkIndex,
            SectionTitle = sectionTitle
        };
    }
}
