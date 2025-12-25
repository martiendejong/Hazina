namespace Hazina.Store.EmbeddingStore;

/// <summary>
/// Interface for generating embeddings from text.
/// Separates embedding generation from storage concerns.
/// </summary>
public interface IEmbeddingGenerator
{
    /// <summary>
    /// Generates an embedding vector for the given text.
    /// </summary>
    /// <param name="text">The text to generate an embedding for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The generated embedding vector</returns>
    Task<Embedding> GenerateAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates embeddings for multiple texts in a batch.
    /// Implementations may optimize for batch processing.
    /// </summary>
    /// <param name="texts">The texts to generate embeddings for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of embeddings in the same order as input texts</returns>
    Task<List<Embedding>> GenerateBatchAsync(
        IEnumerable<string> texts,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// The dimensionality of the embeddings produced by this generator.
    /// </summary>
    int Dimensions { get; }
}
