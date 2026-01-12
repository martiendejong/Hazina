namespace Hazina.AI.RAG.Graph.Pipeline;

using Hazina.AI.RAG.Graph.Models;
using Hazina.AI.RAG.Graph.Storage;

/// <summary>
/// Normalizes and deduplicates entities in the knowledge graph.
/// </summary>
public interface IEntityNormalizer
{
    /// <summary>
    /// Normalizes an entity by checking for duplicates and merging if necessary.
    /// </summary>
    /// <param name="entity">The entity to normalize</param>
    /// <param name="graphStore">The graph store to check for existing entities</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Normalized entity (may be merged with existing entity)</returns>
    Task<GraphEntity> NormalizeEntityAsync(
        GraphEntity entity,
        IGraphStore graphStore,
        CancellationToken cancellationToken = default);
}
