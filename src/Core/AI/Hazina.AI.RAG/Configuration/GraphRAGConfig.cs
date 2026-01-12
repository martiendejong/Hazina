namespace Hazina.AI.RAG.Configuration;

/// <summary>
/// Configuration for GraphRAG features.
/// </summary>
public class GraphRAGConfig
{
    public GraphStorageType StorageType { get; set; } = GraphStorageType.SQLite;
    public string SQLitePath { get; set; } = "graph.db";
    public string? Neo4jUri { get; set; }
    public string? Neo4jUsername { get; set; }
    public string? Neo4jPassword { get; set; }
    public bool AutoBuildGraph { get; set; } = false;
    public int DefaultTraversalDepth { get; set; } = 2;
    public int MaxTraversalDepth { get; set; } = 5;
    public int MaxPathsPerQuery { get; set; } = 20;
    public double MinEntityConfidence { get; set; } = 0.7;
    public double MinRelationshipConfidence { get; set; } = 0.6;
    public bool EnableEntityEmbeddings { get; set; } = true;
    public Graph.Models.GraphSchema Schema { get; set; } = Graph.Models.GraphSchema.CreatePermissive();
    public EntityNormalizationStrategy NormalizationStrategy { get; set; } = EntityNormalizationStrategy.FuzzyMatch;
    public double EntitySimilarityThreshold { get; set; } = 0.85;
    public int MaxEntitiesPerDocument { get; set; } = 100;
    public int MaxRelationshipsPerDocument { get; set; } = 200;
    public bool EnableGraphQueryCache { get; set; } = true;
    public TimeSpan GraphQueryCacheTTL { get; set; } = TimeSpan.FromHours(1);
}

public enum GraphStorageType { SQLite, Neo4j, InMemory }
public enum EntityNormalizationStrategy { ExactMatch, FuzzyMatch, EmbeddingSimilarity, LLMBased }
