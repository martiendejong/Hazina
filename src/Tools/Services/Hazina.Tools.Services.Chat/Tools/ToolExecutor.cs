using DevGPT.GenerationTools.Services.DataGathering.Services;
using DevGPT.GenerationTools.Services.DataGathering.Abstractions;
using DevGPT.GenerationTools.AI.Agents;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DevGPT.GenerationTools.Services.Chat.Tools
{
    /// <summary>
    /// Executes tool calls from the LLM by routing to appropriate services.
    /// Implements the generic IToolExecutor interface from DevGPT.
    /// </summary>
    public class ToolExecutor : IToolExecutor
    {
        private readonly ILogger<ToolExecutor> _logger;
        private readonly Func<IDataGatheringService> _dataGatheringServiceFactory;
        private readonly Func<IAnalysisFieldService> _analysisFieldServiceFactory;
        private readonly Func<ChatService> _chatServiceFactory;

        public ToolExecutor(
            ILogger<ToolExecutor> logger,
            Func<IDataGatheringService> dataGatheringServiceFactory,
            Func<IAnalysisFieldService> analysisFieldServiceFactory,
            Func<ChatService> chatServiceFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dataGatheringServiceFactory = dataGatheringServiceFactory ?? throw new ArgumentNullException(nameof(dataGatheringServiceFactory));
            _analysisFieldServiceFactory = analysisFieldServiceFactory ?? throw new ArgumentNullException(nameof(analysisFieldServiceFactory));
            _chatServiceFactory = chatServiceFactory ?? throw new ArgumentNullException(nameof(chatServiceFactory));
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
    }
}
