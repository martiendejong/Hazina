using backend.Extensions;
using Hazina.Tools.AI.Agents;
using Hazina.Tools.Data;
using Hazina.Tools.Models;
using Hazina.Tools.Services.DataGathering.Abstractions;
using Hazina.Tools.Services.DataGathering.ToolsContexts;
using Hazina.Tools.Services.Store;
using ProjectModel = Hazina.Tools.Models.Project;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
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

Guidelines:
- Only generate content when you have SUFFICIENT context from the conversation
- Be confident in your analysis before calling the tool
- Generate comprehensive, well-structured content appropriate to each field type
- The content format should match the field's purpose - use natural text for narrative fields, structured data for typed fields
- If the conversation or gathered data doesn't provide enough information for a field, don't generate it
- You can generate multiple fields if the conversation supports it

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

            var toolsContext = new AnalysisFieldToolsContext(
                _fieldsProvider,
                _notifier,
                _fileLocator,
                projectId,
                chatId,
                userId,
                availableFields,
                _agentFactory,
                _promptsRoot);

            // Build messages for the analysis call
            var messages = BuildAnalysisMessages(userMessage, conversationHistory, fieldsContext, availableFields);

            // Use GeneratorAgentBase if available, otherwise fall back to ILLMClient
            if (_agentFactory != null)
            {
                var agent = _agentFactory();
            var project = agent.Projects.Load(projectId);
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
            var project = agent.Projects.Load(projectId);
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
