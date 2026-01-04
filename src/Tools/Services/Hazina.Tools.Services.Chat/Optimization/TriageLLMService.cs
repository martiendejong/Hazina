using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hazina.Tools.Models;
using Hazina.Tools.AI.Agents;
using Microsoft.Extensions.Options;

namespace Hazina.Tools.Services.Chat.Optimization
{
    /// <summary>
    /// Handles minimal triage LLM calls to classify requests without full context.
    /// Uses a 500-token budget to make quick decisions about request handling.
    /// </summary>
    public class TriageLLMService
    {
        private readonly GeneratorAgentBase _agent;
        private readonly LLMOptimizationOptions _options;

        public TriageLLMService(
            GeneratorAgentBase agent,
            IOptions<LLMOptimizationOptions> options)
        {
            _agent = agent ?? throw new ArgumentNullException(nameof(agent));
            _options = options?.Value ?? new LLMOptimizationOptions();
        }

        /// <summary>
        /// Make a minimal triage call to determine if the request can be handled
        /// with targeted context or needs full exploration.
        /// </summary>
        /// <param name="userMessage">The user's message</param>
        /// <param name="recentHistory">Last 2-3 messages for context</param>
        /// <param name="availableFields">Fields available in cache</param>
        /// <param name="cancel">Cancellation token</param>
        /// <returns>Triage decision with recommended fields</returns>
        public async Task<TriageDecision> PerformTriageAsync(
            string userMessage,
            List<ConversationMessage> recentHistory,
            List<string> availableFields,
            CancellationToken cancel = default)
        {
            // Build minimal triage prompt
            var triagePrompt = BuildTriagePrompt(availableFields);

            // Build minimal message history (last 2 messages only)
            var messages = BuildMinimalHistory(userMessage, recentHistory);

            try
            {
                // Make triage call with strict token budget
                var generator = await _agent.GetGenerator(null, triagePrompt);

                var response = await generator.GetResponse(
                    userMessage,
                    cancel,
                    history: messages,
                    addRelevantDocuments: false,  // No RAG for triage
                    addFilesList: false,           // No file list for triage
                    toolsContext: null,            // No tools for triage
                    images: null
                );

                // Parse the triage response
                var decision = ParseTriageResponse(response?.Result ?? string.Empty, availableFields);

                // Track token usage if available
                if (response?.TokenUsage != null)
                {
                    decision.TokensUsed = response.TokenUsage.TotalTokens;
                }

                return decision;
            }
            catch (Exception ex)
            {
                // On error, default to full exploration for safety
                return new TriageDecision
                {
                    NeedsFullContext = true,
                    Reason = $"Triage failed: {ex.Message}",
                    RequestedFields = new List<string>()
                };
            }
        }

        private string BuildTriagePrompt(List<string> availableFields)
        {
            var fieldsText = availableFields.Count > 0
                ? $"\n\nAvailable project data fields: {string.Join(", ", availableFields)}"
                : "\n\nNo project data currently available.";

            return $@"You are a triage assistant. Your job is to quickly classify user requests.

Analyze the user's message and respond with ONE of the following:

1. SIMPLE_ANSWER - If you can answer immediately without any project data
2. DATA_NEEDED: field1, field2 - If you need specific project data (list the field names)
3. FULL_CONTEXT - If you need to explore the full project context{fieldsText}

Examples:
- ""what's my brand name?"" → DATA_NEEDED: brand-profile
- ""generate a logo"" → DATA_NEEDED: brand-profile, color-scheme, target-audience
- ""how do I improve my marketing strategy?"" → FULL_CONTEXT
- ""hello"" → SIMPLE_ANSWER

Respond with ONLY the classification (e.g., ""DATA_NEEDED: brand-profile, tagline"").
Do not explain or add commentary.";
        }

        private List<HazinaChatMessage> BuildMinimalHistory(
            string userMessage,
            List<ConversationMessage> recentHistory)
        {
            var messages = new List<HazinaChatMessage>();

            // Include only last 2 messages for context (to stay under 500 tokens)
            if (recentHistory != null && recentHistory.Count > 0)
            {
                var lastTwo = recentHistory
                    .Skip(Math.Max(0, recentHistory.Count - 2))
                    .ToList();

                foreach (var msg in lastTwo)
                {
                    messages.Add(msg.ToChatMessage());
                }
            }

            return messages;
        }

        private TriageDecision ParseTriageResponse(string response, List<string> availableFields)
        {
            var responseLower = response.ToLower().Trim();

            // Check for SIMPLE_ANSWER
            if (responseLower.Contains("simple_answer") || responseLower.StartsWith("simple_answer"))
            {
                return new TriageDecision
                {
                    NeedsFullContext = false,
                    CanAnswerImmediately = true,
                    Reason = "Can answer without project data",
                    RequestedFields = new List<string>()
                };
            }

            // Check for FULL_CONTEXT
            if (responseLower.Contains("full_context") || responseLower.StartsWith("full_context"))
            {
                return new TriageDecision
                {
                    NeedsFullContext = true,
                    Reason = "Requires full project exploration",
                    RequestedFields = new List<string>()
                };
            }

            // Check for DATA_NEEDED
            if (responseLower.Contains("data_needed"))
            {
                var fields = ExtractRequestedFields(response);

                // Validate that requested fields exist
                var validFields = fields
                    .Where(f => availableFields.Contains(f, StringComparer.OrdinalIgnoreCase))
                    .ToList();

                return new TriageDecision
                {
                    NeedsFullContext = false,
                    RequestedFields = validFields,
                    Reason = $"Needs specific fields: {string.Join(", ", validFields)}"
                };
            }

            // Default to full context if we can't parse
            return new TriageDecision
            {
                NeedsFullContext = true,
                Reason = "Unable to parse triage response",
                RequestedFields = new List<string>()
            };
        }

        private List<string> ExtractRequestedFields(string response)
        {
            var fields = new List<string>();

            // Extract fields after "DATA_NEEDED:" or "data_needed:"
            var parts = response.Split(new[] { "DATA_NEEDED:", "data_needed:" }, StringSplitOptions.None);
            if (parts.Length > 1)
            {
                var fieldsText = parts[1].Trim();

                // Split by comma and clean up
                var fieldNames = fieldsText.Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var field in fieldNames)
                {
                    var cleaned = field.Trim().ToLower();
                    if (!string.IsNullOrWhiteSpace(cleaned))
                    {
                        fields.Add(cleaned);
                    }
                }
            }

            return fields;
        }
    }

    /// <summary>
    /// Result of a triage LLM call.
    /// </summary>
    public class TriageDecision
    {
        /// <summary>
        /// Whether the request requires full context exploration.
        /// </summary>
        public bool NeedsFullContext { get; set; }

        /// <summary>
        /// Whether the request can be answered immediately without any data.
        /// </summary>
        public bool CanAnswerImmediately { get; set; }

        /// <summary>
        /// Specific data fields needed to answer the request.
        /// </summary>
        public List<string> RequestedFields { get; set; } = new();

        /// <summary>
        /// Reason for the triage decision.
        /// </summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary>
        /// Tokens used for this triage call.
        /// </summary>
        public int TokensUsed { get; set; }
    }
}
