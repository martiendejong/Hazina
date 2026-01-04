using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Hazina.Tools.Models;
using Microsoft.Extensions.Options;

namespace Hazina.Tools.Services.Chat.Optimization
{
    /// <summary>
    /// Classifies user requests to determine optimal handling strategy.
    /// Uses pattern matching and local cache to avoid unnecessary LLM calls.
    /// </summary>
    public class RequestClassifier
    {
        private readonly ProjectLocalCache _cache;
        private readonly LLMOptimizationOptions _options;

        public RequestClassifier(
            ProjectLocalCache cache,
            IOptions<LLMOptimizationOptions> options)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _options = options?.Value ?? new LLMOptimizationOptions();
        }

        /// <summary>
        /// Classify a user request and determine the optimal handling strategy.
        /// </summary>
        public async Task<RequestStrategy> ClassifyRequestAsync(
            string userMessage,
            List<ConversationMessage> recentHistory,
            string projectId)
        {
            if (string.IsNullOrWhiteSpace(userMessage))
            {
                return new ImmediateResponseStrategy("I didn't receive a message. Could you please try again?");
            }

            var messageLower = userMessage.ToLower().Trim();

            // 1. Check for simple test messages (no LLM needed)
            if (IsSimpleTest(messageLower))
            {
                return new ImmediateResponseStrategy("Test received successfully. How can I help you?");
            }

            // 2. Check for greetings (no LLM needed)
            if (IsGreeting(messageLower))
            {
                return new ImmediateResponseStrategy(GenerateGreeting());
            }

            // 3. Check for content generation requests (higher priority than data retrieval)
            var generationType = DetectGenerationRequest(userMessage);
            if (generationType != null)
            {
                return new TargetedRetrievalStrategy
                {
                    TaskType = TaskType.ContentGeneration,
                    RequestedFields = GetFieldsForGeneration(generationType),
                    Requirements = $"Generate {generationType}"
                };
            }

            // 4. Check for data extraction/storage intent
            if (IsDataExtraction(userMessage, recentHistory))
            {
                return new TargetedRetrievalStrategy
                {
                    TaskType = TaskType.DataExtraction,
                    RequestedFields = new List<string>(), // Will be determined by LLM
                    Requirements = "Extract and store user-provided information"
                };
            }

            // 5. Check if user is asking about specific local data
            var localDataNeeds = IdentifyLocalDataNeeds(userMessage);
            if (localDataNeeds.Any())
            {
                var dataResult = await _cache.GetDataAsync(projectId, localDataNeeds);
                if (dataResult.AllFound)
                {
                    return new LocalDataResponseStrategy(dataResult.FoundFields);
                }

                // Some fields missing - use targeted retrieval
                return new TargetedRetrievalStrategy
                {
                    TaskType = TaskType.DataRetrieval,
                    RequestedFields = localDataNeeds,
                    Requirements = "User asked about specific project data"
                };
            }

            // 6. Check for simple questions about visible/recent context
            if (IsSimpleQuestion(userMessage) && HasRecentContext(recentHistory))
            {
                return new TargetedRetrievalStrategy
                {
                    TaskType = TaskType.Question,
                    RequestedFields = new List<string>(),
                    Requirements = "Answer question based on recent context"
                };
            }

            // 7. Default to full exploration for complex/unclear requests
            return new FullExplorationStrategy
            {
                Reason = "Complex or exploratory request requiring full context"
            };
        }

        private bool IsSimpleTest(string message)
        {
            return _options.ClassificationRules.SimpleTestPatterns
                .Any(pattern => message.Contains(pattern, StringComparison.OrdinalIgnoreCase));
        }

        private bool IsGreeting(string message)
        {
            return _options.ClassificationRules.GreetingPatterns
                .Any(pattern => message.StartsWith(pattern, StringComparison.OrdinalIgnoreCase));
        }

        private string GenerateGreeting()
        {
            var hour = DateTime.Now.Hour;
            var timeOfDay = hour < 12 ? "morning" : hour < 18 ? "afternoon" : "evening";
            return $"Good {timeOfDay}! How can I assist you with your project today?";
        }

        private List<string> IdentifyLocalDataNeeds(string message)
        {
            var needs = new List<string>();

            foreach (var (field, keywords) in _options.ClassificationRules.LocalDataFields)
            {
                if (keywords.Any(keyword => message.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                {
                    needs.Add(field);
                }
            }

            return needs;
        }

        private string? DetectGenerationRequest(string message)
        {
            var generationPatterns = new Dictionary<string, string[]>
            {
                ["logo"] = new[] { "generate logo", "create logo", "make logo", "logo genereren" },
                ["tagline"] = new[] { "generate tagline", "create tagline", "make slogan", "slogan genereren" },
                ["narrative"] = new[] { "generate narrative", "create story", "brand story", "verhaal genereren" },
                ["tone-of-voice"] = new[] { "generate tone", "communication style", "tone of voice genereren" },
                ["color-scheme"] = new[] { "generate colors", "color palette", "kleurenschema genereren" }
            };

            foreach (var (type, patterns) in generationPatterns)
            {
                if (patterns.Any(p => message.Contains(p, StringComparison.OrdinalIgnoreCase)))
                {
                    return type;
                }
            }

            return null;
        }

        private List<string> GetFieldsForGeneration(string generationType)
        {
            // Return fields that are typically needed for different generation types
            return generationType switch
            {
                "logo" => new List<string> { "brand-profile", "color-scheme", "target-audience" },
                "tagline" => new List<string> { "brand-profile", "target-audience", "tone-of-voice" },
                "narrative" => new List<string> { "brand-profile", "target-audience", "tone-of-voice" },
                "tone-of-voice" => new List<string> { "brand-profile", "target-audience" },
                "color-scheme" => new List<string> { "brand-profile", "target-audience" },
                _ => new List<string>()
            };
        }

        private bool IsDataExtraction(string message, List<ConversationMessage> history)
        {
            // Check if message contains factual information that should be stored
            var extractionIndicators = new[]
            {
                "my business",
                "my company",
                "we are",
                "we do",
                "we sell",
                "our target",
                "our customers",
                "mijn bedrijf",
                "mijn zaak",
                "we zijn",
                "wij doen"
            };

            return extractionIndicators.Any(indicator =>
                message.Contains(indicator, StringComparison.OrdinalIgnoreCase));
        }

        private bool IsSimpleQuestion(string message)
        {
            var questionIndicators = new[]
            {
                "what is",
                "what are",
                "can you tell",
                "show me",
                "wat is",
                "wat zijn",
                "kun je",
                "laat me"
            };

            return questionIndicators.Any(indicator =>
                message.StartsWith(indicator, StringComparison.OrdinalIgnoreCase));
        }

        private bool HasRecentContext(List<ConversationMessage> history)
        {
            // Check if there's recent conversation that might contain answer
            return history != null && history.Count >= 2;
        }
    }
}
