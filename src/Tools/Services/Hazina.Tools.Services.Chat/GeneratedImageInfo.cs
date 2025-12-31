using Hazina.Tools.Models;
using Hazina.Tools.Models.WordPress.Blogs;
using System;
using System.Collections.Generic;

namespace Hazina.Tools.Services.Chat
{
    public class GeneratedImageInfo : Serializer<GeneratedImageInfo>
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ProjectId { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string ChatId { get; set; } = string.Empty;
        public string Prompt { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string SourceUrl { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Tags for categorizing and filtering generated images
        /// Automatically includes: "image", "generated"
        /// May also include: "logo", "banner", "illustration", etc.
        /// </summary>
        public List<string> Tags { get; set; } = new List<string> { "image", "generated" };
    }
}
