using System;
using System.Collections.Generic;
using DevGPT.GenerationTools.Models;
using DevGPT.GenerationTools.Models.WordPress.Blogs;

public class ChatMetadata : Serializer<ChatMetadata>
{
    public string Id { get; set; }
    public string Name { get; set; }
    public DateTime Modified { get; set; }
    public DateTime Created { get; set; } = DateTime.UtcNow;
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    public bool IsPinned { get; set; }
    public string ProjectId { get; set; }
    public string LastMessagePreview { get; set; }
}

public class ChatConversation : Serializer<ChatConversation>
{
    // Convenience properties for frontend mappings
    public string Id => MetaData?.Id;
    public string ProjectId => MetaData?.ProjectId;
    public ChatMetadata MetaData { get; set; }
    public List<ConversationMessage> ChatMessages { get; set; }
    public TokenUsageInfo TokenUsage { get; set; }
}
