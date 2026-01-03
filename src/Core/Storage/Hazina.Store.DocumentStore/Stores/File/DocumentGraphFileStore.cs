using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// File-based implementation of document graph store.
/// Stores relationships in JSON files.
/// </summary>
public class DocumentGraphFileStore : IDocumentGraphStore
{
    private readonly string _basePath;
    private readonly string _relationshipsFile;
    private List<DocumentRelationship>? _cache;
    private readonly object _lock = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public DocumentGraphFileStore(string basePath)
    {
        _basePath = basePath ?? throw new ArgumentNullException(nameof(basePath));
        Directory.CreateDirectory(_basePath);
        _relationshipsFile = Path.Combine(_basePath, "relationships.json");
    }

    public async Task AddRelationshipAsync(
        DocumentRelationship relationship,
        CancellationToken ct = default)
    {
        var relationships = await LoadRelationshipsAsync(ct);
        relationships.Add(relationship);
        await SaveRelationshipsAsync(relationships, ct);
    }

    public async Task AddRelationshipsAsync(
        IEnumerable<DocumentRelationship> relationships,
        CancellationToken ct = default)
    {
        var existing = await LoadRelationshipsAsync(ct);
        existing.AddRange(relationships);
        await SaveRelationshipsAsync(existing, ct);
    }

    public async Task<DocumentRelationship?> GetRelationshipAsync(
        string relationshipId,
        CancellationToken ct = default)
    {
        var relationships = await LoadRelationshipsAsync(ct);
        return relationships.FirstOrDefault(r => r.RelationshipId == relationshipId);
    }

    public async Task<DocumentNode> GetDocumentNodeAsync(
        string documentId,
        CancellationToken ct = default)
    {
        var relationships = await LoadRelationshipsAsync(ct);
        return new DocumentNode
        {
            DocumentId = documentId,
            OutgoingRelationships = relationships
                .Where(r => r.SourceDocumentId == documentId)
                .ToList(),
            IncomingRelationships = relationships
                .Where(r => r.TargetDocumentId == documentId)
                .ToList()
        };
    }

    public async Task<List<DocumentRelationship>> GetOutgoingRelationshipsAsync(
        string documentId,
        RelationshipType? filterType = null,
        CancellationToken ct = default)
    {
        var relationships = await LoadRelationshipsAsync(ct);
        var query = relationships.Where(r => r.SourceDocumentId == documentId);
        if (filterType.HasValue)
        {
            query = query.Where(r => r.Type == filterType.Value);
        }
        return query.ToList();
    }

    public async Task<List<DocumentRelationship>> GetIncomingRelationshipsAsync(
        string documentId,
        RelationshipType? filterType = null,
        CancellationToken ct = default)
    {
        var relationships = await LoadRelationshipsAsync(ct);
        var query = relationships.Where(r => r.TargetDocumentId == documentId);
        if (filterType.HasValue)
        {
            query = query.Where(r => r.Type == filterType.Value);
        }
        return query.ToList();
    }

    public async Task<List<DocumentRelationship>> GetRelationshipsBetweenAsync(
        string documentId1,
        string documentId2,
        CancellationToken ct = default)
    {
        var relationships = await LoadRelationshipsAsync(ct);
        return relationships.Where(r =>
            (r.SourceDocumentId == documentId1 && r.TargetDocumentId == documentId2) ||
            (r.SourceDocumentId == documentId2 && r.TargetDocumentId == documentId1))
            .ToList();
    }

    public async Task<GraphTraversalResult> TraverseAsync(
        string startDocumentId,
        int maxDepth = 2,
        List<RelationshipType>? relationshipTypes = null,
        double minConfidence = 0.0,
        CancellationToken ct = default)
    {
        var result = new GraphTraversalResult
        {
            StartDocumentId = startDocumentId
        };

        var relationships = await LoadRelationshipsAsync(ct);
        var visited = new HashSet<string> { startDocumentId };
        var queue = new Queue<(string DocId, int Depth, List<RelationshipType> Path, double Confidence)>();
        queue.Enqueue((startDocumentId, 0, new List<RelationshipType>(), 1.0));

        while (queue.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var (currentId, depth, path, pathConfidence) = queue.Dequeue();

            if (depth > 0)
            {
                result.MaxDepthReached = Math.Max(result.MaxDepthReached, depth);
            }

            if (depth >= maxDepth) continue;

            // Find connected documents
            var connected = relationships.Where(r =>
                r.SourceDocumentId == currentId || r.TargetDocumentId == currentId);

            if (relationshipTypes != null && relationshipTypes.Count > 0)
            {
                connected = connected.Where(r => relationshipTypes.Contains(r.Type));
            }

            if (minConfidence > 0)
            {
                connected = connected.Where(r => r.Confidence >= minConfidence);
            }

            foreach (var rel in connected)
            {
                var nextId = rel.SourceDocumentId == currentId
                    ? rel.TargetDocumentId
                    : rel.SourceDocumentId;

                if (visited.Contains(nextId)) continue;
                visited.Add(nextId);

                var newPath = new List<RelationshipType>(path) { rel.Type };
                var newConfidence = pathConfidence * rel.Confidence;

                result.Documents.Add(new TraversedDocument
                {
                    DocumentId = nextId,
                    Distance = depth + 1,
                    Path = newPath,
                    PathConfidence = newConfidence,
                    Relationship = rel
                });

                queue.Enqueue((nextId, depth + 1, newPath, newConfidence));
            }
        }

        // Sort by distance then confidence
        result.Documents = result.Documents
            .OrderBy(d => d.Distance)
            .ThenByDescending(d => d.PathConfidence)
            .ToList();

        return result;
    }

    public async Task<List<TraversedDocument>> FindSupportingDocumentsAsync(
        string documentId,
        int maxResults = 10,
        CancellationToken ct = default)
    {
        var result = await TraverseAsync(
            documentId,
            maxDepth: 2,
            relationshipTypes: new List<RelationshipType>
            {
                RelationshipType.Supports,
                RelationshipType.ProvidesEvidence,
                RelationshipType.Cites
            },
            ct: ct);

        return result.Documents.Take(maxResults).ToList();
    }

    public async Task<List<TraversedDocument>> FindContradictingDocumentsAsync(
        string documentId,
        int maxResults = 10,
        CancellationToken ct = default)
    {
        var result = await TraverseAsync(
            documentId,
            maxDepth: 2,
            relationshipTypes: new List<RelationshipType>
            {
                RelationshipType.Contradicts
            },
            ct: ct);

        return result.Documents.Take(maxResults).ToList();
    }

    public async Task<List<TraversedDocument>> FindCitingDocumentsAsync(
        string documentId,
        int maxResults = 10,
        CancellationToken ct = default)
    {
        var relationships = await LoadRelationshipsAsync(ct);
        return relationships
            .Where(r => r.TargetDocumentId == documentId &&
                       (r.Type == RelationshipType.Cites || r.Type == RelationshipType.CitedBy))
            .OrderByDescending(r => r.Confidence)
            .Take(maxResults)
            .Select(r => new TraversedDocument
            {
                DocumentId = r.SourceDocumentId,
                Distance = 1,
                Path = new List<RelationshipType> { r.Type },
                PathConfidence = r.Confidence,
                Relationship = r
            })
            .ToList();
    }

    public async Task UpdateRelationshipAsync(
        DocumentRelationship relationship,
        CancellationToken ct = default)
    {
        var relationships = await LoadRelationshipsAsync(ct);
        var index = relationships.FindIndex(r => r.RelationshipId == relationship.RelationshipId);
        if (index >= 0)
        {
            relationship.LastUpdated = DateTime.UtcNow;
            relationships[index] = relationship;
            await SaveRelationshipsAsync(relationships, ct);
        }
    }

    public async Task DeleteRelationshipAsync(
        string relationshipId,
        CancellationToken ct = default)
    {
        var relationships = await LoadRelationshipsAsync(ct);
        relationships.RemoveAll(r => r.RelationshipId == relationshipId);
        await SaveRelationshipsAsync(relationships, ct);
    }

    public async Task DeleteDocumentRelationshipsAsync(
        string documentId,
        CancellationToken ct = default)
    {
        var relationships = await LoadRelationshipsAsync(ct);
        relationships.RemoveAll(r =>
            r.SourceDocumentId == documentId || r.TargetDocumentId == documentId);
        await SaveRelationshipsAsync(relationships, ct);
    }

    public async Task<GraphStatistics> GetStatisticsAsync(CancellationToken ct = default)
    {
        var relationships = await LoadRelationshipsAsync(ct);
        var stats = new GraphStatistics
        {
            TotalRelationships = relationships.Count
        };

        // Count unique documents
        var documents = new HashSet<string>();
        var docRelCounts = new Dictionary<string, int>();

        foreach (var rel in relationships)
        {
            documents.Add(rel.SourceDocumentId);
            documents.Add(rel.TargetDocumentId);

            docRelCounts.TryAdd(rel.SourceDocumentId, 0);
            docRelCounts.TryAdd(rel.TargetDocumentId, 0);
            docRelCounts[rel.SourceDocumentId]++;
            docRelCounts[rel.TargetDocumentId]++;

            stats.RelationshipsByType.TryAdd(rel.Type, 0);
            stats.RelationshipsByType[rel.Type]++;
        }

        stats.TotalDocuments = documents.Count;

        if (docRelCounts.Count > 0)
        {
            var mostConnected = docRelCounts.OrderByDescending(kv => kv.Value).First();
            stats.MostConnectedDocumentId = mostConnected.Key;
            stats.MaxRelationships = mostConnected.Value;
        }

        return stats;
    }

    private async Task<List<DocumentRelationship>> LoadRelationshipsAsync(CancellationToken ct)
    {
        if (_cache != null) return _cache;

        if (!File.Exists(_relationshipsFile))
        {
            _cache = new List<DocumentRelationship>();
            return _cache;
        }

        var json = await File.ReadAllTextAsync(_relationshipsFile, ct);
        _cache = JsonSerializer.Deserialize<List<DocumentRelationship>>(json, JsonOptions)
            ?? new List<DocumentRelationship>();
        return _cache;
    }

    private async Task SaveRelationshipsAsync(
        List<DocumentRelationship> relationships,
        CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(relationships, JsonOptions);
        await File.WriteAllTextAsync(_relationshipsFile, json, ct);
        _cache = relationships;
    }
}
