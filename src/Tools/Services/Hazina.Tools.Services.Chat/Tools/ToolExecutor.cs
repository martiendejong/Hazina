using Hazina.Tools.Services.DataGathering.Services;
using Hazina.Tools.Services.DataGathering.Abstractions;
using Hazina.Tools.Services.DataGathering.Models;
using Hazina.Tools.AI.Agents;
using Hazina.Tools.Data;
using Hazina.Tools.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Hazina.Tools.Services.Chat.Tools
{
    /// <summary>
    /// Context data passed to tool execution containing project/chat identifiers.
    /// STAP 28: Structured context for tool-chat integration.
    /// </summary>
    public class ToolExecutionContext
    {
        public string ProjectId { get; set; }
        public string ChatId { get; set; }
        public string UserId { get; set; }

        /// <summary>
        /// Parse context from JSON string.
        /// </summary>
        public static ToolExecutionContext Parse(string contextJson)
        {
            if (string.IsNullOrWhiteSpace(contextJson))
                return new ToolExecutionContext();

            try
            {
                return JsonSerializer.Deserialize<ToolExecutionContext>(contextJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new ToolExecutionContext();
            }
            catch
            {
                return new ToolExecutionContext();
            }
        }

        /// <summary>
        /// Create JSON string from context.
        /// </summary>
        public string ToJson()
        {
            return JsonSerializer.Serialize(this);
        }
    }

    /// <summary>
    /// Executes tool calls from the LLM by routing to appropriate services.
    /// Implements the generic IToolExecutor interface from Hazina.
    /// </summary>
    public class ToolExecutor : IToolExecutor
    {
        private readonly ILogger<ToolExecutor> _logger;
        private readonly Func<IDataGatheringService> _dataGatheringServiceFactory;
        private readonly Func<IAnalysisFieldService> _analysisFieldServiceFactory;
        private readonly Func<ChatService> _chatServiceFactory;
        private readonly Func<ProjectsRepository> _projectsRepositoryFactory;
        private readonly Func<ProjectChatRepository> _chatRepositoryFactory;
        // STAP 28: Add notifier for component requests
        private readonly IProjectChatNotifier _notifier;

        public ToolExecutor(
            ILogger<ToolExecutor> logger,
            Func<IDataGatheringService> dataGatheringServiceFactory,
            Func<IAnalysisFieldService> analysisFieldServiceFactory,
            Func<ChatService> chatServiceFactory,
            Func<ProjectsRepository> projectsRepositoryFactory = null,
            Func<ProjectChatRepository> chatRepositoryFactory = null,
            IProjectChatNotifier notifier = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dataGatheringServiceFactory = dataGatheringServiceFactory ?? throw new ArgumentNullException(nameof(dataGatheringServiceFactory));
            _analysisFieldServiceFactory = analysisFieldServiceFactory ?? throw new ArgumentNullException(nameof(analysisFieldServiceFactory));
            _chatServiceFactory = chatServiceFactory ?? throw new ArgumentNullException(nameof(chatServiceFactory));
            _projectsRepositoryFactory = projectsRepositoryFactory;
            _chatRepositoryFactory = chatRepositoryFactory;
            _notifier = notifier; // Can be null for unit testing
        }

        public async Task<IToolResult> ExecuteAsync(
            string toolName,
            string argumentsJson,
            string context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Executing tool: {ToolName}", toolName);

                return toolName switch
                {
                    "gather_data" => await ExecuteGatherDataAsync(argumentsJson, context, cancellationToken),
                    "analyze_field" => await ExecuteAnalyzeFieldAsync(argumentsJson, context, cancellationToken),
                    "generate_image" => await ExecuteGenerateImageAsync(argumentsJson, context, cancellationToken),
                    "rename_project" => await ExecuteRenameProjectAsync(argumentsJson, context, cancellationToken),
                    "rename_chat" => await ExecuteRenameChatAsync(argumentsJson, context, cancellationToken),
                    // STAP 9-12: New interactive tools
                    "show_guidance_card" => await ExecuteShowGuidanceCardAsync(argumentsJson, context, cancellationToken),
                    "request_file_upload" => await ExecuteRequestFileUploadAsync(argumentsJson, context, cancellationToken),
                    "show_system_status" => await ExecuteShowSystemStatusAsync(argumentsJson, context, cancellationToken),
                    "show_artifact" => await ExecuteShowArtifactAsync(argumentsJson, context, cancellationToken),
                    _ => new ToolResult
                    {
                        Success = false,
                        Error = $"Unknown tool: {toolName}",
                        TokensUsed = 0
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing tool: {ToolName}", toolName);
                return new ToolResult
                {
                    Success = false,
                    Error = ex.Message,
                    TokensUsed = 0
                };
            }
        }

        public List<IToolDefinition> GetToolDefinitions()
        {
            return new List<IToolDefinition>
            {
                new ToolDefinition
                {
                    Name = "gather_data",
                    Description = "Extract and store structured data from the conversation. Use this when the user provides information about their brand, products, services, target audience, or any other structured information that should be saved for future reference.",
                    Parameters = JsonSerializer.Deserialize<JsonElement>(@"{
                        ""type"": ""object"",
                        ""properties"": {
                            ""data_type"": {
                                ""type"": ""string"",
                                ""enum"": [""brand_info"", ""product_info"", ""service_info"", ""audience_info"", ""other""],
                                ""description"": ""The category of data being gathered""
                            },
                            ""key"": {
                                ""type"": ""string"",
                                ""description"": ""A unique identifier for this data item""
                            },
                            ""value"": {
                                ""type"": ""string"",
                                ""description"": ""The actual data content to store""
                            }
                        },
                        ""required"": [""data_type"", ""key"", ""value""]
                    }")
                },
                new ToolDefinition
                {
                    Name = "analyze_field",
                    Description = "Generate or update an analysis field such as tone of voice, core values, color scheme, or other brand attributes. Use this when you need to create structured brand analysis content.",
                    Parameters = JsonSerializer.Deserialize<JsonElement>(@"{
                        ""type"": ""object"",
                        ""properties"": {
                            ""field_key"": {
                                ""type"": ""string"",
                                ""description"": ""The analysis field identifier (e.g., 'tone-of-voice', 'core-values')""
                            },
                            ""content_prompt"": {
                                ""type"": ""string"",
                                ""description"": ""Instructions for what to generate in this field""
                            }
                        },
                        ""required"": [""field_key"", ""content_prompt""]
                    }")
                },
                new ToolDefinition
                {
                    Name = "generate_image",
                    Description = "Generate an image (logo, hero image, etc.) based on project context and specific requirements. Use this when the user requests visual content creation.",
                    Parameters = JsonSerializer.Deserialize<JsonElement>(@"{
                        ""type"": ""object"",
                        ""properties"": {
                            ""image_type"": {
                                ""type"": ""string"",
                                ""enum"": [""logo"", ""hero"", ""product"", ""social"", ""other""],
                                ""description"": ""The type of image to generate""
                            },
                            ""prompt"": {
                                ""type"": ""string"",
                                ""description"": ""Detailed prompt for image generation""
                            }
                        },
                        ""required"": [""image_type"", ""prompt""]
                    }")
                },
                new ToolDefinition
                {
                    Name = "rename_project",
                    Description = "Rename the current project. Only use this when you have learned the actual brand/company name from the user, or when the user explicitly asks to rename the project. This tool will only succeed if the project has exactly one chat (the current one).",
                    Parameters = JsonSerializer.Deserialize<JsonElement>(@"{
                        ""type"": ""object"",
                        ""properties"": {
                            ""project_id"": {
                                ""type"": ""string"",
                                ""description"": ""The ID of the project to rename""
                            },
                            ""new_name"": {
                                ""type"": ""string"",
                                ""description"": ""The new name for the project (e.g., company name, brand name)""
                            },
                            ""reason"": {
                                ""type"": ""string"",
                                ""description"": ""Brief explanation of why the rename is needed (e.g., 'User provided company name', 'User requested rename')""
                            }
                        },
                        ""required"": [""project_id"", ""new_name"", ""reason""]
                    }")
                },
                new ToolDefinition
                {
                    Name = "rename_chat",
                    Description = "Rename the current chat conversation. Use this to give the chat a meaningful title based on the conversation topic, typically after the first few messages establish the context.",
                    Parameters = JsonSerializer.Deserialize<JsonElement>(@"{
                        ""type"": ""object"",
                        ""properties"": {
                            ""project_id"": {
                                ""type"": ""string"",
                                ""description"": ""The ID of the project containing the chat""
                            },
                            ""chat_id"": {
                                ""type"": ""string"",
                                ""description"": ""The ID of the chat to rename""
                            },
                            ""new_name"": {
                                ""type"": ""string"",
                                ""description"": ""The new title for the chat conversation""
                            }
                        },
                        ""required"": [""project_id"", ""chat_id"", ""new_name""]
                    }")
                },
                // STAP 9: Guidance Card Tool
                new ToolDefinition
                {
                    Name = "show_guidance_card",
                    Description = "Present a guidance question to the user with 2-4 options to choose from. Use this when you need user input to make a decision or when offering alternatives. The LLM will pause and wait for user response.",
                    Parameters = JsonSerializer.Deserialize<JsonElement>(@"{
                        ""type"": ""object"",
                        ""properties"": {
                            ""question_text"": {
                                ""type"": ""string"",
                                ""description"": ""The main question to ask the user""
                            },
                            ""helper_text"": {
                                ""type"": ""string"",
                                ""description"": ""Optional additional context or explanation""
                            },
                            ""options"": {
                                ""type"": ""array"",
                                ""description"": ""2-4 options for the user to choose from"",
                                ""items"": {
                                    ""type"": ""object"",
                                    ""properties"": {
                                        ""label"": { ""type"": ""string"" },
                                        ""value"": { ""type"": ""string"" },
                                        ""description"": { ""type"": ""string"" }
                                    },
                                    ""required"": [""label""]
                                },
                                ""minItems"": 2,
                                ""maxItems"": 4
                            },
                            ""allow_free_text"": {
                                ""type"": ""boolean"",
                                ""description"": ""Whether to allow user to provide free-form text instead of selecting an option""
                            }
                        },
                        ""required"": [""question_text"", ""options""]
                    }")
                },
                // STAP 10: File Upload Tool
                new ToolDefinition
                {
                    Name = "request_file_upload",
                    Description = "Request the user to upload a file. Use this when you need the user to provide a document, image, or other file. The LLM will pause and wait for the file upload.",
                    Parameters = JsonSerializer.Deserialize<JsonElement>(@"{
                        ""type"": ""object"",
                        ""properties"": {
                            ""title"": {
                                ""type"": ""string"",
                                ""description"": ""Title for the upload request""
                            },
                            ""accepted_mime_types"": {
                                ""type"": ""array"",
                                ""description"": ""Allowed MIME types (e.g., ['image/*', 'application/pdf'])"",
                                ""items"": { ""type"": ""string"" }
                            },
                            ""max_size_mb"": {
                                ""type"": ""number"",
                                ""description"": ""Maximum file size in megabytes""
                            }
                        },
                        ""required"": [""title""]
                    }")
                },
                // STAP 11: System Status Tool
                new ToolDefinition
                {
                    Name = "show_system_status",
                    Description = "Display a system status message to provide feedback about ongoing operations, completed tasks, or important information. Use this to keep the user informed about what's happening.",
                    Parameters = JsonSerializer.Deserialize<JsonElement>(@"{
                        ""type"": ""object"",
                        ""properties"": {
                            ""severity"": {
                                ""type"": ""string"",
                                ""enum"": [""info"", ""success"", ""warning"", ""error""],
                                ""description"": ""The severity level of the status message""
                            },
                            ""text"": {
                                ""type"": ""string"",
                                ""description"": ""The status message text""
                            },
                            ""link_to"": {
                                ""type"": ""string"",
                                ""description"": ""Optional link to a related resource or action""
                            }
                        },
                        ""required"": [""severity"", ""text""]
                    }")
                },
                // STAP 12: Show Artifact Tool
                new ToolDefinition
                {
                    Name = "show_artifact",
                    Description = "Display a generated artifact (document, image, color scheme, etc.) in the chat. Use this to present created content to the user with actions they can take.",
                    Parameters = JsonSerializer.Deserialize<JsonElement>(@"{
                        ""type"": ""object"",
                        ""properties"": {
                            ""artifact_type"": {
                                ""type"": ""string"",
                                ""enum"": [""document"", ""image"", ""color-scheme"", ""typography"", ""logo"", ""other""],
                                ""description"": ""The type of artifact being displayed""
                            },
                            ""title"": {
                                ""type"": ""string"",
                                ""description"": ""Title of the artifact""
                            },
                            ""summary"": {
                                ""type"": ""string"",
                                ""description"": ""Brief description of the artifact""
                            },
                            ""data"": {
                                ""type"": ""object"",
                                ""description"": ""The artifact data (structure depends on artifact_type)""
                            }
                        },
                        ""required"": [""artifact_type"", ""title"", ""data""]
                    }")
                }
            };
        }

        private async Task<IToolResult> ExecuteGatherDataAsync(
            string argumentsJson,
            string context,
            CancellationToken cancellationToken)
        {
            var args = JsonSerializer.Deserialize<GatherDataArgs>(argumentsJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            if (args == null)
            {
                return new ToolResult { Success = false, Error = "Invalid arguments", TokensUsed = 0 };
            }

            try
            {
                _logger.LogInformation("Gathering data: {DataType} - {Key}", args.DataType, args.Key);

                // Parse context to get project/chat IDs
                var ctx = ToolExecutionContext.Parse(context);
                if (string.IsNullOrEmpty(ctx.ProjectId))
                {
                    return new ToolResult { Success = false, Error = "Project ID is required in context", TokensUsed = 0 };
                }

                // Create the data item using the factory method
                var dataItem = GatheredDataItem.Create(
                    key: args.Key,
                    title: FormatKeyAsTitle(args.Key),
                    value: args.Value,
                    source: $"chat:{ctx.ChatId}"
                );

                // Store using the data gathering service
                var dataGatheringService = _dataGatheringServiceFactory();
                if (dataGatheringService == null)
                {
                    return new ToolResult
                    {
                        Success = false,
                        Error = "Data gathering service not available",
                        TokensUsed = EstimateTokens(argumentsJson)
                    };
                }

                var success = await dataGatheringService.StoreDataItemAsync(
                    ctx.ProjectId,
                    ctx.ChatId ?? string.Empty,
                    dataItem,
                    cancellationToken);

                if (!success)
                {
                    return new ToolResult
                    {
                        Success = false,
                        Error = "Failed to store data item",
                        TokensUsed = EstimateTokens(argumentsJson)
                    };
                }

                _logger.LogInformation("Data item stored successfully: {Key} in project {ProjectId}", args.Key, ctx.ProjectId);

                return new ToolResult
                {
                    Success = true,
                    Result = new { message = "Data gathered successfully", key = args.Key, dataType = args.DataType },
                    TokensUsed = EstimateTokens(argumentsJson)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error gathering data");
                return new ToolResult { Success = false, Error = ex.Message, TokensUsed = 0 };
            }
        }

        /// <summary>
        /// Converts a key like "brand-name" to a title like "Brand Name".
        /// </summary>
        private static string FormatKeyAsTitle(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return key ?? string.Empty;
            return string.Join(" ", key.Split('-', '_')
                .Where(word => !string.IsNullOrEmpty(word))
                .Select(word => char.ToUpper(word[0]) + word.Substring(1).ToLower()));
        }

        private async Task<IToolResult> ExecuteAnalyzeFieldAsync(
            string argumentsJson,
            string context,
            CancellationToken cancellationToken)
        {
            var args = JsonSerializer.Deserialize<AnalyzeFieldArgs>(argumentsJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            if (args == null)
            {
                return new ToolResult { Success = false, Error = "Invalid arguments", TokensUsed = 0 };
            }

            try
            {
                _logger.LogInformation("Analyzing field: {FieldKey}", args.FieldKey);

                // Parse context to get project/chat IDs
                var ctx = ToolExecutionContext.Parse(context);
                if (string.IsNullOrEmpty(ctx.ProjectId))
                {
                    return new ToolResult { Success = false, Error = "Project ID is required in context", TokensUsed = 0 };
                }

                // Get the analysis field service
                var analysisFieldService = _analysisFieldServiceFactory();
                if (analysisFieldService == null)
                {
                    return new ToolResult
                    {
                        Success = false,
                        Error = "Analysis field service not available",
                        TokensUsed = EstimateTokens(argumentsJson)
                    };
                }

                // First check if the field configuration exists
                var fieldConfigs = await analysisFieldService.LoadFieldConfigsAsync(ctx.ProjectId);
                if (!fieldConfigs.ContainsKey(args.FieldKey))
                {
                    _logger.LogWarning("Analysis field not found: {FieldKey}", args.FieldKey);
                    return new ToolResult
                    {
                        Success = false,
                        Error = $"Analysis field '{args.FieldKey}' not found. Available fields: {string.Join(", ", fieldConfigs.Keys)}",
                        TokensUsed = EstimateTokens(argumentsJson)
                    };
                }

                // Generate the field content using the typed field generator
                // This uses the configured GenericType and generation prompts
                var result = await analysisFieldService.GenerateTypedFieldAsync(
                    ctx.ProjectId,
                    ctx.ChatId ?? string.Empty,
                    args.FieldKey,
                    args.ContentPrompt,
                    ctx.UserId,
                    cancellationToken);

                if (result == null)
                {
                    // If typed generation fails, try saving direct content if provided
                    if (!string.IsNullOrEmpty(args.ContentPrompt))
                    {
                        var saved = await analysisFieldService.SaveFieldAsync(
                            ctx.ProjectId,
                            ctx.ChatId ?? string.Empty,
                            args.FieldKey,
                            args.ContentPrompt,
                            userId: ctx.UserId,
                            cancellationToken: cancellationToken);

                        if (saved)
                        {
                            _logger.LogInformation("Analysis field saved directly: {FieldKey}", args.FieldKey);
                            return new ToolResult
                            {
                                Success = true,
                                Result = new { message = "Field content saved", fieldKey = args.FieldKey },
                                TokensUsed = EstimateTokens(argumentsJson)
                            };
                        }
                    }

                    return new ToolResult
                    {
                        Success = false,
                        Error = "Failed to generate field content",
                        TokensUsed = EstimateTokens(argumentsJson)
                    };
                }

                _logger.LogInformation("Analysis field generated successfully: {FieldKey} in project {ProjectId}", args.FieldKey, ctx.ProjectId);

                return new ToolResult
                {
                    Success = true,
                    Result = new { message = "Field analyzed successfully", fieldKey = args.FieldKey, hasContent = true },
                    TokensUsed = EstimateTokens(argumentsJson)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing field");
                return new ToolResult { Success = false, Error = ex.Message, TokensUsed = 0 };
            }
        }

        private async Task<IToolResult> ExecuteGenerateImageAsync(
            string argumentsJson,
            string context,
            CancellationToken cancellationToken)
        {
            var args = JsonSerializer.Deserialize<GenerateImageArgs>(argumentsJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            if (args == null)
            {
                return new ToolResult { Success = false, Error = "Invalid arguments", TokensUsed = 0 };
            }

            try
            {
                _logger.LogInformation("Generating image: {ImageType} with prompt: {Prompt}", args.ImageType, args.Prompt);

                // Parse context to get project/chat IDs
                var ctx = ToolExecutionContext.Parse(context);
                if (string.IsNullOrEmpty(ctx.ProjectId))
                {
                    return new ToolResult { Success = false, Error = "Project ID is required in context", TokensUsed = 0 };
                }

                // Get the chat service for image generation
                var chatService = _chatServiceFactory();
                if (chatService == null)
                {
                    return new ToolResult
                    {
                        Success = false,
                        Error = "Chat service not available",
                        TokensUsed = EstimateTokens(argumentsJson)
                    };
                }

                // Get the project for context (needed by ChatService.GenerateImage)
                Project project = null;
                if (_projectsRepositoryFactory != null)
                {
                    var projectsRepo = _projectsRepositoryFactory();
                    project = projectsRepo.Load(ctx.ProjectId);
                }

                if (project == null)
                {
                    return new ToolResult
                    {
                        Success = false,
                        Error = "Project not found",
                        TokensUsed = EstimateTokens(argumentsJson)
                    };
                }

                // Build the full prompt with image type context
                var fullPrompt = args.ImageType switch
                {
                    "logo" => $"Generate a professional logo design: {args.Prompt}",
                    "hero" => $"Generate a hero/banner image: {args.Prompt}",
                    "product" => $"Generate a product image: {args.Prompt}",
                    "social" => $"Generate a social media image: {args.Prompt}",
                    _ => args.Prompt
                };

                // Determine if this is part of an image set (e.g., multiple logo options)
                var isImageSet = args.ImageType == "logo";

                // Generate the image using ChatService
                var result = await chatService.GenerateImage(
                    ctx.ProjectId,
                    ctx.ChatId ?? Guid.NewGuid().ToString(),
                    ctx.UserId,
                    project,
                    fullPrompt,
                    cancellationToken,
                    isImageSet);

                if (result == null)
                {
                    return new ToolResult
                    {
                        Success = false,
                        Error = "Image generation failed",
                        TokensUsed = EstimateTokens(argumentsJson)
                    };
                }

                // Get the most recent generated image info
                var generatedImages = chatService.GetGeneratedImages(ctx.ProjectId, ctx.UserId);
                var latestImage = generatedImages?.OrderByDescending(i => i.CreatedAt).FirstOrDefault();

                _logger.LogInformation("Image generated successfully: {ImageType} in project {ProjectId}", args.ImageType, ctx.ProjectId);

                return new ToolResult
                {
                    Success = true,
                    Result = new
                    {
                        message = "Image generated successfully",
                        imageType = args.ImageType,
                        imageUrl = latestImage?.SourceUrl,
                        imageId = latestImage?.Id
                    },
                    TokensUsed = EstimateTokens(argumentsJson)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating image");
                return new ToolResult { Success = false, Error = ex.Message, TokensUsed = 0 };
            }
        }

        private async Task<IToolResult> ExecuteRenameProjectAsync(
            string argumentsJson,
            string context,
            CancellationToken cancellationToken)
        {
            var args = JsonSerializer.Deserialize<RenameProjectArgs>(argumentsJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            if (args == null || string.IsNullOrWhiteSpace(args.ProjectId) || string.IsNullOrWhiteSpace(args.NewName))
            {
                return new ToolResult { Success = false, Error = "Invalid arguments: project_id and new_name are required", TokensUsed = 0 };
            }

            try
            {
                if (_projectsRepositoryFactory == null || _chatRepositoryFactory == null)
                {
                    return new ToolResult { Success = false, Error = "Project repository not available", TokensUsed = 0 };
                }

                var projectsRepo = _projectsRepositoryFactory();
                var chatRepo = _chatRepositoryFactory();

                // Check if project exists
                var project = projectsRepo.Load(args.ProjectId);
                if (project == null)
                {
                    return new ToolResult { Success = false, Error = $"Project not found: {args.ProjectId}", TokensUsed = 0 };
                }

                // Check chat count - only allow rename if there's exactly one chat
                var chats = chatRepo.GetChatMetaData(args.ProjectId);
                if (chats != null && chats.Count > 1)
                {
                    return new ToolResult
                    {
                        Success = false,
                        Error = $"Cannot rename project with multiple chats ({chats.Count} chats). Project name can only be changed when there is exactly one chat.",
                        TokensUsed = 0
                    };
                }

                // Perform the rename
                var oldName = project.Name;
                project.Name = args.NewName.Trim();
                projectsRepo.Save(project);

                _logger.LogInformation("Renamed project {ProjectId} from '{OldName}' to '{NewName}'. Reason: {Reason}",
                    args.ProjectId, oldName, args.NewName, args.Reason ?? "Not specified");

                return new ToolResult
                {
                    Success = true,
                    Result = new { message = $"Project renamed from '{oldName}' to '{args.NewName}'", projectId = args.ProjectId },
                    TokensUsed = EstimateTokens(argumentsJson)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error renaming project {ProjectId}", args.ProjectId);
                return new ToolResult { Success = false, Error = ex.Message, TokensUsed = 0 };
            }
        }

        private async Task<IToolResult> ExecuteRenameChatAsync(
            string argumentsJson,
            string context,
            CancellationToken cancellationToken)
        {
            var args = JsonSerializer.Deserialize<RenameChatArgs>(argumentsJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            if (args == null || string.IsNullOrWhiteSpace(args.ProjectId) ||
                string.IsNullOrWhiteSpace(args.ChatId) || string.IsNullOrWhiteSpace(args.NewName))
            {
                return new ToolResult { Success = false, Error = "Invalid arguments: project_id, chat_id, and new_name are required", TokensUsed = 0 };
            }

            try
            {
                if (_chatRepositoryFactory == null)
                {
                    return new ToolResult { Success = false, Error = "Chat repository not available", TokensUsed = 0 };
                }

                var chatRepo = _chatRepositoryFactory();
                var chats = chatRepo.GetChatMetaData(args.ProjectId);
                var chat = chats?.FirstOrDefault(c => c.Id == args.ChatId);

                if (chat == null)
                {
                    return new ToolResult { Success = false, Error = $"Chat not found: {args.ChatId}", TokensUsed = 0 };
                }

                var oldName = chat.Name;
                chat.Name = args.NewName.Trim();
                chatRepo.SaveChatMetaData(args.ProjectId, chats);

                _logger.LogInformation("Renamed chat {ChatId} in project {ProjectId} from '{OldName}' to '{NewName}'",
                    args.ChatId, args.ProjectId, oldName, args.NewName);

                return new ToolResult
                {
                    Success = true,
                    Result = new { message = $"Chat renamed from '{oldName}' to '{args.NewName}'", chatId = args.ChatId },
                    TokensUsed = EstimateTokens(argumentsJson)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error renaming chat {ChatId} in project {ProjectId}", args.ChatId, args.ProjectId);
                return new ToolResult { Success = false, Error = ex.Message, TokensUsed = 0 };
            }
        }

        // STAP 9 + STAP 28: Show Guidance Card implementation with SignalR notification
        private async Task<IToolResult> ExecuteShowGuidanceCardAsync(
            string argumentsJson,
            string context,
            CancellationToken cancellationToken)
        {
            var args = JsonSerializer.Deserialize<GuidanceCardArgs>(argumentsJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (args == null || string.IsNullOrWhiteSpace(args.QuestionText))
            {
                return new ToolResult { Success = false, Error = "Invalid arguments: question_text is required", TokensUsed = 0 };
            }

            // Validate options count (2-4 options required)
            if (args.Options == null || args.Options.Count < 2 || args.Options.Count > 4)
            {
                return new ToolResult
                {
                    Success = false,
                    Error = "Invalid arguments: options must contain 2-4 items",
                    TokensUsed = 0
                };
            }

            try
            {
                _logger.LogInformation("Showing guidance card: {QuestionText}", args.QuestionText);

                // STAP 28: Parse context and send SignalR notification
                var ctx = ToolExecutionContext.Parse(context);
                if (_notifier != null && !string.IsNullOrEmpty(ctx.ProjectId))
                {
                    var componentData = new
                    {
                        questionText = args.QuestionText,
                        helperText = args.HelperText,
                        options = args.Options?.Select(o => new
                        {
                            label = o.Label,
                            value = o.Value ?? o.Label,
                            description = o.Description
                        }).ToList(),
                        freeTextAllowed = args.AllowFreeText
                    };

                    await _notifier.NotifyComponentRequested(
                        ctx.ProjectId,
                        ctx.ChatId,
                        "chat/GuidanceQuestionCard",
                        componentData);

                    _logger.LogInformation(
                        "GuidanceCard notification sent to project {ProjectId}, chat {ChatId}",
                        ctx.ProjectId, ctx.ChatId);
                }

                return new ToolResult
                {
                    Success = true,
                    AwaitingUserResponse = true, // STAP 8: Signal that we're waiting for user
                    Result = new
                    {
                        message = "Guidance card displayed, awaiting user response",
                        questionText = args.QuestionText,
                        optionsCount = args.Options.Count
                    },
                    TokensUsed = EstimateTokens(argumentsJson)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error showing guidance card");
                return new ToolResult { Success = false, Error = ex.Message, TokensUsed = 0 };
            }
        }

        // STAP 10 + STAP 28: Request File Upload implementation with SignalR notification
        private async Task<IToolResult> ExecuteRequestFileUploadAsync(
            string argumentsJson,
            string context,
            CancellationToken cancellationToken)
        {
            var args = JsonSerializer.Deserialize<FileUploadArgs>(argumentsJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (args == null || string.IsNullOrWhiteSpace(args.Title))
            {
                return new ToolResult { Success = false, Error = "Invalid arguments: title is required", TokensUsed = 0 };
            }

            // Validate max size if provided
            if (args.MaxSizeMb.HasValue && args.MaxSizeMb.Value <= 0)
            {
                return new ToolResult
                {
                    Success = false,
                    Error = "Invalid arguments: max_size_mb must be greater than 0",
                    TokensUsed = 0
                };
            }

            try
            {
                _logger.LogInformation("Requesting file upload: {Title}", args.Title);

                // STAP 28: Parse context and send SignalR notification
                var ctx = ToolExecutionContext.Parse(context);
                if (_notifier != null && !string.IsNullOrEmpty(ctx.ProjectId))
                {
                    var componentData = new
                    {
                        intent = "upload",
                        spec = new
                        {
                            title = args.Title,
                            acceptedMimeTypes = args.AcceptedMimeTypes,
                            maxSizeMb = args.MaxSizeMb
                        }
                    };

                    await _notifier.NotifyComponentRequested(
                        ctx.ProjectId,
                        ctx.ChatId,
                        "chat/InputGuidanceCard",
                        componentData);

                    _logger.LogInformation(
                        "FileUpload notification sent to project {ProjectId}, chat {ChatId}",
                        ctx.ProjectId, ctx.ChatId);
                }

                return new ToolResult
                {
                    Success = true,
                    AwaitingUserResponse = true, // STAP 8: Signal that we're waiting for user
                    Result = new
                    {
                        message = "File upload request displayed, awaiting user upload",
                        title = args.Title,
                        acceptedMimeTypes = args.AcceptedMimeTypes
                    },
                    TokensUsed = EstimateTokens(argumentsJson)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error requesting file upload");
                return new ToolResult { Success = false, Error = ex.Message, TokensUsed = 0 };
            }
        }

        // STAP 11 + STAP 28: Show System Status implementation with SignalR notification
        private async Task<IToolResult> ExecuteShowSystemStatusAsync(
            string argumentsJson,
            string context,
            CancellationToken cancellationToken)
        {
            var args = JsonSerializer.Deserialize<SystemStatusArgs>(argumentsJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (args == null || string.IsNullOrWhiteSpace(args.Severity) || string.IsNullOrWhiteSpace(args.Text))
            {
                return new ToolResult { Success = false, Error = "Invalid arguments: severity and text are required", TokensUsed = 0 };
            }

            // Validate severity
            var validSeverities = new[] { "info", "success", "warning", "error" };
            if (!validSeverities.Contains(args.Severity.ToLower()))
            {
                return new ToolResult
                {
                    Success = false,
                    Error = $"Invalid severity: must be one of {string.Join(", ", validSeverities)}",
                    TokensUsed = 0
                };
            }

            try
            {
                _logger.LogInformation("Showing system status [{Severity}]: {Text}", args.Severity, args.Text);

                // STAP 28: Parse context and send SignalR notification
                var ctx = ToolExecutionContext.Parse(context);
                if (_notifier != null && !string.IsNullOrEmpty(ctx.ProjectId))
                {
                    var componentData = new
                    {
                        severity = args.Severity.ToLower(),
                        text = args.Text,
                        linkTo = args.LinkTo
                    };

                    await _notifier.NotifyComponentRequested(
                        ctx.ProjectId,
                        ctx.ChatId,
                        "chat/SystemStatusLine",
                        componentData);

                    _logger.LogInformation(
                        "SystemStatus notification sent to project {ProjectId}, chat {ChatId}",
                        ctx.ProjectId, ctx.ChatId);
                }

                // No AWAITING needed for status lines - they're informational only
                return new ToolResult
                {
                    Success = true,
                    Result = new
                    {
                        message = "System status displayed",
                        severity = args.Severity,
                        text = args.Text
                    },
                    TokensUsed = EstimateTokens(argumentsJson)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error showing system status");
                return new ToolResult { Success = false, Error = ex.Message, TokensUsed = 0 };
            }
        }

        // STAP 12 + STAP 28: Show Artifact implementation with SignalR notification
        private async Task<IToolResult> ExecuteShowArtifactAsync(
            string argumentsJson,
            string context,
            CancellationToken cancellationToken)
        {
            var args = JsonSerializer.Deserialize<ShowArtifactArgs>(argumentsJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (args == null || string.IsNullOrWhiteSpace(args.ArtifactType) ||
                string.IsNullOrWhiteSpace(args.Title) || args.Data.ValueKind == JsonValueKind.Undefined)
            {
                return new ToolResult { Success = false, Error = "Invalid arguments: artifact_type, title, and data are required", TokensUsed = 0 };
            }

            // Validate artifact type
            var validTypes = new[] { "document", "image", "color-scheme", "typography", "logo", "other" };
            if (!validTypes.Contains(args.ArtifactType.ToLower()))
            {
                return new ToolResult
                {
                    Success = false,
                    Error = $"Invalid artifact_type: must be one of {string.Join(", ", validTypes)}",
                    TokensUsed = 0
                };
            }

            try
            {
                _logger.LogInformation("Showing artifact [{ArtifactType}]: {Title}", args.ArtifactType, args.Title);

                // STAP 28: Parse context and send SignalR notification
                var ctx = ToolExecutionContext.Parse(context);
                if (_notifier != null && !string.IsNullOrEmpty(ctx.ProjectId))
                {
                    // Convert JsonElement to object for serialization
                    object dataObj = null;
                    if (args.Data.ValueKind != JsonValueKind.Undefined)
                    {
                        dataObj = JsonSerializer.Deserialize<object>(args.Data.GetRawText());
                    }

                    var componentData = new
                    {
                        artifactType = args.ArtifactType.ToLower(),
                        title = args.Title,
                        summary = args.Summary,
                        data = dataObj
                    };

                    await _notifier.NotifyComponentRequested(
                        ctx.ProjectId,
                        ctx.ChatId,
                        "chat/ArtifactCard",
                        componentData);

                    _logger.LogInformation(
                        "ArtifactCard notification sent to project {ProjectId}, chat {ChatId}",
                        ctx.ProjectId, ctx.ChatId);
                }

                // No AWAITING needed for artifacts - they're display only
                return new ToolResult
                {
                    Success = true,
                    Result = new
                    {
                        message = "Artifact displayed",
                        artifactType = args.ArtifactType,
                        title = args.Title
                    },
                    TokensUsed = EstimateTokens(argumentsJson)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error showing artifact");
                return new ToolResult { Success = false, Error = ex.Message, TokensUsed = 0 };
            }
        }

        private int EstimateTokens(string text)
        {
            return (text?.Length ?? 0) / 4; // Rough estimation: ~4 characters per token
        }

        // Tool argument classes
        private class GatherDataArgs
        {
            public string DataType { get; set; }
            public string Key { get; set; }
            public string Value { get; set; }
        }

        private class AnalyzeFieldArgs
        {
            public string FieldKey { get; set; }
            public string ContentPrompt { get; set; }
        }

        private class GenerateImageArgs
        {
            public string ImageType { get; set; }
            public string Prompt { get; set; }
        }

        private class RenameProjectArgs
        {
            public string ProjectId { get; set; }
            public string NewName { get; set; }
            public string Reason { get; set; }
        }

        private class RenameChatArgs
        {
            public string ProjectId { get; set; }
            public string ChatId { get; set; }
            public string NewName { get; set; }
        }

        // STAP 9-12: New tool argument classes
        private class GuidanceCardArgs
        {
            public string QuestionText { get; set; }
            public string HelperText { get; set; }
            public List<GuidanceOptionArg> Options { get; set; }
            public bool AllowFreeText { get; set; }
        }

        private class GuidanceOptionArg
        {
            public string Label { get; set; }
            public string Value { get; set; }
            public string Description { get; set; }
        }

        private class FileUploadArgs
        {
            public string Title { get; set; }
            public List<string> AcceptedMimeTypes { get; set; }
            public double? MaxSizeMb { get; set; }
        }

        private class SystemStatusArgs
        {
            public string Severity { get; set; }
            public string Text { get; set; }
            public string LinkTo { get; set; }
        }

        private class ShowArtifactArgs
        {
            public string ArtifactType { get; set; }
            public string Title { get; set; }
            public string Summary { get; set; }
            public JsonElement Data { get; set; }
        }
    }
}
