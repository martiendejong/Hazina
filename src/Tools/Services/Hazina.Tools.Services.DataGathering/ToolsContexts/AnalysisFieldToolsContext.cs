using DevGPT.GenerationTools.AI.Agents;
using DevGPT.GenerationTools.Data;
using DevGPT.GenerationTools.Models;
using DevGPT.GenerationTools.Services.DataGathering.Abstractions;
using DevGPT.GenerationTools.Services.Store;
using ProjectModel = DevGPT.GenerationTools.Models.Project;
using OpenAI.Chat;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace DevGPT.GenerationTools.Services.DataGathering.ToolsContexts;

/// <summary>
/// Tools context for analysis field generation.
/// Provides tools for the LLM to generate and save analysis fields.
/// </summary>
public sealed class AnalysisFieldToolsContext : ToolsContextBase
{
    private readonly IAnalysisFieldsProvider _fieldsProvider;
    private readonly IAnalysisFieldNotifier _notifier;
    private readonly ProjectFileLocator? _fileLocator;
    private readonly string _projectId;
    private readonly string _chatId;
    private readonly string? _userId;
    private readonly IReadOnlyList<AnalysisFieldInfo> _availableFields;
    private readonly Func<GeneratorAgentBase>? _agentFactory;
    private readonly string? _promptsRoot;

    private readonly List<GeneratedAnalysisField> _generatedFields = new();
    private readonly List<string> _imageSetFieldsToGenerate = new();

    /// <summary>
    /// Gets the fields that were generated during tool execution.
    /// </summary>
    public IReadOnlyList<GeneratedAnalysisField> GeneratedFields => _generatedFields.AsReadOnly();

    /// <summary>
    /// Gets the ImageSet field keys that were requested for generation (require special image pipeline handling).
    /// </summary>
    public IReadOnlyList<string> ImageSetFieldsToGenerate => _imageSetFieldsToGenerate.AsReadOnly();

    public AnalysisFieldToolsContext(
        IAnalysisFieldsProvider fieldsProvider,
        IAnalysisFieldNotifier notifier,
        ProjectFileLocator? fileLocator,
        string projectId,
        string chatId,
        string? userId,
        IReadOnlyList<AnalysisFieldInfo> availableFields,
        Func<GeneratorAgentBase>? agentFactory = null,
        string? promptsRoot = null)
    {
        _fieldsProvider = fieldsProvider ?? throw new ArgumentNullException(nameof(fieldsProvider));
        _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
        _fileLocator = fileLocator;
        _projectId = projectId ?? throw new ArgumentNullException(nameof(projectId));
        _chatId = chatId ?? throw new ArgumentNullException(nameof(chatId));
        _userId = userId;
        _availableFields = availableFields ?? Array.Empty<AnalysisFieldInfo>();
        _agentFactory = agentFactory;
        _promptsRoot = promptsRoot ?? AppContext.BaseDirectory;

        RegisterTools();
    }

    private void RegisterTools()
    {
        Add("UpdateAnalysisField",
            "Generate and save content for an analysis field. Only call when you have enough context to generate meaningful content. IMPORTANT: If the field has a 'typeSignature' (check GetAvailableFields), the content MUST be valid JSON matching that signature.",
            [
                CreateParameter("key", "The analysis field key (e.g., 'topic-synopsis', 'central-thesis')", "string", required: true),
                CreateParameter("content", "The generated content for this analysis field. For fields with typeSignature, this must be valid JSON matching the signature.", "string", required: true),
                CreateParameter("reasoning", "Brief explanation of why you generated this content now", "string", required: false)
            ],
            UpdateAnalysisFieldAsync);

        Add("GetAvailableFields",
            "Get the list of available analysis fields that can be generated.",
            [],
            GetAvailableFieldsAsync);

        Add("GetExistingFieldContent",
            "Check if an analysis field already has content.",
            [
                CreateParameter("key", "The analysis field key to check", "string", required: true)
            ],
            GetExistingFieldContentAsync);
    }

    private async Task<string> UpdateAnalysisFieldAsync(
        List<DevGPTChatMessage> messages,
        DevGPTChatToolCall toolCall,
        CancellationToken cancellationToken)
    {
        var key = GetStringParameter(toolCall, "key");
        var content = GetStringParameter(toolCall, "content");
        var reasoning = GetStringParameter(toolCall, "reasoning");

        if (string.IsNullOrWhiteSpace(key))
            return JsonResult(false, "Parameter 'key' is required.");

        // Check if this field was already generated in this call
        if (_generatedFields.Any(f => f.Key.Equals(key, StringComparison.OrdinalIgnoreCase)))
            return JsonResult(true, $"Field {key} already generated in this session.");

        // Validate the key exists in available fields
        var field = _availableFields.FirstOrDefault(f =>
            f.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

        if (field == null)
            return JsonResult(false, $"Unknown analysis field key: {key}");

        // ImageSet/logo requires special image generation pipeline (not text generation)
        if (string.Equals(field.GenericType, nameof(ImageSet), StringComparison.OrdinalIgnoreCase))
        {
            // Mark this field for image generation (handled by caller with image pipeline)
            if (!_imageSetFieldsToGenerate.Contains(key))
            {
                _imageSetFieldsToGenerate.Add(key);
            }
            return JsonSerializer.Serialize(new
            {
                ok = true,
                key,
                displayName = field.DisplayName,
                requiresImageGeneration = true,
                message = "ImageSet generation queued (requires image pipeline)"
            });
        }

        // For other typed fields, use GeneratorAgentBase.InternalGenerate<T>
        if (!string.IsNullOrWhiteSpace(field.GenericType) && _agentFactory != null)
        {
            return await GenerateTypedFieldAsync(field, reasoning, cancellationToken);
        }

        // For non-typed fields, save content directly
        if (string.IsNullOrWhiteSpace(content))
            return JsonResult(false, "Parameter 'content' is required.");

        var ok = await _fieldsProvider.SaveFieldAsync(_projectId, key, content, reasoning, _chatId, _userId);

        if (ok)
        {
            // Track generated field
            _generatedFields.Add(new GeneratedAnalysisField
            {
                Key = key,
                DisplayName = field.DisplayName,
                Content = content,
                GeneratedAt = DateTime.UtcNow
            });

            // Only notify/persist for chat-based generation (not sidebar)
            // Sidebar generations use fake chatIds like "analysis-{fieldKey}"
            if (!_chatId.StartsWith("analysis-"))
            {
                // Send SignalR notification for real-time display
                await _notifier.NotifyFieldGeneratedAsync(
                    _projectId, _chatId, key, field.DisplayName, content, reasoning, field.ComponentName, cancellationToken);

                // Persist to chat file
                if (_fileLocator is not null && !string.IsNullOrWhiteSpace(_chatId))
                {
                    await PersistToChatFileAsync(key, field.DisplayName, content, reasoning, field.ComponentName);
                }
            }
        }

        return JsonSerializer.Serialize(new { ok, key, displayName = field.DisplayName });
    }

    /// <summary>
    /// Generates a typed field using GeneratorAgentBase.InternalGenerate&lt;T&gt;
    /// Same pattern as AnalysisController.GenerateAnalysisField
    /// </summary>
    private async Task<string> GenerateTypedFieldAsync(
        AnalysisFieldInfo field,
        string? reasoning,
        CancellationToken cancellationToken)
    {
        var resolvedType = AnalysisFieldConfigLoader.ResolveType(field.GenericType!);
        if (resolvedType == null)
            return JsonResult(false, $"Could not resolve GenericType: {field.GenericType}");

        try
        {
            var agent = _agentFactory!();

            // Load prompts - basis prompt + field-specific prompt
            var project = agent.Projects.Load(_projectId);
            var basisPrompt = GetBasisPrompt(agent);
            var fieldPrompt = AnalysisFieldConfigLoader.LoadPrompt(GetProjectsFolder(), field);
            if (string.IsNullOrWhiteSpace(fieldPrompt))
            {
                fieldPrompt = $"Generate the {field.Key} analysis field based on the available project information.";
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
                _projectId,
                systemPrompts,
                reasoning ?? "Generate the information",
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
                var contentJson = JsonSerializer.Serialize(generatedContent);

                // Track generated field
                _generatedFields.Add(new GeneratedAnalysisField
                {
                    Key = field.Key,
                    DisplayName = field.DisplayName,
                    Content = contentJson,
                    GeneratedAt = DateTime.UtcNow
                });

                // Only notify/persist for chat-based generation (not sidebar)
                // Sidebar generations use fake chatIds like "analysis-{fieldKey}"
                if (!_chatId.StartsWith("analysis-"))
                {
                    // Send SignalR notification for real-time display
                    await _notifier.NotifyFieldGeneratedAsync(
                        _projectId, _chatId, field.Key, field.DisplayName, contentJson, reasoning, field.ComponentName, cancellationToken);

                    // Persist to chat file
                    if (_fileLocator is not null && !string.IsNullOrWhiteSpace(_chatId))
                    {
                        await PersistToChatFileAsync(field.Key, field.DisplayName, contentJson, reasoning, field.ComponentName);
                    }
                }

                return JsonSerializer.Serialize(new { ok = true, key = field.Key, displayName = field.DisplayName });
            }

            return JsonResult(false, "Failed to generate typed content");
        }
        catch (Exception ex)
        {
            return JsonResult(false, $"Error generating typed field: {ex.Message}");
        }
    }


    private string GetProjectsFolder() =>
        _fileLocator?.ProjectsFolder ?? _promptsRoot!;

    private string GetBasisPrompt(GeneratorAgentBase agent)
    {
        if (!string.IsNullOrWhiteSpace(agent.BasisPrompt))
        {
            return agent.BasisPrompt;
        }

        var basePromptPath = Path.Combine(GetProjectsFolder(), "baseprompt.txt");
        return File.Exists(basePromptPath)
            ? File.ReadAllText(basePromptPath)
            : string.Empty;
    }

    private Task<string> GetAvailableFieldsAsync(
        List<DevGPTChatMessage> messages,
        DevGPTChatToolCall toolCall,
        CancellationToken cancellationToken)
    {
        var fields = _availableFields.Select(f => new
        {
            key = f.Key,
            displayName = f.DisplayName,
            genericType = f.GenericType,
            typeSignature = f.TypeSignature
        }).ToArray();

        return Task.FromResult(JsonSerializer.Serialize(new { fields }));
    }

    private async Task<string> GetExistingFieldContentAsync(
        List<DevGPTChatMessage> messages,
        DevGPTChatToolCall toolCall,
        CancellationToken cancellationToken)
    {
        var key = GetStringParameter(toolCall, "key");

        if (string.IsNullOrWhiteSpace(key))
            return JsonResult(false, "Parameter 'key' is required.");

        var field = _availableFields.FirstOrDefault(f =>
            f.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

        if (field == null)
            return JsonResult(false, $"Unknown analysis field key: {key}");

        // Try to read existing content
        if (_fileLocator is not null)
        {
            var locator = _fileLocator; // Capture to non-nullable local
            var filePath = locator.GetPath(_projectId, field.File);
            if (File.Exists(filePath))
            {
                var content = await File.ReadAllTextAsync(filePath, cancellationToken);
                return JsonSerializer.Serialize(new
                {
                    exists = true,
                    key,
                    content,
                    genericType = field.GenericType,
                    typeSignature = field.TypeSignature
                });
            }
        }

        return JsonSerializer.Serialize(new
        {
            exists = false,
            key,
            genericType = field.GenericType,
            typeSignature = field.TypeSignature
        });
    }

    private async Task PersistToChatFileAsync(string key, string displayName, string content, string? reasoning, string? componentName = null)
    {
        try
        {
            if (_fileLocator is null) return; // Safety check
            var locator = _fileLocator; // Capture to non-nullable local
            var chatFile = string.IsNullOrWhiteSpace(_userId)
                ? locator.GetChatFile(_projectId, _chatId)
                : locator.GetChatFile(_projectId, _chatId, _userId);

            SerializableList<ConversationMessage> messages;
            if (File.Exists(chatFile))
            {
                var json = await File.ReadAllTextAsync(chatFile);
                messages = SerializableList<ConversationMessage>.Deserialize(json);
            }
            else
            {
                messages = new SerializableList<ConversationMessage>();
            }

            // Use field-specific component if provided, otherwise default to AnalysisData
            // For chat messages, use the "Chat" variant if available (e.g., ToneOfVoiceChat, CoreValuesChat)
            var resolvedComponentName = "view/analysis/AnalysisData";
            if (!string.IsNullOrWhiteSpace(componentName))
            {
                // Append "Chat" for dedicated chat components
                var chatComponentName = componentName.EndsWith("Chat") ? componentName : $"{componentName}Chat";
                resolvedComponentName = $"view/analysis/{chatComponentName}";
            }

            var payload = new
            {
                type = "analysis-data",
                componentName = resolvedComponentName,
                key,
                title = displayName,
                content,
                feedback = reasoning
            };

            var message = new ConversationMessage
            {
                Role = ChatMessageRole.Assistant,
                Text = $"I will generate the {displayName}",
                Payload = payload
            };
            messages.Add(message);

            await File.WriteAllTextAsync(chatFile, messages.Serialize());
        }
        catch
        {
            // Best effort
        }
    }

    private static ChatToolParameter CreateParameter(string name, string description, string type, bool required)
        => new() { Name = name, Description = description, Type = type, Required = required };

    private static string GetStringParameter(DevGPTChatToolCall toolCall, string name)
    {
        try
        {
            using var doc = JsonDocument.Parse(toolCall.FunctionArguments);
            if (doc.RootElement.TryGetProperty(name, out var prop))
            {
                // If the property is an object or array, serialize it to JSON string
                if (prop.ValueKind == JsonValueKind.Object || prop.ValueKind == JsonValueKind.Array)
                {
                    return prop.GetRawText();
                }
                return prop.GetString() ?? string.Empty;
            }
        }
        catch { }
        return string.Empty;
    }

    private static string JsonResult(bool success, string message) =>
        JsonSerializer.Serialize(new { success, message });

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
}
