public class ObjectRevision<T>
{
    public string? Feedback { get; set; }
    public T Content { get; set; }
    public string? AssistantFeedback { get; set; }
    public bool IsUserModification { get; set; } = false;
}
public class DocumentRevision
{
    public string? Feedback { get; set; }
    public string Content { get; set; }
    public string? AssistantFeedback { get; set; }
    public bool IsUserModification { get; set; } = false;
    public System.DateTime Timestamp { get; set; } = System.DateTime.UtcNow;
}

