namespace Hazina.API.Search.Services;

/// <summary>
/// Service for generating embeddings (stub implementation)
/// </summary>
public class EmbeddingService
{
    private readonly ILogger<EmbeddingService> _logger;

    public EmbeddingService(ILogger<EmbeddingService> logger)
    {
        _logger = logger;
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text, string model = "text-embedding-3-small")
    {
        // TODO: Replace with real IProviderOrchestrator when available
        // For now, return a stub embedding vector
        await Task.CompletedTask;

        var random = new Random(text.GetHashCode());
        var embedding = new float[1536]; // Standard OpenAI embedding size
        for (int i = 0; i < embedding.Length; i++)
        {
            embedding[i] = (float)(random.NextDouble() * 2 - 1); // Random values between -1 and 1
        }

        _logger.LogInformation("Generated stub embedding for text of length {Length}", text.Length);
        return embedding;
    }

    public async Task<List<float[]>> GenerateBatchEmbeddingsAsync(
        List<string> texts,
        string model = "text-embedding-3-small")
    {
        // TODO: Replace with real IProviderOrchestrator when available
        var embeddings = new List<float[]>();

        foreach (var text in texts)
        {
            embeddings.Add(await GenerateEmbeddingAsync(text, model));
        }

        _logger.LogInformation("Generated stub embeddings for {Count} texts", texts.Count);
        return embeddings;
    }
}
