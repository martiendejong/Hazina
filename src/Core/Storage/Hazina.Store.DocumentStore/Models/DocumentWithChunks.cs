using System.Collections.Generic;

public class DocumentWithChunks
{
    /// <summary>
    /// The document key/ID
    /// </summary>
    public string Key { get; set; } = "";

    /// <summary>
    /// The full text content of the document
    /// </summary>
    public string Content { get; set; } = "";

    /// <summary>
    /// Document metadata
    /// </summary>
    public DocumentMetadata? Metadata { get; set; }

    /// <summary>
    /// List of chunk keys for this document
    /// </summary>
    public List<string> ChunkKeys { get; set; } = new();
}
