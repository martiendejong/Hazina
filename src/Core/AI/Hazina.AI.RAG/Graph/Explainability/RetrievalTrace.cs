namespace Hazina.AI.RAG.Graph.Explainability;

using Hazina.AI.RAG.Graph.Models;
using Hazina.AI.RAG.Graph.Retrieval;

/// <summary>
/// Trace object showing the reasoning path for a retrieval result.
/// Provides explainability for hybrid vector + graph retrieval.
/// </summary>
public class RetrievalTrace
{
    public string QueryText { get; set; } = string.Empty;
    public string ResultDocumentId { get; set; } = string.Empty;
    public double FinalScore { get; set; }
    public List<RetrievalStep> Steps { get; set; } = new();
    public GraphPath? GraphPath { get; set; }
    public string Explanation { get; set; } = string.Empty;
}

/// <summary>
/// Individual step in the retrieval process.
/// </summary>
public class RetrievalStep
{
    public string StepType { get; set; } = string.Empty; // "VectorSearch", "GraphTraversal", "Fusion"
    public string Description { get; set; } = string.Empty;
    public double Score { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Service for generating explainable retrieval traces.
/// </summary>
public class ExplainabilityService
{
    /// <summary>
    /// Generates a human-readable explanation for a retrieval result.
    /// </summary>
    public static string GenerateExplanation(HybridRetrievalResult result)
    {
        var explanation = new System.Text.StringBuilder();

        explanation.AppendLine($"Retrieved document with score: {result.Score:F3}");
        explanation.AppendLine($"Source: {result.Source}");

        if (result.Source == RetrievalSource.VectorSearch || result.VectorScore > 0)
        {
            explanation.AppendLine($"  - Vector similarity: {result.VectorScore:F3}");
        }

        if (result.GraphPath != null && result.GraphPath.Length > 0)
        {
            explanation.AppendLine($"  - Graph relevance:");
            explanation.AppendLine($"    Path length: {result.GraphPath.Length} hops");

            for (var i = 0; i < result.GraphPath.Entities.Count; i++)
            {
                var entity = result.GraphPath.Entities[i];
                explanation.Append($"    {i + 1}. {entity.Name} ({entity.Type})");

                if (i < result.GraphPath.Relationships.Count)
                {
                    var rel = result.GraphPath.Relationships[i];
                    explanation.Append($" --[{rel.RelationType}]--> ");
                }
                else
                {
                    explanation.AppendLine();
                }
            }

            result.GraphPath.CalculateScore();
            explanation.AppendLine($"    Path score: {result.GraphPath.Score:F3}");
        }

        return explanation.ToString();
    }

    /// <summary>
    /// Creates a complete retrieval trace with all steps.
    /// </summary>
    public static RetrievalTrace CreateTrace(
        string queryText,
        HybridRetrievalResult result,
        List<RetrievalStep> steps)
    {
        return new RetrievalTrace
        {
            QueryText = queryText,
            ResultDocumentId = result.DocumentId,
            FinalScore = result.Score,
            Steps = steps,
            GraphPath = result.GraphPath,
            Explanation = GenerateExplanation(result)
        };
    }
}
