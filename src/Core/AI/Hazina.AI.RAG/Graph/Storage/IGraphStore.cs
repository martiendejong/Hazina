namespace Hazina.AI.RAG.Graph.Storage;

using Hazina.AI.RAG.Graph.Models;

/// <summary>
/// Interface for graph storage and query operations.
/// Implementations can use SQLite, Neo4j, or in-memory storage.
/// </summary>
public interface IGraphStore
{
    // Entity Operations
    /// <summary>
    /// Adds multiple entities to the graph store.
    /// </summary>
    Task AddEntitiesAsync(List<GraphEntity> entities, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves an entity by its ID.
    /// </summary>
    Task<GraphEntity?> GetEntityByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for entities by name and optional type.
    /// </summary>
    Task<List<GraphEntity>> SearchEntitiesByNameAsync(
        string name,
        string? type = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for entities similar to the given embedding.
    /// </summary>
    Task<List<GraphEntity>> SearchEntitiesByEmbeddingAsync(
        float[] embedding,
        int topK = 10,
        string? typeFilter = null,
        CancellationToken cancellationToken = default);

    // Relationship Operations
    /// <summary>
    /// Adds multiple relationships to the graph store.
    /// </summary>
    Task AddRelationshipsAsync(List<GraphRelationship> relationships, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all relationships for a given entity.
    /// </summary>
    Task<List<GraphRelationship>> GetRelationshipsAsync(
        string entityId,
        CancellationToken cancellationToken = default);

    // Graph Traversal Operations
    /// <summary>
    /// Gets neighboring entities at a specified depth.
    /// </summary>
    Task<List<GraphEntity>> GetNeighborsAsync(
        string entityId,
        int depth = 1,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds paths between two entities.
    /// </summary>
    Task<List<GraphPath>> FindPathsAsync(
        string sourceId,
        string targetId,
        PathFindingOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches entities connected to the given entity by a specific predicate.
    /// </summary>
    Task<List<GraphEntity>> SearchByLinkageAsync(
        string entityId,
        string? relationTypeFilter = null,
        int depth = 2,
        CancellationToken cancellationToken = default);

    // Utility Operations
    /// <summary>
    /// Updates an existing entity.
    /// </summary>
    Task UpdateEntityAsync(GraphEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an entity and its relationships.
    /// </summary>
    Task DeleteEntityAsync(string entityId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets statistics about the graph (entity count, relationship count, etc.).
    /// </summary>
    Task<GraphStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Statistics about the knowledge graph.
/// </summary>
public class GraphStatistics
{
    public int EntityCount { get; set; }
    public int RelationshipCount { get; set; }
    public Dictionary<string, int> EntityTypeDistribution { get; set; } = new();
    public Dictionary<string, int> RelationshipTypeDistribution { get; set; } = new();
}
