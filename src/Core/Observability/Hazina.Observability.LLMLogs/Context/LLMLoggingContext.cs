using System;
using System.Collections.Generic;
using System.Threading;

namespace Hazina.Observability.LLMLogs.Context
{
    /// <summary>
    /// Provides ambient context for LLM logging metadata.
    /// Uses AsyncLocal to ensure thread-safe context storage across async operations.
    /// </summary>
    public class LLMLoggingContext
    {
        private static readonly AsyncLocal<LLMLoggingContext> _current = new();

        /// <summary>
        /// Gets or sets the current logging context for the current async flow.
        /// </summary>
        public static LLMLoggingContext? Current
        {
            get => _current.Value;
            set => _current.Value = value;
        }

        /// <summary>
        /// Username who initiated this LLM call chain.
        /// </summary>
        public string? Username { get; set; }

        /// <summary>
        /// Feature/module that initiated this LLM call chain.
        /// </summary>
        public string? Feature { get; set; }

        /// <summary>
        /// Current step within the feature.
        /// </summary>
        public string? Step { get; set; }

        /// <summary>
        /// Parent call ID if this is a nested/child LLM call.
        /// </summary>
        public string? ParentCallId { get; set; }

        /// <summary>
        /// Embedded documents added to the context via RAG/similarity search.
        /// Each entry should contain document name, chunk, and relevance score.
        /// </summary>
        public List<EmbeddedDocument>? EmbeddedDocuments { get; set; }

        /// <summary>
        /// Creates a new logging context scope.
        /// </summary>
        public static IDisposable BeginScope(string? username = null, string? feature = null, string? step = null, string? parentCallId = null)
        {
            var previous = Current;
            Current = new LLMLoggingContext
            {
                Username = username ?? previous?.Username,
                Feature = feature ?? previous?.Feature,
                Step = step ?? previous?.Step,
                ParentCallId = parentCallId ?? previous?.ParentCallId
            };
            return new ContextScope(previous);
        }

        /// <summary>
        /// Updates the current step within the existing context.
        /// </summary>
        public static void SetStep(string step)
        {
            if (Current != null)
            {
                Current.Step = step;
            }
            else
            {
                Current = new LLMLoggingContext { Step = step };
            }
        }

        /// <summary>
        /// Sets the parent call ID for child/nested LLM calls.
        /// </summary>
        public static void SetParentCallId(string parentCallId)
        {
            if (Current != null)
            {
                Current.ParentCallId = parentCallId;
            }
            else
            {
                Current = new LLMLoggingContext { ParentCallId = parentCallId };
            }
        }

        private class ContextScope : IDisposable
        {
            private readonly LLMLoggingContext? _previous;

            public ContextScope(LLMLoggingContext? previous)
            {
                _previous = previous;
            }

            public void Dispose()
            {
                Current = _previous;
            }
        }
    }

    /// <summary>
    /// Represents a document that was embedded into the LLM context via RAG/similarity search.
    /// </summary>
    public class EmbeddedDocument
    {
        /// <summary>
        /// Name or identifier of the document.
        /// </summary>
        public string DocumentName { get; set; } = string.Empty;

        /// <summary>
        /// The text chunk that was embedded.
        /// </summary>
        public string Chunk { get; set; } = string.Empty;

        /// <summary>
        /// Relevance score from similarity search (0.0 to 1.0).
        /// </summary>
        public double RelevanceScore { get; set; }

        /// <summary>
        /// Optional metadata about the document (e.g., page number, section, etc.).
        /// </summary>
        public string? Metadata { get; set; }
    }
}
