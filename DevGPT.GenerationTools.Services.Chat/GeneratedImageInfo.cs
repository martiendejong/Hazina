using DevGPT.GenerationTools.Models;
using DevGPT.GenerationTools.Models.WordPress.Blogs;
using System;

namespace DevGPT.GenerationTools.Services.Chat
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
    }
}
