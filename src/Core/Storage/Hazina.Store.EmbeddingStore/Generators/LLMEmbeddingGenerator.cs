namespace Hazina.Store.EmbeddingStore;

/// <summary>
/// Implementation of IEmbeddingGenerator that uses an ILLMClient to generate embeddings.
/// Wraps the LLM client to separate embedding generation from storage concerns.
/// </summary>
public class LLMEmbeddingGenerator : IEmbeddingGenerator
{
    private readonly ILLMClient _llmClient;

    /// <summary>
    /// The dimensionality of embeddings produced by this generator.
    /// </summary>
    public int Dimensions { get; }

    /// <summary>
    /// Creates a new LLMEmbeddingGenerator.
    /// </summary>
    /// <param name="llmClient">The LLM client to use for generating embeddings</param>
    /// <param name="dimensions">Expected embedding dimensions (default: 1536 for OpenAI models)</param>
    public LLMEmbeddingGenerator(ILLMClient llmClient, int dimensions = 1536)
    {
        _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));

        if (dimensions <= 0)
            throw new ArgumentException("Dimensions must be positive", nameof(dimensions));

        Dimensions = dimensions;
    }

    /// <summary>
    /// Generates an embedding vector for the given text.
    /// </summary>
    public async Task<Embedding> GenerateAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text cannot be null or whitespace", nameof(text));

        // TODO: Add cancellation token support to ILLMClient.GenerateEmbedding
        var embedding = await _llmClient.GenerateEmbedding(text);

        // Validate dimensions
        if (embedding.Count != Dimensions)
        {
            throw new InvalidOperationException(
                $"Expected embedding dimension {Dimensions}, but got {embedding.Count}");
        }

        return embedding;
    }

    /// <summary>
    /// Generates embeddings for multiple texts in a batch.
    /// Currently processes sequentially - can be optimized for parallel/batch processing.
    /// </summary>
    public async Task<List<Embedding>> GenerateBatchAsync(
        IEnumerable<string> texts,
        CancellationToken cancellationToken = default)
    {
        if (texts == null)
            throw new ArgumentNullException(nameof(texts));

        var textList = texts.ToList();
        if (textList.Count == 0)
            return new List<Embedding>();

        // TODO: Optimize with true batch API if available
        // For now, process sequentially to maintain order
        var embeddings = new List<Embedding>(textList.Count);
        foreach (var text in textList)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var embedding = await GenerateAsync(text, cancellationToken);
            embeddings.Add(embedding);
        }

        return embeddings;
    }
}
