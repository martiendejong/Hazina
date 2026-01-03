using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Store for document relationships and graph queries.
/// </summary>
public interface IDocumentGraphStore
{
    /// <summary>
    /// Add a relationship between two documents.
    /// </summary>
    Task AddRelationshipAsync(
        DocumentRelationship relationship,
        CancellationToken ct = default);

    /// <summary>
    /// Add multiple relationships.
    /// </summary>
    Task AddRelationshipsAsync(
        IEnumerable<DocumentRelationship> relationships,
        CancellationToken ct = default);

    /// <summary>
    /// Get a specific relationship by ID.
    /// </summary>
    Task<DocumentRelationship?> GetRelationshipAsync(
        string relationshipId,
        CancellationToken ct = default);

    /// <summary>
    /// Get all relationships for a document.
    /// </summary>
    Task<DocumentNode> GetDocumentNodeAsync(
        string documentId,
        CancellationToken ct = default);

    /// <summary>
    /// Get outgoing relationships from a document.
    /// </summary>
    Task<List<DocumentRelationship>> GetOutgoingRelationshipsAsync(
        string documentId,
        RelationshipType? filterType = null,
        CancellationToken ct = default);

    /// <summary>
    /// Get incoming relationships to a document.
    /// </summary>
    Task<List<DocumentRelationship>> GetIncomingRelationshipsAsync(
        string documentId,
        RelationshipType? filterType = null,
        CancellationToken ct = default);

    /// <summary>
    /// Find relationships between two specific documents.
    /// </summary>
    Task<List<DocumentRelationship>> GetRelationshipsBetweenAsync(
        string documentId1,
        string documentId2,
        CancellationToken ct = default);

    /// <summary>
    /// Traverse the graph from a starting document.
    /// </summary>
    /// <param name="startDocumentId">Starting document</param>
    /// <param name="maxDepth">Maximum hops from start</param>
    /// <param name="relationshipTypes">Filter by relationship types (null = all)</param>
    /// <param name="minConfidence">Minimum confidence threshold</param>
    /// <param name="ct">Cancellation token</param>
    Task<GraphTraversalResult> TraverseAsync(
        string startDocumentId,
        int maxDepth = 2,
        List<RelationshipType>? relationshipTypes = null,
        double minConfidence = 0.0,
        CancellationToken ct = default);

    /// <summary>
    /// Find supporting documents for a given document.
    /// </summary>
    Task<List<TraversedDocument>> FindSupportingDocumentsAsync(
        string documentId,
        int maxResults = 10,
        CancellationToken ct = default);

    /// <summary>
    /// Find contradicting documents for a given document.
    /// </summary>
    Task<List<TraversedDocument>> FindContradictingDocumentsAsync(
        string documentId,
        int maxResults = 10,
        CancellationToken ct = default);

    /// <summary>
    /// Find documents that cite the given document.
    /// </summary>
    Task<List<TraversedDocument>> FindCitingDocumentsAsync(
        string documentId,
        int maxResults = 10,
        CancellationToken ct = default);

    /// <summary>
    /// Update an existing relationship.
    /// </summary>
    Task UpdateRelationshipAsync(
        DocumentRelationship relationship,
        CancellationToken ct = default);

    /// <summary>
    /// Delete a relationship.
    /// </summary>
    Task DeleteRelationshipAsync(
        string relationshipId,
        CancellationToken ct = default);

    /// <summary>
    /// Delete all relationships for a document.
    /// </summary>
    Task DeleteDocumentRelationshipsAsync(
        string documentId,
        CancellationToken ct = default);

    /// <summary>
    /// Get graph statistics.
    /// </summary>
    Task<GraphStatistics> GetStatisticsAsync(CancellationToken ct = default);
}

/// <summary>
/// Statistics about the document graph.
/// </summary>
public class GraphStatistics
{
    /// <summary>
    /// Total number of documents with relationships.
    /// </summary>
    public int TotalDocuments { get; set; }

    /// <summary>
    /// Total number of relationships.
    /// </summary>
    public int TotalRelationships { get; set; }

    /// <summary>
    /// Relationships by type.
    /// </summary>
    public Dictionary<RelationshipType, int> RelationshipsByType { get; set; } = new();

    /// <summary>
    /// Average relationships per document.
    /// </summary>
    public double AverageRelationshipsPerDocument =>
        TotalDocuments > 0 ? (double)TotalRelationships / TotalDocuments : 0;

    /// <summary>
    /// Document with most relationships.
    /// </summary>
    public string? MostConnectedDocumentId { get; set; }

    /// <summary>
    /// Maximum relationships on any document.
    /// </summary>
    public int MaxRelationships { get; set; }
}
