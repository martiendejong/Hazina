using System;
using System.Collections.Generic;

/// <summary>
/// Represents a relationship between two documents.
/// </summary>
public class DocumentRelationship
{
    /// <summary>
    /// Unique identifier for this relationship.
    /// </summary>
    public string RelationshipId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Source document ID.
    /// </summary>
    public string SourceDocumentId { get; set; } = "";

    /// <summary>
    /// Target document ID.
    /// </summary>
    public string TargetDocumentId { get; set; } = "";

    /// <summary>
    /// Type of relationship.
    /// </summary>
    public RelationshipType Type { get; set; } = RelationshipType.Related;

    /// <summary>
    /// Confidence score for this relationship (0.0 to 1.0).
    /// Higher values indicate more certain relationships.
    /// </summary>
    public double Confidence { get; set; } = 0.5;

    /// <summary>
    /// How this relationship was detected.
    /// </summary>
    public RelationshipSource Source { get; set; } = RelationshipSource.Manual;

    /// <summary>
    /// Optional description of the relationship.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// When this relationship was created.
    /// </summary>
    public DateTime Created { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this relationship was last updated.
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Additional metadata about the relationship.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>
    /// Whether this is a bidirectional relationship.
    /// </summary>
    public bool IsBidirectional => Type == RelationshipType.Related ||
                                    Type == RelationshipType.SameAuthor ||
                                    Type == RelationshipType.SameTopic;
}

/// <summary>
/// Types of document relationships.
/// </summary>
public enum RelationshipType
{
    /// <summary>
    /// Generic relationship - documents are related.
    /// </summary>
    Related,

    /// <summary>
    /// Source supports the target's claims.
    /// </summary>
    Supports,

    /// <summary>
    /// Source contradicts the target's claims.
    /// </summary>
    Contradicts,

    /// <summary>
    /// Source cites or references the target.
    /// </summary>
    Cites,

    /// <summary>
    /// Source is cited by the target.
    /// </summary>
    CitedBy,

    /// <summary>
    /// Source summarizes the target.
    /// </summary>
    Summarizes,

    /// <summary>
    /// Source expands on the target.
    /// </summary>
    ExpandsOn,

    /// <summary>
    /// Source is a newer version of the target.
    /// </summary>
    UpdatesVersion,

    /// <summary>
    /// Source and target are by the same author.
    /// </summary>
    SameAuthor,

    /// <summary>
    /// Source and target cover the same topic.
    /// </summary>
    SameTopic,

    /// <summary>
    /// Source is a response to the target.
    /// </summary>
    RespondsTo,

    /// <summary>
    /// Source provides evidence for the target.
    /// </summary>
    ProvidesEvidence,

    /// <summary>
    /// Source is derived from the target.
    /// </summary>
    DerivedFrom
}

/// <summary>
/// How the relationship was detected.
/// </summary>
public enum RelationshipSource
{
    /// <summary>
    /// Manually specified by user.
    /// </summary>
    Manual,

    /// <summary>
    /// Detected by LLM analysis.
    /// </summary>
    LLMDetected,

    /// <summary>
    /// Detected by citation parsing.
    /// </summary>
    CitationParsing,

    /// <summary>
    /// Detected by semantic similarity.
    /// </summary>
    SemanticSimilarity,

    /// <summary>
    /// Detected by content overlap.
    /// </summary>
    ContentOverlap,

    /// <summary>
    /// Inferred from metadata (e.g., same author).
    /// </summary>
    MetadataInference
}

/// <summary>
/// A node in the document graph.
/// </summary>
public class DocumentNode
{
    /// <summary>
    /// Document identifier.
    /// </summary>
    public string DocumentId { get; set; } = "";

    /// <summary>
    /// Document title or name.
    /// </summary>
    public string Title { get; set; } = "";

    /// <summary>
    /// Outgoing relationships from this document.
    /// </summary>
    public List<DocumentRelationship> OutgoingRelationships { get; set; } = new();

    /// <summary>
    /// Incoming relationships to this document.
    /// </summary>
    public List<DocumentRelationship> IncomingRelationships { get; set; } = new();

    /// <summary>
    /// All related document IDs (both directions).
    /// </summary>
    public HashSet<string> RelatedDocumentIds
    {
        get
        {
            var ids = new HashSet<string>();
            foreach (var rel in OutgoingRelationships)
                ids.Add(rel.TargetDocumentId);
            foreach (var rel in IncomingRelationships)
                ids.Add(rel.SourceDocumentId);
            return ids;
        }
    }

    /// <summary>
    /// Count of all relationships.
    /// </summary>
    public int RelationshipCount => OutgoingRelationships.Count + IncomingRelationships.Count;
}

/// <summary>
/// Result of traversing the document graph.
/// </summary>
public class GraphTraversalResult
{
    /// <summary>
    /// Starting document ID.
    /// </summary>
    public string StartDocumentId { get; set; } = "";

    /// <summary>
    /// Documents found in traversal, ordered by relevance/distance.
    /// </summary>
    public List<TraversedDocument> Documents { get; set; } = new();

    /// <summary>
    /// Total documents found.
    /// </summary>
    public int TotalFound => Documents.Count;

    /// <summary>
    /// Maximum depth reached in traversal.
    /// </summary>
    public int MaxDepthReached { get; set; }
}

/// <summary>
/// A document found during graph traversal.
/// </summary>
public class TraversedDocument
{
    /// <summary>
    /// Document identifier.
    /// </summary>
    public string DocumentId { get; set; } = "";

    /// <summary>
    /// Distance from start document (number of hops).
    /// </summary>
    public int Distance { get; set; }

    /// <summary>
    /// Path of relationship types from start to this document.
    /// </summary>
    public List<RelationshipType> Path { get; set; } = new();

    /// <summary>
    /// Combined confidence along the path.
    /// </summary>
    public double PathConfidence { get; set; }

    /// <summary>
    /// The relationship that led to this document.
    /// </summary>
    public DocumentRelationship? Relationship { get; set; }
}
