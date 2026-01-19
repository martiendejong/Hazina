using System.Text;

namespace Hazina.AI.Workflows.Configuration;

/// <summary>
/// Parser for .hazina workflow format (v2.0)
/// Supports both legacy v1 format and new v2 format with per-step configuration
/// </summary>
public static class HazinaWorkflowConfigParser
{
    /// <summary>
    /// Parse .hazina workflow file
    /// </summary>
    public static WorkflowConfig Parse(string input)
    {
        var lines = input.Split(new[] { "\r\n", "\n" }, StringSplitOptions.TrimEntries);

        // Detect version
        var version = DetectVersion(lines);

        if (version == "1.0" || version == "1")
        {
            return ParseV1Format(input);
        }
        else
        {
            return ParseV2Format(input);
        }
    }

    /// <summary>
    /// Detect format version from content
    /// </summary>
    private static string DetectVersion(string[] lines)
    {
        foreach (var line in lines)
        {
            if (line.StartsWith("Version:", StringComparison.OrdinalIgnoreCase))
            {
                return line.Substring(8).Trim();
            }
        }

        // Check for [StepN] sections (v2 indicator)
        if (lines.Any(l => l.StartsWith("[Step", StringComparison.OrdinalIgnoreCase)))
        {
            return "2.0";
        }

        return "1.0"; // Default to v1
    }

    /// <summary>
    /// Parse v2 format (with [StepN] sections)
    /// </summary>
    private static WorkflowConfig ParseV2Format(string input)
    {
        var config = new WorkflowConfig();
        var lines = input.Split(new[] { "\r\n", "\n" }, StringSplitOptions.TrimEntries);

        WorkflowStepConfig? currentStep = null;
        bool inWorkflowHeader = true;

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                continue;

            // Step section start
            if (line.StartsWith("[Step", StringComparison.OrdinalIgnoreCase))
            {
                if (currentStep != null)
                {
                    config.Steps.Add(currentStep);
                }
                currentStep = new WorkflowStepConfig();
                inWorkflowHeader = false;
                continue;
            }

            var colonIndex = line.IndexOf(':');
            if (colonIndex < 0) continue;

            var key = line.Substring(0, colonIndex).Trim();
            var value = line.Substring(colonIndex + 1).Trim();

            if (inWorkflowHeader)
            {
                ParseWorkflowHeaderField(config, key, value);
            }
            else if (currentStep != null)
            {
                ParseStepField(currentStep, key, value);
            }
        }

        // Add last step
        if (currentStep != null)
        {
            config.Steps.Add(currentStep);
        }

        return config;
    }

    /// <summary>
    /// Parse workflow header fields (Name, Description, Version, etc.)
    /// </summary>
    private static void ParseWorkflowHeaderField(WorkflowConfig config, string key, string value)
    {
        switch (key)
        {
            case "Name":
                config.Name = value;
                break;
            case "Description":
                config.Description = value;
                break;
            case "Version":
                config.Version = value;
                break;
            // Additional metadata fields can be added to Metadata dictionary
            default:
                config.Metadata[key] = value;
                break;
        }
    }

    /// <summary>
    /// Parse step-level fields
    /// </summary>
    private static void ParseStepField(WorkflowStepConfig step, string key, string value)
    {
        switch (key)
        {
            case "Name":
                step.Name = value;
                break;
            case "Type":
                step.Type = Enum.Parse<StepType>(value, ignoreCase: true);
                break;
            case "AgentName":
                step.AgentName = value;
                break;
            case "Input":
                step.Input = value;
                break;
            case "OutputKey":
                step.OutputKey = value;
                break;
            case "ContinueOnFailure":
                step.ContinueOnFailure = bool.Parse(value);
                break;
            case "StepTimeout":
                step.StepTimeout = int.Parse(value);
                break;

            // LLM Configuration
            case "Model":
            case "Temperature":
            case "MaxTokens":
            case "TopP":
            case "FrequencyPenalty":
            case "PresencePenalty":
            case "FallbackModel":
                ParseLLMField(step, key, value);
                break;

            // RAG Configuration
            case "RAGStore":
            case "RAGTopK":
            case "RAGMinSimilarity":
            case "RAGUseEmbeddings":
            case "RAGMetadataFilter":
            case "RAGMaxContextLength":
                ParseRAGField(step, key, value);
                break;

            // Guardrails
            case "Guardrails":
                step.Guardrails = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
                break;
        }
    }

    /// <summary>
    /// Parse LLM configuration fields
    /// </summary>
    private static void ParseLLMField(WorkflowStepConfig step, string key, string value)
    {
        step.LLMConfig ??= new LLMStepConfig();

        switch (key)
        {
            case "Model":
                step.LLMConfig.Model = value;
                break;
            case "FallbackModel":
                step.LLMConfig.FallbackModel = value;
                break;
            case "Temperature":
                step.LLMConfig.Temperature = float.Parse(value);
                break;
            case "MaxTokens":
                step.LLMConfig.MaxTokens = int.Parse(value);
                break;
            case "TopP":
                step.LLMConfig.TopP = float.Parse(value);
                break;
            case "FrequencyPenalty":
                step.LLMConfig.FrequencyPenalty = float.Parse(value);
                break;
            case "PresencePenalty":
                step.LLMConfig.PresencePenalty = float.Parse(value);
                break;
        }
    }

    /// <summary>
    /// Parse RAG configuration fields
    /// </summary>
    private static void ParseRAGField(WorkflowStepConfig step, string key, string value)
    {
        step.RAGConfig ??= new RAGStepConfig();

        switch (key)
        {
            case "RAGStore":
                step.RAGConfig.StoreName = value;
                break;
            case "RAGTopK":
                step.RAGConfig.TopK = int.Parse(value);
                break;
            case "RAGMinSimilarity":
                step.RAGConfig.MinSimilarity = double.Parse(value);
                break;
            case "RAGUseEmbeddings":
                step.RAGConfig.UseEmbeddings = bool.Parse(value);
                break;
            case "RAGMetadataFilter":
                step.RAGConfig.MetadataFilter = value;
                break;
            case "RAGMaxContextLength":
                step.RAGConfig.MaxContextLength = int.Parse(value);
                break;
        }
    }

    /// <summary>
    /// Parse legacy v1 format (backward compatibility)
    /// </summary>
    private static WorkflowConfig ParseV1Format(string input)
    {
        // Use existing HazinaFlowConfigParser for v1 format
        var v1Flows = HazinaFlowConfigParser.Parse(input);

        // Convert to v2 WorkflowConfig
        var config = new WorkflowConfig();

        if (v1Flows.Any())
        {
            var v1Flow = v1Flows.First();
            config.Name = v1Flow.Name;
            config.Description = v1Flow.Description;
            config.Version = "1.0";

            // Convert CallsAgents to steps
            foreach (var agentName in v1Flow.CallsAgents)
            {
                config.Steps.Add(new WorkflowStepConfig
                {
                    Name = agentName,
                    Type = StepType.AgentTask,
                    AgentName = agentName,
                    Input = "{previousResult}",
                    OutputKey = agentName.ToLowerInvariant()
                });
            }
        }

        return config;
    }

    /// <summary>
    /// Load workflow from .hazina file
    /// </summary>
    public static WorkflowConfig LoadFromFile(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Workflow file not found: {path}");

        var content = File.ReadAllText(path);
        return Parse(content);
    }

    /// <summary>
    /// Save workflow to .hazina file
    /// </summary>
    public static void SaveToFile(WorkflowConfig config, string path)
    {
        var content = Serialize(config);
        File.WriteAllText(path, content);
    }

    /// <summary>
    /// Serialize workflow to .hazina format
    /// </summary>
    public static string Serialize(WorkflowConfig config)
    {
        var sb = new StringBuilder();

        // Workflow header
        sb.AppendLine($"# Workflow Definition");
        sb.AppendLine($"Name: {config.Name}");
        sb.AppendLine($"Description: {config.Description}");
        sb.AppendLine($"Version: {config.Version}");
        sb.AppendLine($"Steps: {config.Steps.Count}");
        sb.AppendLine();

        // Steps
        for (int i = 0; i < config.Steps.Count; i++)
        {
            var step = config.Steps[i];
            sb.AppendLine($"[Step{i + 1}]");
            SerializeStep(sb, step);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// Serialize a single step
    /// </summary>
    private static void SerializeStep(StringBuilder sb, WorkflowStepConfig step)
    {
        sb.AppendLine($"Name: {step.Name}");
        sb.AppendLine($"Type: {step.Type}");
        sb.AppendLine($"AgentName: {step.AgentName}");
        sb.AppendLine($"Input: {step.Input}");

        // LLM Config
        if (step.LLMConfig != null)
        {
            sb.AppendLine($"Temperature: {step.LLMConfig.Temperature}");
            sb.AppendLine($"MaxTokens: {step.LLMConfig.MaxTokens}");
            sb.AppendLine($"Model: {step.LLMConfig.Model}");
            sb.AppendLine($"TopP: {step.LLMConfig.TopP}");
            sb.AppendLine($"FrequencyPenalty: {step.LLMConfig.FrequencyPenalty}");
            sb.AppendLine($"PresencePenalty: {step.LLMConfig.PresencePenalty}");
            if (!string.IsNullOrEmpty(step.LLMConfig.FallbackModel))
                sb.AppendLine($"FallbackModel: {step.LLMConfig.FallbackModel}");
        }

        // RAG Config
        if (step.RAGConfig != null)
        {
            sb.AppendLine($"RAGStore: {step.RAGConfig.StoreName}");
            sb.AppendLine($"RAGTopK: {step.RAGConfig.TopK}");
            sb.AppendLine($"RAGMinSimilarity: {step.RAGConfig.MinSimilarity}");
            sb.AppendLine($"RAGUseEmbeddings: {step.RAGConfig.UseEmbeddings}");
            if (!string.IsNullOrEmpty(step.RAGConfig.MetadataFilter))
                sb.AppendLine($"RAGMetadataFilter: {step.RAGConfig.MetadataFilter}");
            sb.AppendLine($"RAGMaxContextLength: {step.RAGConfig.MaxContextLength}");
        }

        // Guardrails
        if (step.Guardrails.Any())
        {
            sb.AppendLine($"Guardrails: {string.Join(",", step.Guardrails)}");
        }

        sb.AppendLine($"StepTimeout: {step.StepTimeout}");
        sb.AppendLine($"OutputKey: {step.OutputKey}");
        sb.AppendLine($"ContinueOnFailure: {step.ContinueOnFailure}");
    }
}
