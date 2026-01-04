using System;
using System.Collections.Generic;

namespace Hazina.Tools.Services.Chat.Optimization
{
    /// <summary>
    /// Configuration options for LLM token optimization.
    /// Enables progressive context loading and smart request classification.
    /// </summary>
    public class LLMOptimizationOptions
    {
        /// <summary>
        /// Enable smart context optimization (vs. legacy full-context mode).
        /// When disabled, falls back to original behavior.
        /// </summary>
        public bool EnableSmartContext { get; set; } = true;

        /// <summary>
        /// Token budgets per request stage.
        /// </summary>
        public StageTokenBudgets StageTokenBudgets { get; set; } = new();

        /// <summary>
        /// Cache configuration settings.
        /// </summary>
        public CacheSettings CacheSettings { get; set; } = new();

        /// <summary>
        /// Classification rules for pattern matching.
        /// </summary>
        public ClassificationRules ClassificationRules { get; set; } = new();
    }

    public class StageTokenBudgets
    {
        /// <summary>
        /// Maximum tokens for Stage 1 (triage) requests.
        /// Includes minimal prompt + last 2 messages + triage tools.
        /// </summary>
        public int Triage { get; set; } = 500;

        /// <summary>
        /// Maximum tokens for Stage 2 (targeted retrieval) requests.
        /// Includes task-specific prompt + requested data + relevant tools.
        /// </summary>
        public int Targeted { get; set; } = 1200;

        /// <summary>
        /// Maximum tokens for Stage 3 (full exploration) requests.
        /// Includes full system prompt + RAG documents + all tools.
        /// </summary>
        public int Full { get; set; } = 2000;
    }

    public class CacheSettings
    {
        /// <summary>
        /// Enable in-memory caching of project data.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Cache timeout in minutes.
        /// After this duration, cached data is reloaded from disk.
        /// </summary>
        public int TimeoutMinutes { get; set; } = 5;

        /// <summary>
        /// Invalidate cache entries when project data is updated.
        /// </summary>
        public bool InvalidateOnUpdate { get; set; } = true;
    }

    public class ClassificationRules
    {
        /// <summary>
        /// Patterns that indicate simple test messages (no LLM call needed).
        /// </summary>
        public List<string> SimpleTestPatterns { get; set; } = new()
        {
            "test",
            "dit is een test",
            "en nog 1",
            "testing"
        };

        /// <summary>
        /// Patterns that indicate greeting messages.
        /// </summary>
        public List<string> GreetingPatterns { get; set; } = new()
        {
            "hello",
            "hi",
            "hey",
            "hallo",
            "good morning",
            "good afternoon",
            "good evening"
        };

        /// <summary>
        /// Mapping of analysis field keys to trigger keywords.
        /// Used for pattern-based local data retrieval.
        /// </summary>
        public Dictionary<string, List<string>> LocalDataFields { get; set; } = new()
        {
            ["color-scheme"] = new List<string> { "color", "scheme", "colors", "kleur" },
            ["brand-profile"] = new List<string> { "brand name", "company name", "business name", "merknaam" },
            ["target-audience"] = new List<string> { "target audience", "customers", "market", "doelgroep" },
            ["tagline"] = new List<string> { "tagline", "slogan", "motto" },
            ["tone-of-voice"] = new List<string> { "tone of voice", "communication style", "toon" },
            ["narrative"] = new List<string> { "narrative", "story", "verhaal" }
        };
    }
}
