namespace DevGPT.LLMs.Tools
{
    /// <summary>
    /// Represents a tool/function call requested by the LLM
    /// </summary>
    public interface IToolCall
    {
        string Id { get; }
        string Type { get; }
        string FunctionName { get; }
        string Arguments { get; }
    }

    /// <summary>
    /// Standard implementation of tool call
    /// </summary>
    public class ToolCall : IToolCall
    {
        public string Id { get; set; }
        public string Type { get; set; } = "function";
        public string FunctionName { get; set; }
        public string Arguments { get; set; }
    }
}
