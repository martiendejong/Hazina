using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DevGPT.LLMs.Tools
{
    /// <summary>
    /// Core interface for executing tool calls from LLMs
    /// </summary>
    public interface IToolExecutor
    {
        /// <summary>
        /// Execute a specific tool with the provided arguments
        /// </summary>
        Task<IToolResult> ExecuteAsync(
            string toolName,
            string argumentsJson,
            string context,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get all available tool definitions for the LLM
        /// </summary>
        List<IToolDefinition> GetToolDefinitions();
    }
}
