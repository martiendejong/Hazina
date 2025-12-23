using System;
using System.Collections.Generic;
using DevGPT.GenerationTools.Models;
using DevGPT.GenerationTools.Models.WordPress.Blogs;

namespace DevGPTStore.Core.Models
{
    /// <summary>
    /// Metadata describing a project
    /// </summary>
    public class ProjectMetadata : Serializer<ProjectMetadata>
    {
        /// <summary>
        /// Unique identifier (same as folder name)
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Display name of the project
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Project description
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// When the project was created
        /// </summary>
        public DateTime Created { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Whether the project is archived
        /// </summary>
        public bool Archived { get; set; } = false;

        /// <summary>
        /// Current status of the project
        /// </summary>
        public string Status { get; set; } = "INITIAL";

        /// <summary>
        /// Type of project (customer, chat, global, etc.)
        /// </summary>
        public string ProjectType { get; set; } = "customer";

        /// <summary>
        /// Whether the project is pinned
        /// </summary>
        public bool IsPinned { get; set; } = false;

        /// <summary>
        /// Custom metadata fields
        /// </summary>
        public Dictionary<string, string> CustomFields { get; set; } = new Dictionary<string, string>();

        // Business-specific fields
        public string Website { get; set; }
        public string Industry { get; set; }
        public string ContactName { get; set; }
        public string ContactEmail { get; set; }
        public string ContactPhone { get; set; }
    }
}
