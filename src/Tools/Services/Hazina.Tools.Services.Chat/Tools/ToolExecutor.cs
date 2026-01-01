using Hazina.Tools.Services.DataGathering.Services;
using Hazina.Tools.Services.DataGathering.Abstractions;
using Hazina.Tools.AI.Agents;
using Hazina.Tools.Data;
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

        public ToolExecutor(
            ILogger<ToolExecutor> logger,
            Func<IDataGatheringService> dataGatheringServiceFactory,
            Func<IAnalysisFieldService> analysisFieldServiceFactory,
            Func<ChatService> chatServiceFactory,
            Func<ProjectsRepository> projectsRepositoryFactory = null,
            Func<ProjectChatRepository> chatRepositoryFactory = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dataGatheringServiceFactory = dataGatheringServiceFactory ?? throw new ArgumentNullException(nameof(dataGatheringServiceFactory));
            _analysisFieldServiceFactory = analysisFieldServiceFactory ?? throw new ArgumentNullException(nameof(analysisFieldServiceFactory));
            _chatServiceFactory = chatServiceFactory ?? throw new ArgumentNullException(nameof(chatServiceFactory));
            _projectsRepositoryFactory = projectsRepositoryFactory;
            _chatRepositoryFactory = chatRepositoryFactory;
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
                }
            };
        }

        private async Task<IToolResult> ExecuteGatherDataAsync(
            string argumentsJson,
            string context,
            CancellationToken cancellationToken)
        {
            var args = JsonSerializer.Deserialize<GatherDataArgs>(argumentsJson);
            if (args == null)
            {
                return new ToolResult { Success = false, Error = "Invalid arguments", TokensUsed = 0 };
            }

            try
            {
                // TODO: Implement actual data gathering
                _logger.LogInformation("Gathering data: {DataType} - {Key}", args.DataType, args.Key);

                return new ToolResult
                {
                    Success = true,
                    Result = new { message = "Data gathered successfully", key = args.Key },
                    TokensUsed = EstimateTokens(argumentsJson)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error gathering data");
                return new ToolResult { Success = false, Error = ex.Message, TokensUsed = 0 };
            }
        }

        private async Task<IToolResult> ExecuteAnalyzeFieldAsync(
            string argumentsJson,
            string context,
            CancellationToken cancellationToken)
        {
            var args = JsonSerializer.Deserialize<AnalyzeFieldArgs>(argumentsJson);
            if (args == null)
            {
                return new ToolResult { Success = false, Error = "Invalid arguments", TokensUsed = 0 };
            }

            try
            {
                // TODO: Implement actual field analysis
                _logger.LogInformation("Analyzing field: {FieldKey}", args.FieldKey);

                return new ToolResult
                {
                    Success = true,
                    Result = new { message = "Field analyzed successfully", fieldKey = args.FieldKey },
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
            var args = JsonSerializer.Deserialize<GenerateImageArgs>(argumentsJson);
            if (args == null)
            {
                return new ToolResult { Success = false, Error = "Invalid arguments", TokensUsed = 0 };
            }

            try
            {
                // TODO: Implement actual image generation
                _logger.LogInformation("Generating image: {ImageType}", args.ImageType);

                return new ToolResult
                {
                    Success = true,
                    Result = new { message = "Image generation started", imageType = args.ImageType },
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
    }
}
