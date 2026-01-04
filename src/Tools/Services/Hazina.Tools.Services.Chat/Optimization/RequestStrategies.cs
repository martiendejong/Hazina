using System.Collections.Generic;

namespace Hazina.Tools.Services.Chat.Optimization
{
    /// <summary>
    /// Base class for request handling strategies.
    /// Each strategy represents a different approach to handling user requests.
    /// </summary>
    public abstract class RequestStrategy
    {
        /// <summary>
        /// Tokens used during classification/decision making.
        /// </summary>
        public int TokensUsed { get; set; }

        /// <summary>
        /// Stage name for logging and monitoring.
        /// </summary>
        public abstract string StageName { get; }
    }

    /// <summary>
    /// Strategy for immediate responses without LLM call.
    /// Used for simple tests, greetings, and acknowledgments.
    /// </summary>
    public class ImmediateResponseStrategy : RequestStrategy
    {
        public override string StageName => "immediate";

        /// <summary>
        /// Pre-determined response text.
        /// </summary>
        public string Response { get; set; } = string.Empty;

        public ImmediateResponseStrategy(string response)
        {
            Response = response;
            TokensUsed = 0; // No LLM call needed
        }
    }

    /// <summary>
    /// Strategy for responses using locally cached data.
    /// No LLM call needed, just format cached data.
    /// </summary>
    public class LocalDataResponseStrategy : RequestStrategy
    {
        public override string StageName => "local-data";

        /// <summary>
        /// Data retrieved from local cache.
        /// </summary>
        public Dictionary<string, string> Data { get; set; } = new();

        public LocalDataResponseStrategy(Dictionary<string, string> data)
        {
            Data = data;
            TokensUsed = 0; // No LLM call for retrieval
        }
    }

    /// <summary>
    /// Strategy for targeted retrieval with specific context.
    /// Makes Stage 2 LLM call with limited, task-specific context.
    /// </summary>
    public class TargetedRetrievalStrategy : RequestStrategy
    {
        public override string StageName => "targeted";

        /// <summary>
        /// Type of task being performed.
        /// </summary>
        public TaskType TaskType { get; set; }

        /// <summary>
        /// Specific data fields requested for this task.
        /// </summary>
        public List<string> RequestedFields { get; set; } = new();

        /// <summary>
        /// Additional requirements or constraints.
        /// </summary>
        public string? Requirements { get; set; }
    }

    /// <summary>
    /// Strategy for full context exploration.
    /// Makes Stage 3 LLM call with complete context and RAG retrieval.
    /// </summary>
    public class FullExplorationStrategy : RequestStrategy
    {
        public override string StageName => "full";

        /// <summary>
        /// Reason why full exploration is needed.
        /// </summary>
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>
    /// Types of tasks that can be performed.
    /// </summary>
    public enum TaskType
    {
        /// <summary>
        /// Extracting and storing data from conversation.
        /// </summary>
        DataExtraction,

        /// <summary>
        /// Generating content (logo, analysis field, etc.).
        /// </summary>
        ContentGeneration,

        /// <summary>
        /// Retrieving and presenting existing data.
        /// </summary>
        DataRetrieval,

        /// <summary>
        /// Answering questions about the project.
        /// </summary>
        Question,

        /// <summary>
        /// Other/unknown task type - requires full context.
        /// </summary>
        Unknown
    }
}
