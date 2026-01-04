using System;

namespace Hazina.Observability.LLMLogs.Storage.Models
{
    /// <summary>
    /// Represents a complete log entry for an LLM API call.
    /// Captures request metadata, messages, responses, token usage, and cost estimation.
    /// </summary>
    public class LLMCallLog
    {
        /// <summary>
        /// Unique identifier for this LLM call.
        /// </summary>
        public string CallId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// If this call was made as part of a function call or tool chain, this references the parent call.
        /// </summary>
        public string? ParentCallId { get; set; }

        /// <summary>
        /// Username of the user who initiated this request.
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Feature/module that initiated this LLM call (e.g., "Chat", "Analysis", "BlogGeneration").
        /// </summary>
        public string Feature { get; set; } = string.Empty;

        /// <summary>
        /// Optional step within a feature (e.g., "ColorSchemeGeneration", "PromptEnhancement").
        /// </summary>
        public string? Step { get; set; }

        /// <summary>
        /// Timestamp when the call was initiated (UTC).
        /// </summary>
        public DateTime DateTimeUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// LLM provider name (e.g., "OpenAI", "Anthropic", "Google").
        /// </summary>
        public string Provider { get; set; } = string.Empty;

        /// <summary>
        /// Model name (e.g., "gpt-4", "claude-3-opus", "gemini-pro").
        /// </summary>
        public string Model { get; set; } = string.Empty;

        /// <summary>
        /// Indicates if this call was a tool/function call.
        /// </summary>
        public bool IsToolCall { get; set; }

        /// <summary>
        /// If IsToolCall is true, the name of the tool that was called.
        /// </summary>
        public string? ToolName { get; set; }

        /// <summary>
        /// If IsToolCall is true, the arguments passed to the tool (JSON format).
        /// </summary>
        public string? ToolArguments { get; set; }

        /// <summary>
        /// Complete request messages sent to the LLM (serialized as JSON).
        /// </summary>
        public string RequestMessages { get; set; } = string.Empty;

        /// <summary>
        /// Complete response data from the LLM (serialized as JSON).
        /// </summary>
        public string ResponseData { get; set; } = string.Empty;

        /// <summary>
        /// Number of messages in the request.
        /// </summary>
        public int MessageCount { get; set; }

        /// <summary>
        /// Embedded documents/context added via RAG/similarity search (serialized as JSON).
        /// Includes document names, chunks, and relevance scores.
        /// </summary>
        public string? EmbeddedDocuments { get; set; }

        /// <summary>
        /// Number of documents/chunks added to context via embeddings.
        /// </summary>
        public int EmbeddedDocumentCount { get; set; }

        /// <summary>
        /// Number of tokens used in the input/request.
        /// </summary>
        public int InputTokens { get; set; }

        /// <summary>
        /// Number of tokens used in the output/response.
        /// </summary>
        public int OutputTokens { get; set; }

        /// <summary>
        /// Total tokens used (typically InputTokens + OutputTokens).
        /// </summary>
        public int TotalTokens { get; set; }

        /// <summary>
        /// Estimated cost for input tokens (in USD).
        /// </summary>
        public decimal InputCost { get; set; }

        /// <summary>
        /// Estimated cost for output tokens (in USD).
        /// </summary>
        public decimal OutputCost { get; set; }

        /// <summary>
        /// Total estimated cost (InputCost + OutputCost, in USD).
        /// </summary>
        public decimal TotalCost { get; set; }

        /// <summary>
        /// Execution time in milliseconds.
        /// </summary>
        public long ExecutionTimeMs { get; set; }

        /// <summary>
        /// Whether the call completed successfully.
        /// </summary>
        public bool Success { get; set; } = true;

        /// <summary>
        /// If Success is false, contains the error message.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// When this log entry was created in the database (UTC).
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
