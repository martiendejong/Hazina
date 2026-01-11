namespace Hazina.AI.RAG.Graph.Pipeline;

using Hazina.AI.RAG.Configuration;
using Hazina.AI.RAG.Graph.Models;
using Hazina.AI.RAG.Graph.Storage;
using Microsoft.Extensions.Logging;

/// <summary>
/// Orchestrates the complete graph construction pipeline from document to knowledge graph.
/// </summary>
public class GraphConstructionPipeline
{
    private readonly IEntityExtractor _entityExtractor;
    private readonly IRelationshipExtractor _relationshipExtractor;
    private readonly IEntityNormalizer _entityNormalizer;
    private readonly IGraphStore _graphStore;
    private readonly ILogger<GraphConstructionPipeline> _logger;
    private readonly GraphRAGConfig _config;

    public GraphConstructionPipeline(
        IEntityExtractor entityExtractor,
        IRelationshipExtractor relationshipExtractor,
        IEntityNormalizer entityNormalizer,
        IGraphStore graphStore,
        ILogger<GraphConstructionPipeline> logger,
        GraphRAGConfig config)
    {
        _entityExtractor = entityExtractor ?? throw new ArgumentNullException(nameof(entityExtractor));
        _relationshipExtractor = relationshipExtractor ?? throw new ArgumentNullException(nameof(relationshipExtractor));
        _entityNormalizer = entityNormalizer ?? throw new ArgumentNullException(nameof(entityNormalizer));
        _graphStore = graphStore ?? throw new ArgumentNullException(nameof(graphStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>
    /// Processes a document through the complete graph construction pipeline.
    /// </summary>
    /// <param name="documentId">Unique identifier for the document</param>
    /// <param name="documentText">Text content of the document</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing statistics about the graph construction</returns>
    public async Task<GraphConstructionResult> ProcessDocumentAsync(
        string documentId,
        string documentText,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(documentId))
        {
            throw new ArgumentException("Document ID cannot be empty", nameof(documentId));
        }

        if (string.IsNullOrWhiteSpace(documentText))
        {
            throw new ArgumentException("Document text cannot be empty", nameof(documentText));
        }

        var result = new GraphConstructionResult
        {
            DocumentId = documentId,
            StartTime = DateTime.UtcNow
        };

        try
        {
            _logger.LogInformation("Starting graph construction for document {DocumentId}", documentId);

            // Step 1: Extract entities
            _logger.LogDebug("Extracting entities from document {DocumentId}", documentId);
            var extractedEntities = await _entityExtractor.ExtractEntitiesAsync(
                documentText,
                documentId,
                cancellationToken);

            result.EntitiesExtracted = extractedEntities.Count;
            _logger.LogInformation("Extracted {Count} entities from document {DocumentId}",
                extractedEntities.Count, documentId);

            if (extractedEntities.Count == 0)
            {
                _logger.LogWarning("No entities extracted from document {DocumentId}", documentId);
                result.EndTime = DateTime.UtcNow;
                result.Success = true;
                return result;
            }

            // Check entity limit
            if (extractedEntities.Count > _config.MaxEntitiesPerDocument)
            {
                _logger.LogWarning("Document {DocumentId} exceeded max entities ({Count} > {Max}), truncating",
                    documentId, extractedEntities.Count, _config.MaxEntitiesPerDocument);
                extractedEntities = extractedEntities.Take(_config.MaxEntitiesPerDocument).ToList();
            }

            // Step 2: Extract relationships
            _logger.LogDebug("Extracting relationships from document {DocumentId}", documentId);
            var extractedRelationships = await _relationshipExtractor.ExtractRelationshipsAsync(
                documentText,
                extractedEntities,
                documentId,
                cancellationToken);

            result.RelationshipsExtracted = extractedRelationships.Count;
            _logger.LogInformation("Extracted {Count} relationships from document {DocumentId}",
                extractedRelationships.Count, documentId);

            // Check relationship limit
            if (extractedRelationships.Count > _config.MaxRelationshipsPerDocument)
            {
                _logger.LogWarning("Document {DocumentId} exceeded max relationships ({Count} > {Max}), truncating",
                    documentId, extractedRelationships.Count, _config.MaxRelationshipsPerDocument);
                extractedRelationships = extractedRelationships
                    .OrderByDescending(r => r.Confidence)
                    .Take(_config.MaxRelationshipsPerDocument)
                    .ToList();
            }

            // Step 3: Normalize entities (deduplicate and merge)
            _logger.LogDebug("Normalizing {Count} entities", extractedEntities.Count);
            var normalizedEntities = new List<GraphEntity>();

            foreach (var entity in extractedEntities)
            {
                var normalized = await _entityNormalizer.NormalizeEntityAsync(
                    entity,
                    _graphStore,
                    cancellationToken);

                normalizedEntities.Add(normalized);
            }

            result.EntitiesMerged = extractedEntities.Count - normalizedEntities.DistinctBy(e => e.Id).Count();
            _logger.LogInformation("Normalized entities: {Original} -> {Normalized} (merged: {Merged})",
                extractedEntities.Count, normalizedEntities.Count, result.EntitiesMerged);

            // Step 4: Persist entities to graph store
            _logger.LogDebug("Persisting {Count} entities to graph store", normalizedEntities.Count);
            var uniqueEntities = normalizedEntities.DistinctBy(e => e.Id).ToList();

            await _graphStore.AddEntitiesAsync(uniqueEntities, cancellationToken);
            result.EntitiesPersisted = uniqueEntities.Count;

            // Step 5: Update relationship entity IDs (in case of normalization)
            var entityIdMapping = normalizedEntities
                .GroupBy(e => extractedEntities.FindIndex(ex => ex.Name == e.Name))
                .ToDictionary(
                    g => extractedEntities[g.Key].Id,
                    g => g.First().Id);

            foreach (var relationship in extractedRelationships)
            {
                if (entityIdMapping.TryGetValue(relationship.SourceEntityId, out var newSourceId))
                {
                    relationship.SourceEntityId = newSourceId;
                }

                if (entityIdMapping.TryGetValue(relationship.TargetEntityId, out var newTargetId))
                {
                    relationship.TargetEntityId = newTargetId;
                }
            }

            // Step 6: Persist relationships to graph store
            _logger.LogDebug("Persisting {Count} relationships to graph store", extractedRelationships.Count);
            await _graphStore.AddRelationshipsAsync(extractedRelationships, cancellationToken);
            result.RelationshipsPersisted = extractedRelationships.Count;

            result.Success = true;
            result.EndTime = DateTime.UtcNow;

            _logger.LogInformation(
                "Graph construction completed for document {DocumentId}: " +
                "{Entities} entities ({Merged} merged), {Relationships} relationships in {Duration}ms",
                documentId,
                result.EntitiesPersisted,
                result.EntitiesMerged,
                result.RelationshipsPersisted,
                result.Duration.TotalMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
            result.EndTime = DateTime.UtcNow;

            _logger.LogError(ex, "Error during graph construction for document {DocumentId}", documentId);

            return result;
        }
    }

    /// <summary>
    /// Processes multiple documents in batch.
    /// </summary>
    /// <param name="documents">Dictionary of document ID to text content</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Results for each document</returns>
    public async Task<List<GraphConstructionResult>> ProcessDocumentsBatchAsync(
        Dictionary<string, string> documents,
        CancellationToken cancellationToken = default)
    {
        var results = new List<GraphConstructionResult>();

        foreach (var (documentId, text) in documents)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Batch processing cancelled after {Count} documents", results.Count);
                break;
            }

            var result = await ProcessDocumentAsync(documentId, text, cancellationToken);
            results.Add(result);
        }

        _logger.LogInformation("Batch processing completed: {Total} documents, {Success} successful, {Failed} failed",
            results.Count,
            results.Count(r => r.Success),
            results.Count(r => !r.Success));

        return results;
    }
}

/// <summary>
/// Result of graph construction process for a document.
/// </summary>
public class GraphConstructionResult
{
    public string DocumentId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int EntitiesExtracted { get; set; }
    public int RelationshipsExtracted { get; set; }
    public int EntitiesMerged { get; set; }
    public int EntitiesPersisted { get; set; }
    public int RelationshipsPersisted { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration => EndTime - StartTime;
}
