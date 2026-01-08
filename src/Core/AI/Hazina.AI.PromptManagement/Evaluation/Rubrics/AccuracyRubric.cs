using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Hazina.LLMs;

namespace Hazina.AI.PromptManagement.Evaluation.Rubrics;

/// <summary>
/// LLM-as-judge rubric for evaluating response accuracy
/// </summary>
public class AccuracyRubric : IQualityRubric
{
    private readonly ILLMClient _llmClient;

    public string Name => "Accuracy";
    public string Description => "Evaluates factual accuracy and correctness of the response";

    public AccuracyRubric(ILLMClient llmClient)
    {
        _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
    }

    public async Task<RubricScore> EvaluateAsync(
        EvaluationContext context,
        CancellationToken cancellationToken = default)
    {
        var prompt = BuildEvaluationPrompt(context);

        var messages = new List<HazinaChatMessage>
        {
            new HazinaChatMessage(HazinaMessageRole.User, prompt)
        };

        var response = await _llmClient.GetResponse(messages, HazinaChatResponseFormat.Text, null, null, cancellationToken);

        return ParseEvaluationResponse(response.Result);
    }

    private static string BuildEvaluationPrompt(EvaluationContext context)
    {
        var hasExpected = !string.IsNullOrWhiteSpace(context.ExpectedOutput);

        return $@"You are an expert evaluator assessing the accuracy of AI-generated responses.

Query: {context.Query}

Response to Evaluate:
{context.Response}

{(hasExpected ? $@"Expected Answer:
{context.ExpectedOutput}

" : "")}Task: Evaluate the accuracy of the response on a scale from 0.0 to 1.0, where:
- 1.0 = Completely accurate, all facts are correct
- 0.8 = Mostly accurate with minor inaccuracies
- 0.6 = Partially accurate, some correct information
- 0.4 = Significant inaccuracies
- 0.2 = Mostly inaccurate
- 0.0 = Completely inaccurate or irrelevant

Provide your evaluation in the following JSON format:
{{
  ""score"": <number between 0.0 and 1.0>,
  ""confidence"": <your confidence in this score, 0.0 to 1.0>,
  ""explanation"": ""<brief explanation of the score>""
}}

Output only the JSON, no additional text.";
    }

    private static RubricScore ParseEvaluationResponse(string response)
    {
        try
        {
            // Extract JSON from response (in case there's extra text)
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var evaluation = JsonSerializer.Deserialize<EvaluationJson>(json);

                return new RubricScore
                {
                    Score = Math.Clamp(evaluation?.score ?? 0.0, 0.0, 1.0),
                    Confidence = evaluation?.confidence.HasValue == true
                        ? Math.Clamp(evaluation.confidence.Value, 0.0, 1.0)
                        : null,
                    Explanation = evaluation?.explanation
                };
            }
        }
        catch
        {
            // Fallback if parsing fails
        }

        return new RubricScore
        {
            Score = 0.5,
            Confidence = 0.0,
            Explanation = "Failed to parse LLM evaluation"
        };
    }

    private class EvaluationJson
    {
        public double score { get; set; }
        public double? confidence { get; set; }
        public string? explanation { get; set; }
    }
}

/// <summary>
/// Rubric for evaluating response relevance
/// </summary>
public class RelevanceRubric : IQualityRubric
{
    private readonly ILLMClient _llmClient;

    public string Name => "Relevance";
    public string Description => "Evaluates how relevant the response is to the query";

    public RelevanceRubric(ILLMClient llmClient)
    {
        _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
    }

    public async Task<RubricScore> EvaluateAsync(
        EvaluationContext context,
        CancellationToken cancellationToken = default)
    {
        var prompt = $@"You are an expert evaluator assessing response relevance.

Query: {context.Query}

Response to Evaluate:
{context.Response}

Task: Rate how relevant this response is to the query on a scale from 0.0 to 1.0:
- 1.0 = Directly addresses the query, highly relevant
- 0.8 = Mostly relevant, minor tangents
- 0.6 = Partially relevant
- 0.4 = Somewhat off-topic
- 0.2 = Mostly irrelevant
- 0.0 = Completely irrelevant

Output JSON format:
{{
  ""score"": <0.0 to 1.0>,
  ""confidence"": <0.0 to 1.0>,
  ""explanation"": ""<brief explanation>""
}}";

        var messages = new List<HazinaChatMessage>
        {
            new HazinaChatMessage(HazinaMessageRole.User, prompt)
        };

        var response = await _llmClient.GetResponse(messages, HazinaChatResponseFormat.Text, null, null, cancellationToken);

        return ParseResponse(response.Result);
    }

    private static RubricScore ParseResponse(string response)
    {
        try
        {
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var evaluation = JsonSerializer.Deserialize<EvaluationJson>(json);

                return new RubricScore
                {
                    Score = Math.Clamp(evaluation?.score ?? 0.0, 0.0, 1.0),
                    Confidence = evaluation?.confidence.HasValue == true
                        ? Math.Clamp(evaluation.confidence.Value, 0.0, 1.0)
                        : null,
                    Explanation = evaluation?.explanation
                };
            }
        }
        catch { }

        return new RubricScore { Score = 0.5, Confidence = 0.0, Explanation = "Parse failed" };
    }

    private class EvaluationJson
    {
        public double score { get; set; }
        public double? confidence { get; set; }
        public string? explanation { get; set; }
    }
}

/// <summary>
/// Rubric for evaluating response clarity
/// </summary>
public class ClarityRubric : IQualityRubric
{
    private readonly ILLMClient _llmClient;

    public string Name => "Clarity";
    public string Description => "Evaluates how clear and well-structured the response is";

    public ClarityRubric(ILLMClient llmClient)
    {
        _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
    }

    public async Task<RubricScore> EvaluateAsync(
        EvaluationContext context,
        CancellationToken cancellationToken = default)
    {
        var prompt = $@"Evaluate the clarity and structure of this response.

Query: {context.Query}

Response:
{context.Response}

Rate clarity from 0.0 to 1.0:
- 1.0 = Crystal clear, well-organized
- 0.8 = Clear with minor ambiguities
- 0.6 = Acceptable clarity
- 0.4 = Somewhat unclear
- 0.2 = Confusing
- 0.0 = Incomprehensible

JSON format:
{{
  ""score"": <0.0 to 1.0>,
  ""confidence"": <0.0 to 1.0>,
  ""explanation"": ""<brief explanation>""
}}";

        var messages = new List<HazinaChatMessage>
        {
            new HazinaChatMessage(HazinaMessageRole.User, prompt)
        };

        var response = await _llmClient.GetResponse(messages, HazinaChatResponseFormat.Text, null, null, cancellationToken);

        return ParseResponse(response.Result);
    }

    private static RubricScore ParseResponse(string response)
    {
        try
        {
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var evaluation = JsonSerializer.Deserialize<EvaluationJson>(json);

                return new RubricScore
                {
                    Score = Math.Clamp(evaluation?.score ?? 0.0, 0.0, 1.0),
                    Confidence = evaluation?.confidence,
                    Explanation = evaluation?.explanation
                };
            }
        }
        catch { }

        return new RubricScore { Score = 0.5, Confidence = 0.0, Explanation = "Parse failed" };
    }

    private class EvaluationJson
    {
        public double score { get; set; }
        public double? confidence { get; set; }
        public string? explanation { get; set; }
    }
}

/// <summary>
/// Factory implementation for quality rubrics
/// </summary>
public class QualityRubricFactory : IQualityRubricFactory
{
    private readonly Dictionary<string, IQualityRubric> _rubrics = new();
    private readonly ILLMClient _llmClient;

    public QualityRubricFactory(ILLMClient llmClient)
    {
        _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));

        // Register default rubrics
        RegisterRubric(new AccuracyRubric(llmClient));
        RegisterRubric(new RelevanceRubric(llmClient));
        RegisterRubric(new ClarityRubric(llmClient));
    }

    public IQualityRubric GetRubric(string name)
    {
        if (_rubrics.TryGetValue(name, out var rubric))
        {
            return rubric;
        }

        throw new ArgumentException($"Rubric '{name}' not found. Available: {string.Join(", ", _rubrics.Keys)}");
    }

    public void RegisterRubric(IQualityRubric rubric)
    {
        _rubrics[rubric.Name] = rubric;
    }

    public List<string> GetAvailableRubrics()
    {
        return new List<string>(_rubrics.Keys);
    }
}
