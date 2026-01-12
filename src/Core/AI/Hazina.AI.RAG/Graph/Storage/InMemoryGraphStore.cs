namespace Hazina.AI.RAG.Graph.Storage;

using System.Collections.Concurrent;
using Hazina.AI.RAG.Graph.Models;

/// <summary>
/// In-memory implementation of graph storage for testing and development.
/// NOT recommended for production use - data is lost when application restarts.
/// </summary>
public class InMemoryGraphStore : IGraphStore
{
    private readonly ConcurrentDictionary<string, GraphEntity> _entities = new();
    private readonly ConcurrentDictionary<string, GraphRelationship> _relationships = new();
    private readonly ConcurrentDictionary<string, List<string>> _entityRelationships = new(); // EntityId -> RelationshipIds

    public Task AddEntitiesAsync(List<GraphEntity> entities, CancellationToken cancellationToken = default)
    {
        foreach (var entity in entities)
        {
            _entities.AddOrUpdate(entity.Id, entity, (key, existing) =>
            {
                // Merge properties if entity already exists
                foreach (var prop in entity.Properties)
                {
                    existing.Properties[prop.Key] = prop.Value;
                }
                existing.LastUpdated = DateTime.UtcNow;
                return existing;
            });
        }

        return Task.CompletedTask;
    }

    public Task<GraphEntity?> GetEntityByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        _entities.TryGetValue(id, out var entity);
        return Task.FromResult(entity);
    }

    public Task<List<GraphEntity>> SearchEntitiesByNameAsync(
        string name,
        string? type = null,
        CancellationToken cancellationToken = default)
    {
        var results = _entities.Values
            .Where(e => e.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
            .Where(e => type == null || e.Type.Equals(type, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return Task.FromResult(results);
    }

    public Task<List<GraphEntity>> SearchEntitiesByEmbeddingAsync(
        float[] embedding,
        int topK = 10,
        string? typeFilter = null,
        CancellationToken cancellationToken = default)
    {
        var entitiesWithEmbeddings = _entities.Values
            .Where(e => e.Embedding != null && e.Embedding.Length > 0)
            .Where(e => typeFilter == null || e.Type.Equals(typeFilter, StringComparison.OrdinalIgnoreCase))
            .Select(e => new
            {
                Entity = e,
                Similarity = CalculateCosineSimilarity(embedding, e.Embedding!)
            })
            .OrderByDescending(x => x.Similarity)
            .Take(topK)
            .Select(x => x.Entity)
            .ToList();

        return Task.FromResult(entitiesWithEmbeddings);
    }

    public Task<List<GraphEntity>> GetEntitiesByDocumentAsync(
        string documentId,
        CancellationToken cancellationToken = default)
    {
        // Find all entities that have this documentId in their SourceDocuments
        var results = _entities.Values
            .Where(e => e.SourceDocuments.Contains(documentId))
            .OrderByDescending(e => e.MentionCount)
            .ThenByDescending(e => e.Confidence)
            .ToList();

        return Task.FromResult(results);
    }

    public Task AddRelationshipsAsync(List<GraphRelationship> relationships, CancellationToken cancellationToken = default)
    {
        foreach (var relationship in relationships)
        {
            _relationships.TryAdd(relationship.Id, relationship);

            // Track relationships by source entity
            _entityRelationships.AddOrUpdate(
                relationship.SourceEntityId,
                new List<string> { relationship.Id },
                (key, existing) =>
                {
                    if (!existing.Contains(relationship.Id))
                    {
                        existing.Add(relationship.Id);
                    }
                    return existing;
                });

            // Track relationships by target entity (for bidirectional traversal)
            _entityRelationships.AddOrUpdate(
                relationship.TargetEntityId,
                new List<string> { relationship.Id },
                (key, existing) =>
                {
                    if (!existing.Contains(relationship.Id))
                    {
                        existing.Add(relationship.Id);
                    }
                    return existing;
                });
        }

        return Task.CompletedTask;
    }

    public Task<List<GraphRelationship>> GetRelationshipsAsync(
        string entityId,
        CancellationToken cancellationToken = default)
    {
        if (!_entityRelationships.TryGetValue(entityId, out var relationshipIds))
        {
            return Task.FromResult(new List<GraphRelationship>());
        }

        var relationships = relationshipIds
            .Select(id => _relationships.TryGetValue(id, out var rel) ? rel : null)
            .Where(rel => rel != null)
            .Cast<GraphRelationship>()
            .ToList();

        return Task.FromResult(relationships);
    }

    public async Task<List<GraphEntity>> GetNeighborsAsync(
        string entityId,
        int depth = 1,
        CancellationToken cancellationToken = default)
    {
        var visited = new HashSet<string>();
        var neighbors = new List<GraphEntity>();

        await GetNeighborsRecursiveAsync(entityId, depth, visited, neighbors, cancellationToken);

        return neighbors;
    }

    private async Task GetNeighborsRecursiveAsync(
        string entityId,
        int depth,
        HashSet<string> visited,
        List<GraphEntity> neighbors,
        CancellationToken cancellationToken)
    {
        if (depth <= 0 || visited.Contains(entityId))
        {
            return;
        }

        visited.Add(entityId);

        var relationships = await GetRelationshipsAsync(entityId, cancellationToken);

        foreach (var rel in relationships)
        {
            var neighborId = rel.SourceEntityId == entityId ? rel.TargetEntityId : rel.SourceEntityId;

            if (!visited.Contains(neighborId))
            {
                var neighbor = await GetEntityByIdAsync(neighborId, cancellationToken);
                if (neighbor != null)
                {
                    neighbors.Add(neighbor);

                    if (depth > 1)
                    {
                        await GetNeighborsRecursiveAsync(neighborId, depth - 1, visited, neighbors, cancellationToken);
                    }
                }
            }
        }
    }

    public Task<List<GraphPath>> FindPathsAsync(
        string sourceId,
        string targetId,
        PathFindingOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new PathFindingOptions();
        var paths = new List<GraphPath>();
        var visited = new HashSet<string>();
        var currentPath = new List<GraphEntity>();
        var currentRelationships = new List<GraphRelationship>();

        FindPathsRecursive(sourceId, targetId, options, visited, currentPath, currentRelationships, paths);

        // Score and sort paths
        foreach (var path in paths)
        {
            path.CalculateScore();
        }

        return Task.FromResult(paths
            .Where(p => p.Score >= options.MinScore)
            .OrderByDescending(p => p.Score)
            .Take(options.MaxPaths)
            .ToList());
    }

    private void FindPathsRecursive(
        string currentId,
        string targetId,
        PathFindingOptions options,
        HashSet<string> visited,
        List<GraphEntity> currentPath,
        List<GraphRelationship> currentRelationships,
        List<GraphPath> foundPaths)
    {
        if (currentPath.Count >= options.MaxDepth)
        {
            return;
        }

        if (!_entities.TryGetValue(currentId, out var currentEntity))
        {
            return;
        }

        currentPath.Add(currentEntity);
        visited.Add(currentId);

        if (currentId == targetId && currentPath.Count > 1)
        {
            // Found a path
            foundPaths.Add(new GraphPath
            {
                Entities = new List<GraphEntity>(currentPath),
                Relationships = new List<GraphRelationship>(currentRelationships)
            });
        }
        else if (foundPaths.Count < options.MaxPaths)
        {
            // Continue searching
            if (_entityRelationships.TryGetValue(currentId, out var relationshipIds))
            {
                foreach (var relId in relationshipIds)
                {
                    if (_relationships.TryGetValue(relId, out var relationship))
                    {
                        var nextId = relationship.SourceEntityId == currentId
                            ? relationship.TargetEntityId
                            : relationship.SourceEntityId;

                        if (!visited.Contains(nextId) || options.AllowCycles)
                        {
                            currentRelationships.Add(relationship);
                            FindPathsRecursive(nextId, targetId, options, visited, currentPath, currentRelationships, foundPaths);
                            currentRelationships.RemoveAt(currentRelationships.Count - 1);
                        }
                    }
                }
            }
        }

        currentPath.RemoveAt(currentPath.Count - 1);
        visited.Remove(currentId);
    }

    public async Task<List<GraphEntity>> SearchByLinkageAsync(
        string entityId,
        string? relationTypeFilter = null,
        int depth = 2,
        CancellationToken cancellationToken = default)
    {
        var visited = new HashSet<string>();
        var results = new List<GraphEntity>();

        await SearchByLinkageRecursiveAsync(entityId, relationTypeFilter, depth, visited, results, cancellationToken);

        return results;
    }

    private async Task SearchByLinkageRecursiveAsync(
        string entityId,
        string? relationTypeFilter,
        int depth,
        HashSet<string> visited,
        List<GraphEntity> results,
        CancellationToken cancellationToken)
    {
        if (depth <= 0 || visited.Contains(entityId))
        {
            return;
        }

        visited.Add(entityId);

        var relationships = await GetRelationshipsAsync(entityId, cancellationToken);
        var filteredRelationships = relationTypeFilter == null
            ? relationships
            : relationships.Where(r => r.RelationType.Equals(relationTypeFilter, StringComparison.OrdinalIgnoreCase)).ToList();

        foreach (var rel in filteredRelationships)
        {
            var neighborId = rel.SourceEntityId == entityId ? rel.TargetEntityId : rel.SourceEntityId;

            if (!visited.Contains(neighborId))
            {
                var neighbor = await GetEntityByIdAsync(neighborId, cancellationToken);
                if (neighbor != null)
                {
                    results.Add(neighbor);

                    if (depth > 1)
                    {
                        await SearchByLinkageRecursiveAsync(neighborId, relationTypeFilter, depth - 1, visited, results, cancellationToken);
                    }
                }
            }
        }
    }

    public Task UpdateEntityAsync(GraphEntity entity, CancellationToken cancellationToken = default)
    {
        if (_entities.ContainsKey(entity.Id))
        {
            entity.LastUpdated = DateTime.UtcNow;
            _entities[entity.Id] = entity;
        }

        return Task.CompletedTask;
    }

    public Task DeleteEntityAsync(string entityId, CancellationToken cancellationToken = default)
    {
        _entities.TryRemove(entityId, out _);

        // Remove relationships involving this entity
        if (_entityRelationships.TryGetValue(entityId, out var relationshipIds))
        {
            foreach (var relId in relationshipIds)
            {
                _relationships.TryRemove(relId, out _);
            }

            _entityRelationships.TryRemove(entityId, out _);
        }

        return Task.CompletedTask;
    }

    public Task<GraphStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var stats = new GraphStatistics
        {
            EntityCount = _entities.Count,
            RelationshipCount = _relationships.Count,
            EntityTypeDistribution = _entities.Values
                .GroupBy(e => e.Type)
                .ToDictionary(g => g.Key, g => g.Count()),
            RelationshipTypeDistribution = _relationships.Values
                .GroupBy(r => r.RelationType)
                .ToDictionary(g => g.Key, g => g.Count())
        };

        return Task.FromResult(stats);
    }

    private double CalculateCosineSimilarity(float[] vec1, float[] vec2)
    {
        if (vec1.Length != vec2.Length)
        {
            return 0.0;
        }

        double dotProduct = 0.0;
        double magnitude1 = 0.0;
        double magnitude2 = 0.0;

        for (var i = 0; i < vec1.Length; i++)
        {
            dotProduct += vec1[i] * vec2[i];
            magnitude1 += vec1[i] * vec1[i];
            magnitude2 += vec2[i] * vec2[i];
        }

        if (magnitude1 == 0.0 || magnitude2 == 0.0)
        {
            return 0.0;
        }

        return dotProduct / (Math.Sqrt(magnitude1) * Math.Sqrt(magnitude2));
    }
}
