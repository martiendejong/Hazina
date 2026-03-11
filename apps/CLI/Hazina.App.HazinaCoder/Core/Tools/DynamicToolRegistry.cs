using System;
using System.Collections.Generic;
using System.Linq;

namespace Hazina.App.HazinaCoder.Core.Tools;

/// <summary>
/// Registry for dynamically registered tools at runtime.
/// External applications can register custom tools via HTTP API.
/// </summary>
/// <remarks>
/// Improvement #22: Dynamic Tool Registration
///
/// Example:
/// POST /api/tools/register
/// {
///   "name": "custom-formatter",
///   "description": "Format code with custom rules",
///   "endpoint": "http://localhost:8080/format",
///   "parameters": [...]
/// }
/// </remarks>
public class DynamicToolRegistry
{
    private readonly Dictionary<string, ToolDescriptor> _tools = new();
    private readonly object _lock = new();

    /// <summary>
    /// Register a new tool at runtime
    /// </summary>
    public bool RegisterTool(ToolDescriptor tool)
    {
        lock (_lock)
        {
            if (_tools.ContainsKey(tool.Name))
            {
                Console.WriteLine($"[ToolRegistry] Tool already registered: {tool.Name}");
                return false;
            }

            _tools[tool.Name] = tool;
            Console.WriteLine($"[ToolRegistry] Registered tool: {tool.Name}");
            return true;
        }
    }

    /// <summary>
    /// Unregister a tool
    /// </summary>
    public bool UnregisterTool(string name)
    {
        lock (_lock)
        {
            var removed = _tools.Remove(name);
            if (removed)
                Console.WriteLine($"[ToolRegistry] Unregistered tool: {name}");
            return removed;
        }
    }

    /// <summary>
    /// Get all registered tools
    /// </summary>
    public IReadOnlyList<ToolDescriptor> GetTools()
    {
        lock (_lock)
        {
            return _tools.Values.ToList();
        }
    }

    /// <summary>
    /// Find a tool by name
    /// </summary>
    public ToolDescriptor? FindTool(string name)
    {
        lock (_lock)
        {
            return _tools.TryGetValue(name, out var tool) ? tool : null;
        }
    }
}

/// <summary>
/// Descriptor for a dynamically registered tool
/// </summary>
public class ToolDescriptor
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public List<ToolParameter> Parameters { get; set; } = new();
    public Dictionary<string, string> Headers { get; set; } = new();
    public string Method { get; set; } = "POST";
}

/// <summary>
/// Parameter for a dynamic tool
/// </summary>
public class ToolParameter
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "string";
    public bool Required { get; set; }
    public string? DefaultValue { get; set; }
    public string Description { get; set; } = string.Empty;
}
