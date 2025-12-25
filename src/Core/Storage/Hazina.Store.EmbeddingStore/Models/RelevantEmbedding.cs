public class RelevantEmbedding
{
    public double Similarity { get; set; }
    public string StoreName { get; set; } = "";
    public EmbeddingInfo? Document { get; set; } = null;
    public string? ParentDocumentKey { get; set; } = null;
    public Func<string, Task<string>>? GetText { get; set; } = null;
}
