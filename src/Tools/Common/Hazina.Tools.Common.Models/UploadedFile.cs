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
        /// LLM-generated description of the document (for document processing)
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string Description { get; set; }

        /// <summary>
        /// Processing status for automatic document processing
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public ProcessingStatus Status { get; set; }

        /// <summary>
        /// Content type classification
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public ContentType Type { get; set; }

        /// <summary>
        /// List of extracted images (for PDFs/Office docs)
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public List<string> ExtractedImages { get; set; } = new List<string>();

        /// <summary>
        /// When processing was completed (null if not completed)
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public DateTime? ProcessedAt { get; set; }

        /// <summary>
        /// Error message if processing failed
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string ProcessingError { get; set; }

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
}
