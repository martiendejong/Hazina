using Hazina.AI.ContextEngineering.Models;

namespace Hazina.AI.ContextEngineering.Interfaces;

/// <summary>
/// Interface for context retrievers that fetch relevant information
/// from various sources (embeddings, facts, metadata, direct lookup)
/// </summary>
public interface IContextRetriever
{
    /// <summary>
    /// Name of the retriever (e.g., "semantic", "facts", "metadata", "lookup")
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Retrieve relevant results for the given query
    /// </summary>
    /// <param name="query">Query parameters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of retrieval results, ordered by relevance</returns>
    Task<List<RetrievalResult>> RetrieveAsync(
        RetrievalQuery query,
        CancellationToken cancellationToken = default);
}
