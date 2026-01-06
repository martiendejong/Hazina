namespace Hazina.LLMs.Tools
{
    /// <summary>
    /// Represents the result of a tool execution
    /// </summary>
    public interface IToolResult
    {
        string ToolCallId { get; }
        bool Success { get; }
        object Result { get; }
        string Error { get; }
        int TokensUsed { get; }

        // STAP 8: Support for async user response pattern (backwards compatible)
        /// <summary>
        /// Indicates if the tool execution requires user response before continuing
        /// </summary>
        bool AwaitingUserResponse { get; }
    }

    /// <summary>
    /// Standard implementation of tool result
    /// </summary>
    public class ToolResult : IToolResult
    {
        public string ToolCallId { get; set; }
        public bool Success { get; set; }
        public object Result { get; set; }
        public string Error { get; set; }
        public int TokensUsed { get; set; }

        // STAP 8: Default to false for backward compatibility
        public bool AwaitingUserResponse { get; set; } = false;
    }
}
