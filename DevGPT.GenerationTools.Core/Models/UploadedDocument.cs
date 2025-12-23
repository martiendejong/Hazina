using System;
using System.Collections.Generic;
using DevGPTStore.Core.Interfaces;
using DevGPT.GenerationTools.Models;
using DevGPT.GenerationTools.Models.WordPress.Blogs;

namespace DevGPTStore.Core.Models
{
    /// <summary>
    /// Represents a document uploaded by the user
    /// </summary>
    public class UploadedDocument : Serializer<UploadedDocument>, IDocument
    {
        public string Id { get; set; }

        public string Name { get; set; }

        /// <summary>
        /// Original filename
        /// </summary>
        public string Filename { get; set; }

        /// <summary>
        /// Path to the uploaded file
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// Text content extracted from the file (for PDFs, images, etc.)
        /// </summary>
        public string ExtractedText { get; set; }

        /// <summary>
        /// Returns extracted text as content
        /// </summary>
        public string Content => ExtractedText ?? string.Empty;

        /// <summary>
        /// When the document was uploaded
        /// </summary>
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// File size in bytes
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// MIME type of the file
        /// </summary>
        public string MimeType { get; set; }

        /// <summary>
        /// Vector embedding for semantic search
        /// </summary>
        public List<double> Embedding { get; set; }

        public string ToDescriptiveString()
        {
            return $"Uploaded Document: {Name}\nFile: {Filename}\nUploaded: {UploadedAt:yyyy-MM-dd}\n\n{Content}";
        }
    }
}
