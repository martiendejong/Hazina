using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public class ToolsContextBase : IToolsContext
{
    public List<HazinaChatTool> Tools { get; set; } = new List<HazinaChatTool>();
    public Action<string, string, string>? SendMessage { get; set; } = null;
    public string? ProjectId { get; set; } = null;
    public Action<string, int, int, string>? OnTokensUsed { get; set; } = null;

    public void Add(HazinaChatTool info)
    {
        if (Tools.Any(t => t.FunctionName == info.FunctionName)) return;
        Tools.Add(info);
    }

    // Convenience overload to construct a tool in-place
    public void Add(string name, string description, List<ChatToolParameter> parameters, Func<List<HazinaChatMessage>, HazinaChatToolCall, CancellationToken, Task<string>> execute)
    {
        Add(new HazinaChatTool(name, description, parameters, execute));
    }

    public static ChatToolParameter CreateParameter(string name, string description, string type, bool required)
        => new() { Name = name, Description = description, Type = type, Required = required };
}
