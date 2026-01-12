using Hazina.AI.Providers.Core;
using Hazina.LLMs;
using Hazina.LongContext.Interfaces;
using Hazina.LongContext.Models;
using System.Text.Json;

namespace Hazina.LongContext.Planning;

/// <summary>
/// Recursive planner that uses LLM to decompose queries into hierarchical sub-queries.
/// Creates a tree of query nodes with controlled depth and branching factor.
/// </summary>
public class RecursiveQueryPlanner : IQueryPlanner
{
    private readonly IProviderOrchestrator _llmOrchestrator;
    private readonly IQueryPlanner _fallbackPlanner;

    public RecursiveQueryPlanner(
        IProviderOrchestrator llmOrchestrator,
        IQueryPlanner fallbackPlanner)
    {
        _llmOrchestrator = llmOrchestrator ?? throw new ArgumentNullException(nameof(llmOrchestrator));
        _fallbackPlanner = fallbackPlanner ?? throw new ArgumentNullException(nameof(fallbackPlanner));
    }

    public async Task<QueryNode> PlanAsync(
        LongContextRequest request,
        CancellationToken ct = default)
    {
        // If recursion disabled or max depth is 1, use fallback
        if (!request.EnableRecursion || request.MaxDepth <= 1)
        {
            return await _fallbackPlanner.PlanAsync(request, ct);
        }

        try
        {
            // Decompose the query into sub-questions
            var subQuestions = await DecomposeQuestionAsync(
                request.Query,
                request.MaxBranchingFactor,
                ct);

            // If decomposition failed or produced only 1 question, use fallback
            if (subQuestions == null || subQuestions.Count <= 1)
            {
                return await _fallbackPlanner.PlanAsync(request, ct);
            }

            // Build recursive query tree
            var root = new QueryNode
            {
                Type = QueryNodeType.Root,
                Prompt = request.Query,
                Depth = 0,
                Metadata = new Dictionary<string, object>
                {
                    ["originalQuery"] = request.Query,
                    ["maxDepth"] = request.MaxDepth,
                    ["strategy"] = "recursive"
                }
            };

            // Create decomposition node
            var decomposition = new QueryNode
            {
                Type = QueryNodeType.Decomposition,
                Prompt = request.Query,
                Depth = 1,
                ParentId = root.Id,
                Metadata = new Dictionary<string, object>
                {
                    ["subQuestionCount"] = subQuestions.Count
                }
            };

            // Create retrieval nodes for each sub-question
            var retrievalNodes = subQuestions.Select(subQ => new QueryNode
            {
                Type = QueryNodeType.Retrieval,
                Prompt = subQ.Question,
                Depth = 2,
                ParentId = decomposition.Id,
                Metadata = new Dictionary<string, object>
                {
                    ["maxShards"] = 15,
                    ["subQuestionRationale"] = subQ.Rationale ?? "",
                    ["tags"] = request.Tags.ToList()
                }
            }).ToList();

            // Create aggregation node to combine results
            var aggregation = new QueryNode
            {
                Type = QueryNodeType.Aggregation,
                Prompt = $"Combine answers to: {request.Query}",
                Depth = 2,
                ParentId = decomposition.Id,
                Children = retrievalNodes,
                Metadata = new Dictionary<string, object>
                {
                    ["finalSynthesis"] = true
                }
            };

            // Assemble tree
            var decompositionWithChildren = new QueryNode
            {
                Id = decomposition.Id,
                Type = decomposition.Type,
                Prompt = decomposition.Prompt,
                Depth = decomposition.Depth,
                ParentId = decomposition.ParentId,
                Metadata = decomposition.Metadata,
                Children = new[] { aggregation }
            };

            var rootWithChildren = new QueryNode
            {
                Id = root.Id,
                Type = root.Type,
                Prompt = root.Prompt,
                Depth = root.Depth,
                Metadata = root.Metadata,
                Children = new[] { decompositionWithChildren }
            };

            return rootWithChildren;
        }
        catch (Exception ex)
        {
            // Fallback to simple planner if decomposition fails
            return await _fallbackPlanner.PlanAsync(request, ct);
        }
    }

    private async Task<List<SubQuestion>?> DecomposeQuestionAsync(
        string question,
        int maxSubQuestions,
        CancellationToken ct)
    {
        var prompt = BuildDecompositionPrompt(question, maxSubQuestions);

        var messages = new List<HazinaChatMessage>
        {
            new()
            {
                Role = HazinaMessageRole.System,
                Text = "You are an expert at breaking down complex questions into simpler sub-questions. " +
                       "Respond ONLY with valid JSON. Do not include any markdown formatting or code blocks."
            },
            new()
            {
                Role = HazinaMessageRole.User,
                Text = prompt
            }
        };

        var response = await _llmOrchestrator.GetResponse(
            messages,
            HazinaChatResponseFormat.Text,
            null,
            null,
            ct);

        // Parse JSON response
        try
        {
            var result = response.Result.Trim();

            // Remove markdown code blocks if present
            if (result.StartsWith("```json"))
            {
                result = result.Substring(7); // Remove ```json
                if (result.EndsWith("```"))
                {
                    result = result.Substring(0, result.Length - 3); // Remove ```
                }
                result = result.Trim();
            }
            else if (result.StartsWith("```"))
            {
                result = result.Substring(3); // Remove ```
                if (result.EndsWith("```"))
                {
                    result = result.Substring(0, result.Length - 3); // Remove ```
                }
                result = result.Trim();
            }

            var decomposition = JsonSerializer.Deserialize<QuestionDecomposition>(
                result,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return decomposition?.SubQuestions;
        }
        catch (JsonException)
        {
            // If JSON parsing fails, return null to trigger fallback
            return null;
        }
    }

    private static string BuildDecompositionPrompt(string question, int maxSubQuestions)
    {
        return $@"Break down the following question into {Math.Min(maxSubQuestions, 5)} independent sub-questions.

Original Question:
{question}

Requirements:
1. Each sub-question should be independently answerable
2. Sub-questions should NOT overlap or be redundant
3. Together, the sub-questions should cover all aspects of the original question
4. Keep sub-questions clear and specific
5. If the question is already simple and cannot be meaningfully broken down, return only 1 sub-question (the original)

Respond with ONLY this exact JSON format (no markdown, no code blocks):
{{
  ""canDecompose"": true,
  ""reasoning"": ""Brief explanation of decomposition strategy"",
  ""subQuestions"": [
    {{
      ""question"": ""First sub-question"",
      ""rationale"": ""Why this sub-question is needed""
    }},
    {{
      ""question"": ""Second sub-question"",
      ""rationale"": ""Why this sub-question is needed""
    }}
  ]
}}

If the question cannot be meaningfully decomposed:
{{
  ""canDecompose"": false,
  ""reasoning"": ""This question is already atomic and cannot be broken down"",
  ""subQuestions"": [
    {{
      ""question"": ""{question}"",
      ""rationale"": ""Original question as-is""
    }}
  ]
}}";
    }

    private record QuestionDecomposition(
        bool CanDecompose,
        string Reasoning,
        List<SubQuestion> SubQuestions
    );
}

/// <summary>
/// Represents a sub-question generated by decomposition
/// </summary>
public record SubQuestion(
    string Question,
    string? Rationale
);
