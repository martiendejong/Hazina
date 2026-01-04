using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hazina.Tools.Models;
using Microsoft.Extensions.Options;

namespace Hazina.Tools.Services.Chat.Optimization
{
    /// <summary>
    /// Builds context progressively based on request strategy.
    /// Uses different prompt templates and context inclusion strategies per stage.
    /// </summary>
    public class SmartContextBuilder
    {
        private readonly ProjectLocalCache _cache;
        private readonly LLMOptimizationOptions _options;

        public SmartContextBuilder(
            ProjectLocalCache cache,
            IOptions<LLMOptimizationOptions> options)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _options = options?.Value ?? new LLMOptimizationOptions();
        }

        /// <summary>
        /// Build context based on the request strategy.
        /// </summary>
        public async Task<ContextBuildResult> BuildContextAsync(
            RequestStrategy strategy,
            string projectId,
            List<ConversationMessage> recentHistory,
            string? customPrompt = null)
        {
            return strategy switch
            {
                ImmediateResponseStrategy immediate => BuildImmediateContext(immediate),
                LocalDataResponseStrategy localData => BuildLocalDataContext(localData),
                TargetedRetrievalStrategy targeted => await BuildTargetedContextAsync(targeted, projectId, recentHistory, customPrompt),
                FullExplorationStrategy full => await BuildFullContextAsync(full, projectId, recentHistory, customPrompt),
                _ => await BuildFullContextAsync(new FullExplorationStrategy { Reason = "Unknown strategy" }, projectId, recentHistory, customPrompt)
            };
        }

        private ContextBuildResult BuildImmediateContext(ImmediateResponseStrategy strategy)
        {
            // No LLM call needed - return empty context with immediate response
            return new ContextBuildResult
            {
                SystemPrompt = string.Empty,
                MessageHistory = new List<HazinaChatMessage>(),
                IncludeRAGDocuments = false,
                IncludeFilesList = false,
                IncludeTools = false,
                ImmediateResponse = strategy.Response,
                EstimatedTokens = 0,
                Stage = "immediate"
            };
        }

        private ContextBuildResult BuildLocalDataContext(LocalDataResponseStrategy strategy)
        {
            // Format the cached data as response - no LLM call needed
            var response = FormatLocalDataResponse(strategy.Data);

            return new ContextBuildResult
            {
                SystemPrompt = string.Empty,
                MessageHistory = new List<HazinaChatMessage>(),
                IncludeRAGDocuments = false,
                IncludeFilesList = false,
                IncludeTools = false,
                ImmediateResponse = response,
                EstimatedTokens = 0,
                Stage = "local-data"
            };
        }

        private async Task<ContextBuildResult> BuildTargetedContextAsync(
            TargetedRetrievalStrategy strategy,
            string projectId,
            List<ConversationMessage> recentHistory,
            string? customPrompt)
        {
            var result = new ContextBuildResult
            {
                Stage = "targeted",
                EstimatedTokens = _options.StageTokenBudgets.Targeted
            };

            // Build task-specific system prompt
            result.SystemPrompt = BuildTargetedPrompt(strategy.TaskType, customPrompt);

            // Include recent message history (last 5 messages to stay under budget)
            result.MessageHistory = BuildRecentHistory(recentHistory, maxMessages: 5);

            // Load requested fields from cache
            if (strategy.RequestedFields.Any())
            {
                var dataResult = await _cache.GetDataAsync(projectId, strategy.RequestedFields);
                result.ProjectData = dataResult.FoundFields;
            }

            // Targeted retrieval uses limited tools based on task type
            result.IncludeTools = strategy.TaskType switch
            {
                TaskType.ContentGeneration => true,  // Need StoreAnalysisField, GenerateImage
                TaskType.DataExtraction => true,      // Need StoreGatheredData
                TaskType.DataRetrieval => false,      // Just return data
                TaskType.Question => false,           // Just answer question
                _ => false
            };

            // No RAG documents for targeted retrieval (using specific cached fields instead)
            result.IncludeRAGDocuments = false;
            result.IncludeFilesList = false;

            return result;
        }

        private async Task<ContextBuildResult> BuildFullContextAsync(
            FullExplorationStrategy strategy,
            string projectId,
            List<ConversationMessage> recentHistory,
            string? customPrompt)
        {
            var result = new ContextBuildResult
            {
                Stage = "full",
                EstimatedTokens = _options.StageTokenBudgets.Full
            };

            // Use full system prompt
            result.SystemPrompt = customPrompt ?? GetDefaultFullPrompt();

            // Include full message history (up to 10 messages)
            result.MessageHistory = BuildRecentHistory(recentHistory, maxMessages: 10);

            // Load all available project data
            var cachedData = await _cache.GetOrLoadAsync(projectId);
            result.ProjectData = cachedData.AnalysisFields.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value
            );

            // Include everything for full exploration
            result.IncludeRAGDocuments = true;
            result.IncludeFilesList = true;
            result.IncludeTools = true;

            return result;
        }

        private string BuildTargetedPrompt(TaskType taskType, string? customPrompt)
        {
            // If custom prompt provided, use it
            if (!string.IsNullOrWhiteSpace(customPrompt))
                return customPrompt;

            // Otherwise use task-specific prompt templates
            return taskType switch
            {
                TaskType.ContentGeneration => PromptTemplates.ContentGenerationPrompt,
                TaskType.DataExtraction => PromptTemplates.DataExtractionPrompt,
                TaskType.DataRetrieval => PromptTemplates.DataRetrievalPrompt,
                TaskType.Question => PromptTemplates.QuestionAnsweringPrompt,
                _ => PromptTemplates.DefaultTargetedPrompt
            };
        }

        private List<HazinaChatMessage> BuildRecentHistory(
            List<ConversationMessage> history,
            int maxMessages)
        {
            var messages = new List<HazinaChatMessage>();

            if (history == null || history.Count == 0)
                return messages;

            // Take last N messages
            var recent = history
                .Skip(Math.Max(0, history.Count - maxMessages))
                .ToList();

            foreach (var msg in recent)
            {
                messages.Add(msg.ToChatMessage());
            }

            return messages;
        }

        private string FormatLocalDataResponse(Dictionary<string, string> data)
        {
            if (data.Count == 0)
                return "No data found.";

            if (data.Count == 1)
            {
                var item = data.First();
                return $"**{FormatFieldName(item.Key)}**: {item.Value}";
            }

            // Multiple fields - format as list
            var sb = new StringBuilder();
            sb.AppendLine("Here's the information you requested:\n");

            foreach (var kvp in data)
            {
                sb.AppendLine($"**{FormatFieldName(kvp.Key)}**: {kvp.Value}");
            }

            return sb.ToString().TrimEnd();
        }

        private string FormatFieldName(string fieldKey)
        {
            // Convert field keys to readable names
            return fieldKey switch
            {
                "brand-profile" => "Brand Profile",
                "color-scheme" => "Color Scheme",
                "target-audience" => "Target Audience",
                "tagline" => "Tagline",
                "tone-of-voice" => "Tone of Voice",
                "narrative" => "Brand Narrative",
                _ => fieldKey.Replace("-", " ").Replace("_", " ")
            };
        }

        private string GetDefaultFullPrompt()
        {
            return @"You are a helpful marketing assistant with access to the project's complete context.
You can help with branding, content creation, strategy, and general marketing questions.

When generating content:
- Use the UpdateAnalysisField tool to save generated brand elements
- Use the StoreGatheredData tool to save user-provided information
- Use the GenerateImage tool to create visual assets

Be concise, helpful, and professional in your responses.";
        }
    }

    /// <summary>
    /// Result of building context for a request.
    /// </summary>
    public class ContextBuildResult
    {
        /// <summary>
        /// System prompt to use for this request.
        /// </summary>
        public string SystemPrompt { get; set; } = string.Empty;

        /// <summary>
        /// Message history to include.
        /// </summary>
        public List<HazinaChatMessage> MessageHistory { get; set; } = new();

        /// <summary>
        /// Project data fields to include in context.
        /// </summary>
        public Dictionary<string, string> ProjectData { get; set; } = new();

        /// <summary>
        /// Whether to include RAG-retrieved documents.
        /// </summary>
        public bool IncludeRAGDocuments { get; set; }

        /// <summary>
        /// Whether to include file list.
        /// </summary>
        public bool IncludeFilesList { get; set; }

        /// <summary>
        /// Whether to include tool definitions.
        /// </summary>
        public bool IncludeTools { get; set; }

        /// <summary>
        /// Immediate response (if no LLM call needed).
        /// </summary>
        public string? ImmediateResponse { get; set; }

        /// <summary>
        /// Estimated token count for this context.
        /// </summary>
        public int EstimatedTokens { get; set; }

        /// <summary>
        /// Stage name for logging.
        /// </summary>
        public string Stage { get; set; } = string.Empty;
    }

    /// <summary>
    /// Prompt templates for different request types.
    /// </summary>
    public static class PromptTemplates
    {
        public const string ContentGenerationPrompt = @"You are a creative marketing assistant focused on generating brand content.

Your task is to create compelling, professional content based on the provided brand information.

When you generate content:
1. Use the brand profile, target audience, and tone of voice
2. Ensure consistency with existing brand elements
3. Save your work using the UpdateAnalysisField tool

Be creative but professional.";

        public const string DataExtractionPrompt = @"You are a data extraction assistant.

Your task is to extract structured information from the user's message and store it appropriately.

When extracting data:
1. Identify key facts about the business, brand, or project
2. Categorize information appropriately (brand profile, target audience, etc.)
3. Use the StoreGatheredData tool to save extracted information
4. Confirm what you've stored with the user

Be thorough but concise.";

        public const string DataRetrievalPrompt = @"You are a helpful assistant providing project information.

Your task is to clearly present the requested information to the user.

Format your response:
1. Present the data clearly and concisely
2. Use formatting to improve readability
3. Offer to provide more details if needed

Be clear and informative.";

        public const string QuestionAnsweringPrompt = @"You are a helpful assistant answering questions about the project.

Your task is to provide accurate, helpful answers based on the available context.

When answering:
1. Base your answer on the provided context
2. Be concise and direct
3. If you're not sure, say so
4. Offer to help with related questions

Be helpful and honest.";

        public const string DefaultTargetedPrompt = @"You are a helpful marketing assistant.

Provide clear, concise, and professional responses to user requests.

Use available tools when appropriate to help the user achieve their goals.";
    }
}
