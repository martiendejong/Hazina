using System.Text.Json;
using System.Text.Json.Serialization;
using Hazina.AI.Agents.Planning;

namespace Hazina.AI.Agents.Tracing;

/// <summary>
/// Serialization utilities for agent traces and plans.
/// Ensures traces are fully serializable for persistence and debugging.
/// </summary>
public static class TraceSerialization
{
    private static readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Serialize trace to JSON
    /// </summary>
    public static string SerializeTrace(AgentTrace trace)
    {
        return JsonSerializer.Serialize(trace, _options);
    }

    /// <summary>
    /// Deserialize trace from JSON
    /// </summary>
    public static AgentTrace? DeserializeTrace(string json)
    {
        return JsonSerializer.Deserialize<AgentTrace>(json, _options);
    }

    /// <summary>
    /// Save trace to file
    /// </summary>
    public static async Task SaveTraceAsync(AgentTrace trace, string filePath)
    {
        var json = SerializeTrace(trace);
        await File.WriteAllTextAsync(filePath, json);
    }

    /// <summary>
    /// Load trace from file
    /// </summary>
    public static async Task<AgentTrace?> LoadTraceAsync(string filePath)
    {
        if (!File.Exists(filePath))
            return null;

        var json = await File.ReadAllTextAsync(filePath);
        return DeserializeTrace(json);
    }

    /// <summary>
    /// Serialize plan to JSON
    /// </summary>
    public static string SerializePlan(AgentPlan plan)
    {
        return JsonSerializer.Serialize(plan, _options);
    }

    /// <summary>
    /// Deserialize plan from JSON
    /// </summary>
    public static AgentPlan? DeserializePlan(string json)
    {
        return JsonSerializer.Deserialize<AgentPlan>(json, _options);
    }

    /// <summary>
    /// Save plan to file
    /// </summary>
    public static async Task SavePlanAsync(AgentPlan plan, string filePath)
    {
        var json = SerializePlan(plan);
        await File.WriteAllTextAsync(filePath, json);
    }

    /// <summary>
    /// Load plan from file
    /// </summary>
    public static async Task<AgentPlan?> LoadPlanAsync(string filePath)
    {
        if (!File.Exists(filePath))
            return null;

        var json = await File.ReadAllTextAsync(filePath);
        return DeserializePlan(json);
    }

    /// <summary>
    /// Append trace event to JSONL file
    /// </summary>
    public static async Task AppendTraceEventAsync(TraceEvent traceEvent, string filePath)
    {
        var json = JsonSerializer.Serialize(traceEvent, _options);
        await File.AppendAllTextAsync(filePath, json + Environment.NewLine);
    }

    /// <summary>
    /// Export trace to Markdown for human readability
    /// </summary>
    public static string ExportTraceToMarkdown(AgentTrace trace)
    {
        var md = new System.Text.StringBuilder();

        md.AppendLine($"# Agent Trace: {trace.TraceId}");
        md.AppendLine();
        md.AppendLine($"**Agent:** {trace.AgentName}");
        md.AppendLine($"**Task:** {trace.Task}");
        md.AppendLine($"**Start Time:** {trace.StartTime:yyyy-MM-dd HH:mm:ss UTC}");
        if (trace.EndTime.HasValue)
        {
            md.AppendLine($"**End Time:** {trace.EndTime.Value:yyyy-MM-dd HH:mm:ss UTC}");
            md.AppendLine($"**Duration:** {trace.GetDuration()}");
        }
        md.AppendLine();

        md.AppendLine("## Events");
        md.AppendLine();

        foreach (var evt in trace.Events)
        {
            md.AppendLine($"### {evt.EventType} - {evt.Timestamp:HH:mm:ss.fff}");
            md.AppendLine();

            switch (evt)
            {
                case ToolCallEvent toolCall:
                    md.AppendLine($"**Tool:** {toolCall.ToolName}");
                    md.AppendLine("**Arguments:**");
                    foreach (var arg in toolCall.Arguments)
                    {
                        md.AppendLine($"- {arg.Key}: {arg.Value}");
                    }
                    md.AppendLine($"**Result:** {toolCall.Result}");
                    break;

                case DecisionEvent decision:
                    md.AppendLine($"**Decision:** {decision.Decision}");
                    md.AppendLine($"**Reasoning:** {decision.Reasoning}");
                    if (decision.Context != null)
                    {
                        md.AppendLine("**Context:**");
                        foreach (var ctx in decision.Context)
                        {
                            md.AppendLine($"- {ctx.Key}: {ctx.Value}");
                        }
                    }
                    break;

                case RetrievalEvent retrieval:
                    md.AppendLine($"**Query:** {retrieval.Query}");
                    md.AppendLine($"**Reranker:** {retrieval.RerankerUsed}");
                    md.AppendLine($"**Retrieved Chunks:** {string.Join(", ", retrieval.RetrievedChunkIds)}");
                    break;

                case LogEvent log:
                    md.AppendLine($"**[{log.Level}]** {log.Message}");
                    break;
            }

            md.AppendLine();
        }

        return md.ToString();
    }

    /// <summary>
    /// Export plan to Markdown for human readability
    /// </summary>
    public static string ExportPlanToMarkdown(AgentPlan plan)
    {
        var md = new System.Text.StringBuilder();

        md.AppendLine($"# Agent Plan: {plan.PlanId}");
        md.AppendLine();
        md.AppendLine($"**Goal:** {plan.Goal}");
        md.AppendLine($"**Status:** {plan.Status}");
        md.AppendLine($"**Progress:** {plan.GetProgress():P0}");
        md.AppendLine($"**Created:** {plan.CreatedAt:yyyy-MM-dd HH:mm:ss UTC}");
        md.AppendLine($"**Updated:** {plan.UpdatedAt:yyyy-MM-dd HH:mm:ss UTC}");
        md.AppendLine();

        md.AppendLine("## Steps");
        md.AppendLine();

        for (int i = 0; i < plan.Steps.Count; i++)
        {
            var step = plan.Steps[i];
            var statusIcon = step.Status switch
            {
                StepStatus.Done => "✅",
                StepStatus.Active => "🔄",
                StepStatus.Failed => "❌",
                StepStatus.Skipped => "⏭️",
                _ => "⏸️"
            };

            md.AppendLine($"{i + 1}. {statusIcon} **{step.Description}** `[{step.Status}]`");

            if (step.StartedAt.HasValue)
            {
                md.AppendLine($"   - Started: {step.StartedAt.Value:HH:mm:ss}");
            }

            if (step.CompletedAt.HasValue)
            {
                md.AppendLine($"   - Completed: {step.CompletedAt.Value:HH:mm:ss}");
            }

            if (!string.IsNullOrEmpty(step.Result))
            {
                md.AppendLine($"   - Result: {step.Result}");
            }

            if (!string.IsNullOrEmpty(step.Error))
            {
                md.AppendLine($"   - Error: {step.Error}");
            }

            md.AppendLine();
        }

        return md.ToString();
    }
}
