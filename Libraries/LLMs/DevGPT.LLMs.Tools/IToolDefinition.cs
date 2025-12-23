using System.Text.Json;

namespace DevGPT.LLMs.Tools
{
    /// <summary>
    /// Represents a tool/function definition for LLM tool calling
    /// </summary>
    public interface IToolDefinition
    {
        string Type { get; }
        string Name { get; }
        string Description { get; }
        JsonElement Parameters { get; }
    }

    /// <summary>
    /// Standard implementation of tool definition
    /// </summary>
    public class ToolDefinition : IToolDefinition
    {
        public string Type { get; set; } = "function";
        public string Name { get; set; }
        public string Description { get; set; }
        public JsonElement Parameters { get; set; }
    }
}
