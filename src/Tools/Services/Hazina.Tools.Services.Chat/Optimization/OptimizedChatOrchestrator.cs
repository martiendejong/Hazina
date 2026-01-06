using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hazina.Tools.AI.Agents;
using Hazina.Tools.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hazina.Tools.Services.Chat.Optimization
{
    /// <summary>
    /// Orchestrates optimized chat request handling using progressive context loading.
    /// Coordinates RequestClassifier, TriageLLMService, and SmartContextBuilder
    /// to minimize token usage while maintaining response quality.
    /// </summary>
    public class OptimizedChatOrchestrator
    {
        private readonly RequestClassifier _classifier;
        private readonly TriageLLMService _triageService;
        private readonly SmartContextBuilder _contextBuilder;
        private readonly ProjectLocalCache _cache;
        private readonly GeneratorAgentBase _agent;
        private readonly LLMOptimizationOptions _options;
        private readonly ILogger<OptimizedChatOrchestrator> _logger;

        public OptimizedChatOrchestrator(
            RequestClassifier classifier,
            TriageLLMService triageService,
            SmartContextBuilder contextBuilder,
            ProjectLocalCache cache,
            GeneratorAgentBase agent,
            IOptions<LLMOptimizationOptions> options,
            ILogger<OptimizedChatOrchestrator> logger)
        {
            _classifier = classifier ?? throw new ArgumentNullException(nameof(classifier));
            _triageService = triageService ?? throw new ArgumentNullException(nameof(triageService));
            _contextBuilder = contextBuilder ?? throw new ArgumentNullException(nameof(contextBuilder));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _agent = agent ?? throw new ArgumentNullException(nameof(agent));
            _options = options?.Value ?? new LLMOptimizationOptions();
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Handle a chat request using optimized context loading.
        /// </summary>
        public async Task<OptimizedChatResponse> HandleRequestAsync(
            string userMessage,
            string projectId,
            List<ConversationMessage> recentHistory,
            string? customPrompt = null,
            CancellationToken cancel = default)
        {
            var response = new OptimizedChatResponse
            {
                StartTime = DateTime.UtcNow
            };

            try
            {
                // Check if optimization is enabled
                if (!_options.EnableSmartContext)
                {
                    _logger.LogInformation("Smart context optimization is disabled, using legacy flow");
                    response.UsedLegacyFlow = true;
                    return response;
                }

                // STAGE 1: Pattern-based classification (0 tokens)
                _logger.LogInformation("Stage 1: Classifying request using pattern matching");
                var strategy = await _classifier.ClassifyRequestAsync(userMessage, recentHistory, projectId);
                response.Strategy = strategy.StageName;
                response.TokensUsedClassification = strategy.TokensUsed;

                _logger.LogInformation($"Classification result: {strategy.StageName}");

                // Build context based on strategy
                var context = await _contextBuilder.BuildContextAsync(strategy, projectId, recentHistory, customPrompt);
                response.EstimatedContextTokens = context.EstimatedTokens;

                // Check for immediate responses
                if (!string.IsNullOrEmpty(context.ImmediateResponse))
                {
                    _logger.LogInformation("Returning immediate response (0 LLM tokens)");
                    response.Message = context.ImmediateResponse;
                    response.CompletedStage = "immediate";
                    response.Success = true;
                    response.TotalTokensUsed = 0;
                    return response;
                }

                // STAGE 2 or 3: Make LLM call with optimized context
                _logger.LogInformation($"Making LLM call with {context.Stage} context");

                var generator = await _agent.GetGenerator(null, context.SystemPrompt);

                // Prepare project data context if available
                string? projectDataContext = null;
                if (context.ProjectData.Any())
                {
                    projectDataContext = FormatProjectDataContext(context.ProjectData);
                }

                // Make the LLM call
                var llmResponse = await generator.GetResponse(
                    BuildUserMessageWithContext(userMessage, projectDataContext),
                    cancel,
                    history: context.MessageHistory,
                    addRelevantDocuments: context.IncludeRAGDocuments,
                    addFilesList: context.IncludeFilesList,
                    toolsContext: context.IncludeTools ? null : null, // TODO: Pass actual tools context
                    images: null
                );

                response.Message = llmResponse?.Result ?? string.Empty;
                response.CompletedStage = context.Stage;
                response.Success = true;

                // Extract token usage if available
                if (llmResponse?.TokenUsage != null)
                {
                    response.TokensUsedLLM = llmResponse.TokenUsage.TotalTokens;
                    response.TotalTokensUsed = response.TokensUsedClassification + response.TokensUsedLLM;
                }

                _logger.LogInformation($"Request completed successfully. Tokens used: {response.TotalTokensUsed}");

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling optimized chat request");
                response.Success = false;
                response.ErrorMessage = ex.Message;
                response.UsedLegacyFlow = true; // Fall back to legacy on error
                return response;
            }
            finally
            {
                response.EndTime = DateTime.UtcNow;
                response.DurationMs = (long)(response.EndTime - response.StartTime).TotalMilliseconds;
            }
        }

        /// <summary>
        /// Get optimization statistics for a project.
        /// </summary>
        public async Task<OptimizationStats> GetStatsAsync(string projectId)
        {
            var cachedData = await _cache.GetOrLoadAsync(projectId);

            return new OptimizationStats
            {
                ProjectId = projectId,
                CachedFieldsCount = cachedData.AnalysisFields.Count,
                CachedDataFieldsCount = cachedData.GatheredData.Count,
                AvailableFilesCount = cachedData.AvailableFiles.Count,
                CacheAge = DateTime.UtcNow - cachedData.CachedAt
            };
        }

        /// <summary>
        /// Invalidate cache for a project (call after updates).
        /// </summary>
        public void InvalidateCache(string projectId, string[]? changedFields = null)
        {
            _logger.LogInformation($"Invalidating cache for project {projectId}");
            _cache.Invalidate(projectId, changedFields);
        }

        private string FormatProjectDataContext(Dictionary<string, string> projectData)
        {
            if (projectData.Count == 0)
                return string.Empty;

            var lines = new List<string> { "**Available Project Data:**" };

            foreach (var kvp in projectData)
            {
                lines.Add($"- **{FormatFieldName(kvp.Key)}**: {kvp.Value}");
            }

            return string.Join("\n", lines);
        }

        private string BuildUserMessageWithContext(string userMessage, string? projectDataContext)
        {
            if (string.IsNullOrEmpty(projectDataContext))
                return userMessage;

            return $"{projectDataContext}\n\n**User Request:**\n{userMessage}";
        }

        private string FormatFieldName(string fieldKey)
        {
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
    }

    /// <summary>
    /// Response from optimized chat handling.
    /// </summary>
    public class OptimizedChatResponse
    {
        /// <summary>
        /// The generated response message.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Strategy used (immediate, local-data, targeted, full).
        /// </summary>
        public string Strategy { get; set; } = string.Empty;

        /// <summary>
        /// Stage completed (immediate, local-data, targeted, full).
        /// </summary>
        public string CompletedStage { get; set; } = string.Empty;

        /// <summary>
        /// Tokens used for classification (usually 0 for pattern matching).
        /// </summary>
        public int TokensUsedClassification { get; set; }

        /// <summary>
        /// Tokens used for LLM call.
        /// </summary>
        public int TokensUsedLLM { get; set; }

        /// <summary>
        /// Total tokens used.
        /// </summary>
        public int TotalTokensUsed { get; set; }

        /// <summary>
        /// Estimated tokens for context.
        /// </summary>
        public int EstimatedContextTokens { get; set; }

        /// <summary>
        /// Whether the legacy (non-optimized) flow was used.
        /// </summary>
        public bool UsedLegacyFlow { get; set; }

        /// <summary>
        /// Whether the request was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Error message if not successful.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Request start time.
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// Request end time.
        /// </summary>
        public DateTime EndTime { get; set; }

        /// <summary>
        /// Duration in milliseconds.
        /// </summary>
        public long DurationMs { get; set; }

        // STAP 15: Support for async user response pattern
        /// <summary>
        /// Indicates if the orchestration is paused awaiting user response.
        /// When true, the frontend should render interactive components and wait for user input.
        /// </summary>
        public bool AwaitingUserResponse { get; set; }

        /// <summary>
        /// The tool result data that triggered the await state (contains guidance/upload request data)
        /// </summary>
        public object? AwaitingToolData { get; set; }
    }

    /// <summary>
    /// Optimization statistics for a project.
    /// </summary>
    public class OptimizationStats
    {
        public string ProjectId { get; set; } = string.Empty;
        public int CachedFieldsCount { get; set; }
        public int CachedDataFieldsCount { get; set; }
        public int AvailableFilesCount { get; set; }
        public TimeSpan CacheAge { get; set; }
    }
}
