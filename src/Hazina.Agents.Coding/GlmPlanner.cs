using System.Text.Json;
using Hazina.AI.Providers.Core;

namespace Hazina.Agents.Coding;

/// <summary>
/// GLM planner - enforces JSON-only responses with strict schema.
/// No free-form chat, no direct execution.
/// Maximum 2 retry attempts on invalid JSON.
/// </summary>
public class GlmPlanner
{
    private readonly IProviderOrchestrator _orchestrator;
    private readonly GlmPlannerOptions _options;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true
    };

    public GlmPlanner(IProviderOrchestrator orchestrator, GlmPlannerOptions? options = null)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _options = options ?? new GlmPlannerOptions();
    }

    /// <summary>
    /// Generate plan from GLM - enforces JSON schema
    /// </summary>
    public async Task<PlanResult> GeneratePlanAsync(AgentContext context)
    {
        var systemPrompt = BuildSystemPrompt();
        var userPrompt = BuildUserPrompt(context);

        int attempts = 0;
        const int maxAttempts = 2;

        while (attempts < maxAttempts)
        {
            attempts++;

            try
            {
                var messages = new List<HazinaChatMessage>
                {
                    new HazinaChatMessage
                    {
                        Role = HazinaMessageRole.System,
                        Text = systemPrompt
                    },
                    new HazinaChatMessage
                    {
                        Role = HazinaMessageRole.User,
                        Text = userPrompt
                    }
                };

                var response = await _orchestrator.GetResponse(
                    messages,
                    HazinaChatResponseFormat.Json,
                    null,
                    null,
                    CancellationToken.None
                );

                var planResult = ParsePlanResult(response.Result);

                if (planResult == null)
                {
                    if (attempts >= maxAttempts)
                    {
                        throw new InvalidOperationException(
                            $"GLM failed to produce valid JSON after {maxAttempts} attempts"
                        );
                    }

                    userPrompt = BuildRetryPrompt(context, response.Result);
                    continue;
                }

                var (valid, error) = ToolRegistry.ValidateActions(planResult.Actions);
                if (!valid)
                {
                    if (attempts >= maxAttempts)
                    {
                        throw new InvalidOperationException($"Invalid actions: {error}");
                    }

                    userPrompt = BuildRetryPromptWithError(context, error!);
                    continue;
                }

                return planResult;
            }
            catch (JsonException ex)
            {
                if (attempts >= maxAttempts)
                {
                    throw new InvalidOperationException(
                        $"Failed to parse GLM response as JSON: {ex.Message}"
                    );
                }
            }
        }

        throw new InvalidOperationException("Failed to generate valid plan");
    }

    /// <summary>
    /// Build system prompt enforcing JSON-only output
    /// </summary>
    private string BuildSystemPrompt()
    {
        return @"You are a deterministic coding agent planner. Your ONLY job is to output valid JSON.

MANDATORY OUTPUT FORMAT:
{
  ""thought"": ""short internal reasoning"",
  ""plan"": [
    ""step 1"",
    ""step 2""
  ],
  ""actions"": [
    {
      ""tool"": ""read_file|apply_diff|run|git_status|git_diff"",
      ""path"": ""optional file path"",
      ""diff"": ""optional unified diff"",
      ""command"": ""optional PowerShell command""
    }
  ]
}

ALLOWED TOOLS:
- read_file: Requires ""path""
- apply_diff: Requires ""path"" and ""diff"" (unified diff format)
- run: Requires ""command"" (PowerShell syntax)
- git_status: No parameters
- git_diff: No parameters

RULES:
1. Output ONLY valid JSON matching the schema above
2. No markdown code blocks, no explanations outside JSON
3. Actions execute sequentially - order matters
4. If task is complete, return empty actions array
5. Never use destructive commands (rm -rf, del /s, etc.)
6. All file paths are relative to working directory
7. Return empty actions when task is done or tests pass

YOU ARE A PLANNER, NOT AN EXECUTOR. Never perform actions yourself.";
    }

    /// <summary>
    /// Build user prompt with task and context
    /// </summary>
    private string BuildUserPrompt(AgentContext context)
    {
        var prompt = $@"TASK: {context.Task}

WORKING DIRECTORY: {context.WorkingDirectory}

ITERATION: {context.Iteration}";

        if (!string.IsNullOrWhiteSpace(context.MemorySummary))
        {
            prompt += $@"

MEMORY SUMMARY:
{context.MemorySummary}";
        }

        if (context.PreviousResults != null && context.PreviousResults.Count > 0)
        {
            prompt += "\n\nPREVIOUS RESULTS:";
            foreach (var result in context.PreviousResults)
            {
                prompt += $@"
Tool: {result.Tool}
Success: {result.Success}
Output: {result.Output.Substring(0, Math.Min(500, result.Output.Length))}";

                if (!string.IsNullOrWhiteSpace(result.Error))
                {
                    prompt += $@"
Error: {result.Error}";
                }
            }
        }

        prompt += "\n\nGenerate your plan as JSON:";

        return prompt;
    }

    /// <summary>
    /// Build retry prompt on invalid JSON
    /// </summary>
    private string BuildRetryPrompt(AgentContext context, string invalidResponse)
    {
        return BuildUserPrompt(context) + $@"

PREVIOUS ATTEMPT WAS INVALID JSON:
{invalidResponse.Substring(0, Math.Min(200, invalidResponse.Length))}

Try again with VALID JSON only.";
    }

    /// <summary>
    /// Build retry prompt with validation error
    /// </summary>
    private string BuildRetryPromptWithError(AgentContext context, string error)
    {
        return BuildUserPrompt(context) + $@"

VALIDATION ERROR:
{error}

Fix the error and output valid JSON.";
    }

    /// <summary>
    /// Parse JSON response into PlanResult
    /// </summary>
    private PlanResult? ParsePlanResult(string json)
    {
        try
        {
            var cleanJson = json.Trim();

            if (cleanJson.StartsWith("```"))
            {
                var lines = cleanJson.Split('\n');
                cleanJson = string.Join('\n', lines.Skip(1).SkipLast(1));
            }

            var parsed = JsonSerializer.Deserialize<PlanResult>(cleanJson, JsonOptions);

            if (parsed == null)
                return null;

            if (parsed.Thought == null || parsed.Plan == null || parsed.Actions == null)
                return null;

            return parsed;
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// GLM planner configuration options
/// </summary>
public class GlmPlannerOptions
{
    /// <summary>
    /// Maximum retries on invalid JSON (default: 2)
    /// </summary>
    public int MaxRetries { get; set; } = 2;
}
