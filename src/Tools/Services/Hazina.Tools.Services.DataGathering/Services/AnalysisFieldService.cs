using Hazina.LLMs;
using Hazina.Tools.Extensions;
using Hazina.Tools.AI.Agents;
using Hazina.Tools.Data;
using Hazina.Tools.Models;
using Hazina.Tools.Services.DataGathering.Abstractions;
using Hazina.Tools.Services.DataGathering.ToolsContexts;
using Hazina.Tools.Services.Store;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using ProjectModel = Hazina.Tools.Models.Project;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace Hazina.Tools.Services.DataGathering.Services;

/// <summary>
/// Service for automatically generating analysis fields from chat conversations.
/// </summary>
public sealed class AnalysisFieldService : IAnalysisFieldService
{
    private readonly IAnalysisFieldsProvider _fieldsProvider;
    private readonly IAnalysisFieldNotifier _notifier;
    private readonly Func<ILLMClient> _clientFactory;
    private readonly ILogger<AnalysisFieldService>? _logger;
    private readonly string _systemPrompt;
    private readonly string _promptsRoot;
    private readonly ProjectFileLocator? _fileLocator;
    private readonly IConfiguration? _configuration;
    private readonly Func<GeneratorAgentBase>? _agentFactory;

    // Cache for loaded field configurations per project
    private static readonly Dictionary<string, Dictionary<string, AnalysisFieldConfig>> _projectFieldConfigs =
        new Dictionary<string, Dictionary<string, AnalysisFieldConfig>>();

    private const string AnalysisPromptFile = "prompt.analysis.txt";

    private const string DefaultSystemPromptTemplate = @"You are an analysis assistant. Your job is to analyze conversations and generate structured analysis content when you have enough information.

You have access to analysis fields that need to be populated. When you identify that the conversation contains enough information to confidently generate content for an analysis field, use the UpdateAnalysisField tool.

{0}

## When to Generate New Fields (marked [NOT YET GENERATED])
Generate a field when you have sufficient context. Ask yourself:
- Does the conversation contain specific, relevant information for this field?
- Is there enough detail to create meaningful, useful content?
- Would the user benefit from having this field generated now?

If YES to all, generate it. Don't wait for perfect information - generate when you have ENOUGH.

## When to Update Existing Fields (marked [ALREADY GENERATED])
Update an existing field when ANY of these apply:
- The user explicitly asks to regenerate/update/improve a field
- The conversation provides significant NEW information that contradicts or substantially improves the existing content
- The user corrected information that was used to generate the field
- The user provided much more detail about the topic

Use the 'regenerate' parameter and explain your reasoning when updating existing fields.

## Feasibility Assessment
Before generating each field, quickly assess:
1. RELEVANCE: Is this field type relevant to the conversation topic?
2. DATA QUALITY: Is the information specific enough, not just vague mentions?
3. COMPLETENESS: Do you have at least 60% of what you need for a useful result?

If a field passes all three, generate it. Don't be overly conservative - it's better to generate something useful that can be refined than to wait indefinitely.

## Guidelines
- Generate comprehensive, well-structured content appropriate to each field type
- The content format should match the field's purpose - use natural text for narrative fields, structured data for typed fields
- You can generate multiple fields if the conversation supports it
- Always provide reasoning when generating or updating fields

After analyzing, respond with a brief summary of what you generated (or 'No analysis fields could be generated yet' if insufficient context).";

    public ProjectsRepository Projects { get; set; }

    public AnalysisFieldService(
        ProjectsRepository projects,
        IAnalysisFieldsProvider fieldsProvider,
        IAnalysisFieldNotifier notifier,
        Func<ILLMClient> clientFactory,
        ILogger<AnalysisFieldService>? logger = null,
        string? promptsRoot = null,
        ProjectFileLocator? fileLocator = null,
        IConfiguration? configuration = null,
        Func<GeneratorAgentBase>? agentFactory = null)
    {
        Projects = projects;
        _fieldsProvider = fieldsProvider ?? throw new ArgumentNullException(nameof(fieldsProvider));
        _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        _logger = logger;
        _promptsRoot = string.IsNullOrWhiteSpace(promptsRoot) ? AppContext.BaseDirectory : promptsRoot;
        _systemPrompt = LoadSystemPrompt(_promptsRoot);
        _fileLocator = fileLocator;
        _configuration = configuration;
        _agentFactory = agentFactory;
    }


    /// <summary>
    /// Load field configurations from the project's configuration file
    /// </summary>
    public async Task<Dictionary<string, AnalysisFieldConfig>> LoadFieldConfigsAsync(string projectId)
    {
        // Check cache first
        if (_projectFieldConfigs.ContainsKey(projectId))
            return _projectFieldConfigs[projectId];

        var fields = await _fieldsProvider.GetFieldsAsync(projectId);
        if (fields == null || fields.Count == 0)
            return new Dictionary<string, AnalysisFieldConfig>();

        var projectsFolder = Projects.ProjectsFolder;
        var fieldConfigs = fields.ToDictionary(
            f => f.Key,
            f => new AnalysisFieldConfig
            {
                FileName = f.File,
                DisplayName = f.DisplayName,
                GenericType = f.GenericType,
                ComponentName = f.ComponentName,
                RowComponentName = f.RowComponentName,
                PromptLoader = (_) => AnalysisFieldConfigLoader.LoadPrompt(projectsFolder, f)
            });

        _projectFieldConfigs[projectId] = fieldConfigs;
        return fieldConfigs;
    }

    public async Task<IReadOnlyList<GeneratedAnalysisField>> GenerateFromConversationAsync(
        string projectId,
        string chatId,
        string userMessage,
        IEnumerable<HazinaChatMessage> conversationHistory,
        string? userId = null,
        CancellationToken cancellationToken = default)
    {

        if (string.IsNullOrWhiteSpace(userMessage))
        {
            return Array.Empty<GeneratedAnalysisField>();
        }

        if (string.IsNullOrWhiteSpace(projectId))
        {
            _logger?.LogWarning("GenerateFromConversationAsync called without a projectId; skipping analysis generation.");
            return Array.Empty<GeneratedAnalysisField>();
        }

        try
        {
            // Get available fields to include in context
            var availableFields = await _fieldsProvider.GetFieldsAsync(projectId);

            // Check which fields already have content
            var existingFields = new Dictionary<string, string>();
            foreach (var field in availableFields)
            {
                var content = await GetFieldContentAsync(projectId, field.Key);
                if (!string.IsNullOrWhiteSpace(content))
                {
                    // Truncate long content for context
                    var preview = content.Length > 200 ? content.Substring(0, 200) + "..." : content;
                    existingFields[field.Key] = preview;
                }
            }

            var fieldsContext = BuildFieldsContext(availableFields, existingFields);

            // Smart regeneration detection
            var userRequestedRegeneration = DetectUserRegenerationIntent(userMessage, conversationHistory);
            var fieldsWithNewContext = DetectFieldsWithNewContext(userMessage, conversationHistory, availableFields, existingFields);

            _logger?.LogDebug("Analysis field generation context: userRequestedRegeneration={Regenerate}, fieldsWithNewContext={Fields}",
                userRequestedRegeneration, string.Join(",", fieldsWithNewContext));

            var toolsContext = new AnalysisFieldToolsContext(
                _fieldsProvider,
                _notifier,
                _fileLocator,
                projectId,
                chatId,
                userId,
                availableFields,
                _agentFactory,
                _promptsRoot,
                userRequestedRegeneration,
                fieldsWithNewContext,
                userMessage);

            // Build messages for the analysis call
            var messages = BuildAnalysisMessages(userMessage, conversationHistory, fieldsContext, availableFields);

            // Use GeneratorAgentBase if available, otherwise fall back to ILLMClient
            if (_agentFactory != null)
            {
                var agent = _agentFactory();
            var project = agent.Projects.Load(projectId)
                ?? throw new InvalidOperationException($"Project '{projectId}' not found.");
            var generator = await agent.GetGenerator(project, _systemPrompt);

                // Add conversation context to generator
                generator.BaseMessages.AddRange(messages.Skip(1)); // Skip system prompt as it's already in GetGenerator

                // Make the LLM call with tools using the generator
                await generator.GetResponse(
                    userMessage,
                    cancellationToken,
                    [],
                    true,
                    true,
                    toolsContext);
            }
            else
            {
                // Fallback to ILLMClient for backward compatibility
                var client = _clientFactory();
                await client.GetResponse(
                    messages,
                    HazinaChatResponseFormat.Text,
                    toolsContext,
                    images: null,
                    cancellationToken);
            }

            var generatedFields = toolsContext.GeneratedFields;
            var imageSetFields = toolsContext.ImageSetFieldsToGenerate;

            _logger?.LogInformation(
                "Generated {Count} analysis fields from conversation in chat {ChatId}. {ImageSetCount} ImageSet fields queued for generation.",
                generatedFields.Count, chatId, imageSetFields.Count);

            // For ImageSet fields that were requested, we need to return them in a special way
            // so the caller (ChatService) can trigger the actual image generation
            var allFields = generatedFields.ToList();
            foreach (var imageSetKey in imageSetFields)
            {
                var field = availableFields.FirstOrDefault(f => f.Key.Equals(imageSetKey, StringComparison.OrdinalIgnoreCase));
                if (field != null)
                {
                    allFields.Add(new GeneratedAnalysisField
                    {
                        Key = imageSetKey,
                        DisplayName = field.DisplayName,
                        Content = "__IMAGE_SET_GENERATION_REQUIRED__", // Special marker
                        GeneratedAt = DateTime.UtcNow
                    });
                }
            }

            return allFields;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error generating analysis fields from conversation in chat {ChatId}", chatId);
            return Array.Empty<GeneratedAnalysisField>();
        }
    }

    public Task<IReadOnlyList<AnalysisFieldInfo>> GetFieldsAsync(string projectId)
    {
        return _fieldsProvider.GetFieldsAsync(projectId);
    }

    public async Task<string?> GetFieldContentAsync(string projectId, string key)
    {
        var fields = await _fieldsProvider.GetFieldsAsync(projectId);
        var field = fields.FirstOrDefault(f => f.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        if (field == null) return null;

        // Read the file content
        var filePath = Path.Combine(_promptsRoot, projectId, field.File);
        if (!File.Exists(filePath)) return null;
        return await File.ReadAllTextAsync(filePath);
    }

    public async Task<bool> SaveFieldAsync(
        string projectId,
        string chatId,
        string key,
        string content,
        string? feedback = null,
        string? userId = null,
        CancellationToken cancellationToken = default)
    {
        var success = await _fieldsProvider.SaveFieldAsync(projectId, key, content, feedback, chatId, userId);
        return success;
    }

    /// <summary>
    /// Generates a typed analysis field using the same pattern as AnalysisController.
    /// Uses InternalGenerate&lt;T&gt; for fields with GenericType defined.
    /// </summary>
    public async Task<object?> GenerateTypedFieldAsync(
        string projectId,
        string chatId,
        string key,
        string? instruction = null,
        string? userId = null,
        CancellationToken cancellationToken = default)
    {
        if (_agentFactory == null || _configuration == null)
        {
            _logger?.LogWarning("Cannot generate typed field: agentFactory or configuration not provided");
            return null;
        }

        var fields = await _fieldsProvider.GetFieldsAsync(projectId);
        var field = fields.FirstOrDefault(f => f.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        if (field == null)
        {
            _logger?.LogWarning("Unknown analysis field key: {Key}", key);
            return null;
        }

        // If no GenericType, fall back to regular save
        if (string.IsNullOrWhiteSpace(field.GenericType))
        {
            _logger?.LogDebug("Field {Key} has no GenericType, skipping typed generation", key);
            return null;
        }

        // Handle ImageSet (logo) generation with image pipeline, not text generation
        if (string.Equals(field.GenericType, nameof(ImageSet), StringComparison.OrdinalIgnoreCase))
        {
            _logger?.LogInformation("Skipping ImageSet generation in DataGathering service (handled upstream).");
            return null;
        }

        // ImageSet/logo generation handled elsewhere (controller/service without Chat dependency)
        if (string.Equals(field.GenericType, nameof(ImageSet), StringComparison.OrdinalIgnoreCase))
        {
            _logger?.LogInformation("Skipping typed generation for ImageSet in AnalysisFieldService (handled upstream)");
            return null;
        }

        var resolvedType = AnalysisFieldConfigLoader.ResolveType(field.GenericType);
        if (resolvedType == null)
        {
            _logger?.LogWarning("Could not resolve GenericType: {GenericType} for field {Key}", field.GenericType, key);
            return null;
        }

        try
        {
            var agent = _agentFactory();

            // Load prompts - basis prompt + field-specific prompt
            var project = agent.Projects.Load(projectId)
                ?? throw new InvalidOperationException($"Project '{projectId}' not found.");
            var basisPrompt = GetBasisPrompt(agent);
            var projectsFolder = GetProjectsFolder();
            var fieldPrompt = AnalysisFieldConfigLoader.LoadPrompt(projectsFolder, field);
            if (string.IsNullOrWhiteSpace(fieldPrompt))
            {
                fieldPrompt = $"Generate the {key} analysis field based on the available project information.";
            }
            var projectLanguage = GetSelectedLanguage(project);
            var languagePrompt = BuildLanguageInstruction(projectLanguage);
            var systemPrompts = new[] { languagePrompt, fieldPrompt };

            // Use reflection to call InternalGenerate<T> with the resolved type
            var method = typeof(GeneratorAgentBase)
                .GetMethods()
                .First(m => m.Name == "InternalGenerate"
                            && m.IsGenericMethodDefinition
                            && m.GetParameters().Length == 5
                            && m.GetParameters()[1].ParameterType == typeof(string[]));

            var typedMethod = method.MakeGenericMethod(resolvedType);
            var taskObj = (Task)typedMethod.Invoke(agent, new object[]
            {
                projectId,
                systemPrompts,
                instruction ?? "Generate the information",
                field.DisplayName,
                field.File
            })!;

            await taskObj;

            // Extract the result from the task
            var resultProperty = taskObj.GetType().GetProperty("Result");
            var llmResponse = resultProperty?.GetValue(taskObj);

            // Get the actual result from LLMResponse<T>
            object? generatedContent = null;
            if (llmResponse != null)
            {
                var resultProp = llmResponse.GetType().GetProperty("Result");
                generatedContent = resultProp?.GetValue(llmResponse);
            }

            if (generatedContent != null)
            {
                _logger?.LogInformation("Generated typed analysis field {Key} for project {ProjectId}", key, projectId);
            }

            return generatedContent;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error generating typed analysis field {Key} for project {ProjectId}", key, projectId);
            return null;
        }
    }

    private string GetProjectsFolder()
    {
        if (_fileLocator is not null)
            return _fileLocator.ProjectsFolder;

        var fromConfig = _configuration?.GetSection("ProjectSettings:ProjectsFolder").Value;
        if (!string.IsNullOrWhiteSpace(fromConfig))
            return fromConfig;

        return _promptsRoot;
    }

    private static string GetBasisPrompt(GeneratorAgentBase agent)
    {
        if (!string.IsNullOrWhiteSpace(agent.BasisPrompt))
        {
            return agent.BasisPrompt;
        }

        var projectsFolder = agent.Projects?.ProjectsFolder;
        if (!string.IsNullOrWhiteSpace(projectsFolder))
        {
            var basePromptPath = Path.Combine(projectsFolder, "baseprompt.txt");
            if (File.Exists(basePromptPath))
            {
                return File.ReadAllText(basePromptPath);
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// Builds the fields context string showing available fields and their status.
    /// </summary>
    private static string BuildFieldsContext(
        IReadOnlyList<AnalysisFieldInfo> availableFields,
        Dictionary<string, string> existingFields)
    {
        var sb = new System.Text.StringBuilder();

        foreach (var field in availableFields)
        {
            if (existingFields.TryGetValue(field.Key, out var preview))
            {
                sb.AppendLine($"- {field.Key}: {field.DisplayName} [ALREADY GENERATED]");
                sb.AppendLine($"  Current content preview: {preview}");
            }
            else
            {
                sb.AppendLine($"- {field.Key}: {field.DisplayName} [NOT YET GENERATED]");
            }
        }

        return sb.ToString();
    }

    private static string GetSelectedLanguage(ProjectModel project)
    {
        if (project != null && !string.IsNullOrWhiteSpace(project.Language))
            return project.Language;
        return "en";
    }

    private static string BuildLanguageInstruction(string language)
    {
        if (string.IsNullOrWhiteSpace(language))
            return string.Empty;
        return $"Please provide your output in {language}.";
    }

    private List<HazinaChatMessage> BuildAnalysisMessages(
        string userMessage,
        IEnumerable<HazinaChatMessage> conversationHistory,
        string fieldsContext,
        IReadOnlyList<AnalysisFieldInfo> availableFields)
    {
        // Build dynamic JSON array of analysis fields
        var fieldsJson = BuildAnalysisFieldsJson(availableFields);

        // Format the system prompt with the fields JSON embedded in the narrative
        var formattedSystemPrompt = string.Format(_systemPrompt, fieldsJson);

        var existingFieldsNote = @"
IMPORTANT: Some fields may already be generated (marked [ALREADY GENERATED]).
- Do NOT regenerate fields that already have content unless:
  1. The user explicitly asks to update/regenerate a specific field
  2. The conversation reveals significant new information that contradicts or substantially improves the existing content
- Focus on generating fields marked [NOT YET GENERATED] when you have sufficient context.
";

        var enhancedPrompt = $"{formattedSystemPrompt}\n\n{existingFieldsNote}\nAnalysis fields status:\n{fieldsContext}";

        var messages = new List<HazinaChatMessage>
        {
            new(HazinaMessageRole.System, enhancedPrompt)
        };

        // Include recent conversation context
        var recentHistory = conversationHistory.TakeLast(10).ToList();

        if (recentHistory.Count > 0)
        {
            messages.Add(new HazinaChatMessage(HazinaMessageRole.System, "Here is the recent conversation context:"));
            messages.AddRange(recentHistory);
        }

        // Add the current message to analyze
        messages.Add(new HazinaChatMessage(
            HazinaMessageRole.User,
            $"Analyze this conversation and generate any analysis fields that have sufficient context. Remember: only regenerate existing fields if there's a compelling reason.\n\n{userMessage}"));

        return messages;
    }

    /// <summary>
    /// Builds the JSON array of analysis fields from the available fields configuration.
    /// This is metadata about which fields exist, not a specification of the output format.
    /// </summary>
    private static string BuildAnalysisFieldsJson(IReadOnlyList<AnalysisFieldInfo> availableFields)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine("  \"analysisFields\": [");

        for (int i = 0; i < availableFields.Count; i++)
        {
            var field = availableFields[i];
            sb.AppendLine("    {");
            sb.AppendLine($"      \"key\": \"{field.Key}\",");
            sb.AppendLine($"      \"fileName\": \"{field.File}\",");
            sb.AppendLine($"      \"displayName\": \"{field.DisplayName}\"");

            if (!string.IsNullOrWhiteSpace(field.GenericType))
                sb.AppendLine($"      ,\"genericType\": \"{field.GenericType}\"");
            if (!string.IsNullOrWhiteSpace(field.ComponentName))
                sb.AppendLine($"      ,\"componentName\": \"{field.ComponentName}\"");
            if (!string.IsNullOrWhiteSpace(field.RowComponentName))
                sb.AppendLine($"      ,\"rowComponentName\": \"{field.RowComponentName}\"");

            sb.Append("    }");
            if (i < availableFields.Count - 1)
                sb.AppendLine(",");
            else
                sb.AppendLine();
        }

        sb.AppendLine("  ]");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string LoadSystemPrompt(string root)
    {
        try
        {
            var path = Path.Combine(root, AnalysisPromptFile);
            if (File.Exists(path))
            {
                var text = File.ReadAllText(path);
                if (!string.IsNullOrWhiteSpace(text))
                    return text;
            }
        }
        catch { }

        return DefaultSystemPromptTemplate;
    }

    /// <summary>
    /// Detects if the user is explicitly asking to regenerate or update analysis fields.
    /// </summary>
    private static bool DetectUserRegenerationIntent(string userMessage, IEnumerable<HazinaChatMessage> conversationHistory)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
            return false;

        var lowerMessage = userMessage.ToLowerInvariant();

        // Strong regeneration intent phrases - these are unambiguous
        var strongIntentPhrases = new[]
        {
            "regenerate", "re-generate", "generate again", "create again",
            "redo the analysis", "re-do the analysis", "remake", "re-make",
            "update all fields", "refresh all", "recreate", "re-create",
            "regenerate all", "redo all"
        };

        if (strongIntentPhrases.Any(phrase => lowerMessage.Contains(phrase)))
        {
            Console.WriteLine($"[AnalysisFieldService] Detected regeneration intent: strong phrase match");
            return true;
        }

        // Field-specific update patterns - require "field" or "analysis" context
        // Using pre-compiled-style patterns to be more specific
        var fieldContextIndicators = new[] { "field", "analysis", "generated", "content" };
        var updateVerbs = new[] { "regenerate", "update", "redo", "refresh", "recreate", "fix", "improve", "change" };

        // Check if message has both a field context indicator AND an update verb
        var hasFieldContext = fieldContextIndicators.Any(ind => lowerMessage.Contains(ind));
        var hasUpdateVerb = updateVerbs.Any(verb => lowerMessage.Contains(verb));

        if (hasFieldContext && hasUpdateVerb)
        {
            Console.WriteLine($"[AnalysisFieldService] Detected regeneration intent: field context + update verb");
            return true;
        }

        return false;
    }

    /// <summary>
    /// Detects which fields have significant new context in the conversation that would justify regeneration.
    /// </summary>
    private static HashSet<string> DetectFieldsWithNewContext(
        string userMessage,
        IEnumerable<HazinaChatMessage> conversationHistory,
        IReadOnlyList<AnalysisFieldInfo> availableFields,
        Dictionary<string, string> existingFields)
    {
        var fieldsWithNewContext = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(userMessage))
            return fieldsWithNewContext;

        var lowerMessage = userMessage.ToLowerInvariant();
        var historyMessages = conversationHistory?.ToList() ?? new List<HazinaChatMessage>();

        foreach (var field in availableFields)
        {
            // Skip fields that don't exist yet - they should always be generated if possible
            if (!existingFields.ContainsKey(field.Key))
                continue;

            // Check if this message provides new context for this field
            if (HasNewContextForField(lowerMessage, field, historyMessages))
            {
                fieldsWithNewContext.Add(field.Key);
                Console.WriteLine($"[AnalysisFieldService] Field '{field.Key}' has new context from conversation");
            }
        }

        return fieldsWithNewContext;
    }

    /// <summary>
    /// Determines if the current message provides new/significant context for a specific field.
    /// </summary>
    private static bool HasNewContextForField(string lowerMessage, AnalysisFieldInfo field, List<HazinaChatMessage> history)
    {
        // Field name keywords to detect relevance
        var fieldKeywords = GetFieldKeywords(field);

        // Check if message is relevant to this field (require at least one keyword match)
        var matchedKeywords = fieldKeywords.Count(kw => lowerMessage.Contains(kw));
        if (matchedKeywords == 0)
            return false;

        // Check for explicit correction/clarification phrases (more specific than before)
        var correctionPhrases = new[]
        {
            "actually it's", "actually its", "instead of", "rather than",
            "i meant", "correction:", "let me clarify", "to clarify",
            "more specifically", "to be more precise", "i should mention that",
            "i forgot to mention", "i need to correct", "that's not right",
            "the correct", "should be", "is actually"
        };

        if (correctionPhrases.Any(phrase => lowerMessage.Contains(phrase)))
            return true;

        // Check if this is substantial new information (message is long and highly relevant)
        // Require at least 2 keyword matches for longer messages
        if (lowerMessage.Length > 150 && matchedKeywords >= 2)
            return true;

        // Check if user is providing specific details that weren't in history
        var userMessages = history.Where(m => m.Role == HazinaMessageRole.User).Select(m => m.Text?.ToLowerInvariant() ?? "").ToList();

        // Skip this check if no history (first message shouldn't trigger regeneration)
        if (userMessages.Count == 0)
            return false;

        var previousContent = string.Join(" ", userMessages);

        // Simple heuristic: if this message has substantial NEW content not in previous messages
        var words = lowerMessage.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 5) // Only consider words > 5 chars (more meaningful)
            .ToList();

        if (words.Count < 5) // Not enough substantial words to analyze
            return false;

        var newWords = words.Where(w => !previousContent.Contains(w)).ToList();

        // If more than 40% of substantial words are new AND message is relevant, consider it new context
        if (newWords.Count > words.Count * 0.4)
            return true;

        return false;
    }

    /// <summary>
    /// Gets relevant keywords for a field based on its key and display name.
    /// </summary>
    private static List<string> GetFieldKeywords(AnalysisFieldInfo field)
    {
        var keywords = new List<string>();

        // Add key parts (split by - and _)
        keywords.AddRange(field.Key.ToLowerInvariant().Split(new[] { '-', '_' }, StringSplitOptions.RemoveEmptyEntries));

        // Add display name words
        if (!string.IsNullOrWhiteSpace(field.DisplayName))
        {
            keywords.AddRange(field.DisplayName.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 3)); // Skip short words
        }

        // Remove common words
        var stopWords = new[] { "the", "and", "for", "with", "your", "this", "that" };
        keywords = keywords.Where(k => !stopWords.Contains(k)).Distinct().ToList();

        return keywords;
    }
}

public class AnalysisFieldConfig
{
    public string FileName { get; set; }
    public string DisplayName { get; set; }
    public string GenericType { get; set; }
    public string ComponentName { get; set; }
    public string RowComponentName { get; set; }
    public System.Func<IConfiguration, string> PromptLoader { get; set; }
}
