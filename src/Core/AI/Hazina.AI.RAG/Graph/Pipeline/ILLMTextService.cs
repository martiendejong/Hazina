namespace Hazina.AI.RAG.Graph.Pipeline;

/// <summary>
/// Simplified text-in/text-out LLM service for graph construction.
/// </summary>
public interface ILLMTextService
{
    /// <summary>
    /// Gets a text response from the LLM for a given prompt.
    /// </summary>
    /// <param name="prompt">The prompt text</param>
    /// <param name="maxTokens">Maximum tokens for response</param>
    /// <param name="temperature">Temperature for sampling (0.0-2.0)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Text response from LLM</returns>
    Task<string> GetResponseAsync(
        string prompt,
        int maxTokens = 2000,
        float temperature = 0.3f,
        CancellationToken cancellationToken = default);
}
