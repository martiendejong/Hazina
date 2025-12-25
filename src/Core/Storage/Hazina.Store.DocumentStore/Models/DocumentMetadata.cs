using System;
using System.Collections.Generic;

public class DocumentMetadata
{
    public string Id { get; set; } = "";
    public string OriginalPath { get; set; } = "";
    public string MimeType { get; set; } = "text/plain";
    public long Size { get; set; }
    public DateTime Created { get; set; } = DateTime.UtcNow;
    public Dictionary<string, string> CustomMetadata { get; set; } = new();
    public bool IsBinary { get; set; }
    public string? Summary { get; set; }

    public string ToChunkText()
    {
        var lines = new List<string>
        {
            $"Document ID: {Id}",
            $"Original Path: {OriginalPath}",
            $"MIME Type: {MimeType}",
            $"Size: {Size} bytes",
            $"Created: {Created:yyyy-MM-dd HH:mm:ss} UTC",
            $"Is Binary: {IsBinary}"
        };

        if (!string.IsNullOrEmpty(Summary))
        {
            lines.Add($"Summary: {Summary}");
        }

        if (CustomMetadata.Count > 0)
        {
            lines.Add("Custom Metadata:");
            foreach (var kv in CustomMetadata)
            {
                lines.Add($"  {kv.Key}: {kv.Value}");
            }
        }

        return string.Join("\n", lines);
    }
}
