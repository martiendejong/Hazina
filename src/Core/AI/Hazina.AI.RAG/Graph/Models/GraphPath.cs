namespace Hazina.AI.RAG.Graph.Models;

/// <summary>
/// Represents a path through the knowledge graph.
/// </summary>
public class GraphPath
{
    public List<GraphEntity> Entities { get; set; } = new();
    public List<GraphRelationship> Relationships { get; set; } = new();
    public int Length => Relationships.Count;
    public double Score { get; set; } = 0.0;
    public GraphEntity? Source => Entities.FirstOrDefault();
    public GraphEntity? Target => Entities.LastOrDefault();
    public string? Explanation { get; set; }
    
    public void CalculateScore()
    {
        if (Entities.Count == 0 || Relationships.Count == 0) { Score = 0.0; return; }
        double avgWeight = Relationships.Average(r => r.Weight);
        double avgConfidence = Relationships.Average(r => r.Confidence);
        double avgEntityConfidence = Entities.Average(e => e.Confidence);
        double lengthPenalty = 1.0 / (1.0 + Length * 0.1);
        Score = (avgWeight * 0.3 + avgConfidence * 0.3 + avgEntityConfidence * 0.2 + lengthPenalty * 0.2);
    }
}

public class PathFindingOptions
{
    public int MaxDepth { get; set; } = 3;
    public int MaxPaths { get; set; } = 10;
    public double MinScore { get; set; } = 0.5;
    public bool AllowCycles { get; set; } = false;
    public List<string> AllowedRelationTypes { get; set; } = new();
    public List<string> AllowedEntityTypes { get; set; } = new();
    public PathStrategy Strategy { get; set; } = PathStrategy.BreadthFirst;
}

public enum PathStrategy { BreadthFirst, DepthFirst, BestFirst, AllSimplePaths }
