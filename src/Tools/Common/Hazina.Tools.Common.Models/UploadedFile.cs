using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

// Keep legacy namespace to minimize breaking changes across the codebase
namespace HazinaStore.Models
{
    public class UploadedFile : IEmbedding
    {
        public string Filename { get; set; }
        public string TextFilename { get; set; }
        public string Label { get; set; }
        public string Extension { get; set; }
        public int TokenCount { get; set; }
        public List<List<double>> Parts { get; set; } = new List<List<double>>();

        public string Id => Filename;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string Content { get; set; }

        public List<double> Embedding { get; set; }

        /// <summary>
        /// Tags for categorizing and filtering documents
        /// Examples: "text", "image", "upload", "generated", "website", "webpage", "product"
        /// </summary>
        public List<string> Tags { get; set; } = new List<string>();

        /// <summary>
        /// LLM-generated description for images or summary for text documents
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Processing status for automatic document processing workflow
        /// </summary>
        public ProcessingStatus Status { get; set; } = ProcessingStatus.NotProcessed;

        /// <summary>
        /// Timestamp when processing completed (description generated, embeddings created)
        /// </summary>
        public DateTime? ProcessedAt { get; set; }

        /// <summary>
        /// Error message if processing failed
        /// </summary>
        public string? ProcessingError { get; set; }

        /// <summary>
        /// Content type classification for semantic search filtering
        /// </summary>
        public ContentType Type { get; set; } = ContentType.Text;

        /// <summary>
        /// References to images extracted from this document (PDF/Office images)
        /// </summary>
        public List<string> ExtractedImages { get; set; } = new List<string>();

        /// <summary>
        /// File size in bytes
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public long FileSize { get; set; }

        /// <summary>
        /// MIME type
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string MimeType { get; set; }

        /// <summary>
        /// When the file was uploaded
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public DateTime UploadedAt { get; set; }

        public string ToDescriptiveString() => Content;
    }

    /// <summary>
    /// Processing status for automatic document processing
    /// </summary>
    public enum ProcessingStatus
    {
        NotProcessed,
        Processing,
        Completed,
        Failed
    }

    /// <summary>
    /// Content type classification for filtering and search
    /// </summary>
    public enum ContentType
    {
        Unknown,
        Image,
        Text,
        Mixed,
        Pdf,
        Office
    }
}
