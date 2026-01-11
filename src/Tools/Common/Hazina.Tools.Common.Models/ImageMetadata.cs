using System.Collections.Generic;

namespace HazinaStore.Models
{
    /// <summary>
    /// Metadata for images with LLM-generated descriptions and embeddings for semantic search
    /// </summary>
    public class ImageMetadata
    {
        /// <summary>
        /// Image file path (relative to project uploads folder)
        /// </summary>
        public string Filename { get; set; }

        /// <summary>
        /// LLM-generated natural language description of the image
        /// Example: "A black dog sitting on grass in a park, medium size, friendly appearance"
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Objects detected in the image (for filtering and search)
        /// Example: ["dog", "park", "grass", "tree"]
        /// </summary>
        public List<string> DetectedObjects { get; set; } = new List<string>();

        /// <summary>
        /// Dominant colors detected in the image
        /// Example: ["black", "green", "brown", "blue"]
        /// </summary>
        public List<string> DetectedColors { get; set; } = new List<string>();

        /// <summary>
        /// Scene classification (outdoor, indoor, nature, urban, etc.)
        /// Example: ["outdoor", "daytime", "park"]
        /// </summary>
        public List<string> DetectedScenes { get; set; } = new List<string>();

        /// <summary>
        /// Vector embedding for semantic similarity search
        /// </summary>
        public List<double> Embedding { get; set; }

        /// <summary>
        /// Source document if this image was extracted from PDF/DOCX
        /// Example: "document.pdf"
        /// </summary>
        public string? SourceDocument { get; set; }

        /// <summary>
        /// Page number if extracted from PDF
        /// </summary>
        public int? PageNumber { get; set; }
    }
}
