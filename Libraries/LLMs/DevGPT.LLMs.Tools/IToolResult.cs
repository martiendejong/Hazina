namespace DevGPT.LLMs.Tools
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
    }
}
