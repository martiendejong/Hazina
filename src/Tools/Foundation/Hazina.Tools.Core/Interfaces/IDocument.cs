using System.Collections.Generic;

namespace DevGPTStore.Core.Interfaces
{
    /// <summary>
    /// Base interface for all documents in the system
    /// </summary>
    public interface IDocument
    {
        /// <summary>
        /// Unique identifier for the document
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Display name of the document
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Full text content of the document
        /// </summary>
        string Content { get; }

        /// <summary>
        /// Vector embedding for semantic search (nullable if not yet generated)
        /// </summary>
        List<double> Embedding { get; set; }

        /// <summary>
        /// Convert document to a descriptive string format for AI context
        /// </summary>
        string ToDescriptiveString();
    }
}
