using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace Hazina.App.HazinaCoder.Core.Tools;

/// <summary>
/// Auto-discovers tools from configured directories.
/// Tools can be executables, scripts, or JSON descriptors.
/// </summary>
/// <remarks>
/// Improvement #24: Tool Auto-Discovery
///
/// Supported formats:
/// - .exe/.bat/.ps1 files with companion .tool.json
/// - Standalone .tool.json descriptors
/// - Assemblies with [Tool] attribute
/// </remarks>
public class ToolAutoDiscovery
{
    private readonly List<string> _toolDirectories;
    private readonly Dictionary<string, DiscoveredTool> _discoveredTools = new();

    public ToolAutoDiscovery(IEnumerable<string> toolDirectories)
    {
        _toolDirectories = toolDirectories.ToList();
    }

    /// <summary>
    /// Scan all configured directories and discover tools
    /// </summary>
    public int DiscoverTools()
    {
        _discoveredTools.Clear();
        int count = 0;

        foreach (var directory in _toolDirectories)
        {
            if (!Directory.Exists(directory))
            {
                Console.WriteLine($"[ToolDiscovery] Directory not found: {directory}");
                continue;
            }

            // Find .tool.json descriptors
            var jsonFiles = Directory.GetFiles(directory, "*.tool.json", SearchOption.AllDirectories);
            foreach (var file in jsonFiles)
            {
                try
                {
                    var tool = ParseToolDescriptor(file);
                    if (tool != null)
                    {
                        _discoveredTools[tool.Name] = tool;
                        count++;
                        Console.WriteLine($"[ToolDiscovery] Found tool: {tool.Name} ({file})");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ToolDiscovery] Error parsing {file}: {ex.Message}");
                }
            }

            // TODO: Discover .exe/.bat/.ps1 with companion .tool.json
            // TODO: Load assemblies and scan for [Tool] attributes
        }

        Console.WriteLine($"[ToolDiscovery] Discovered {count} tools");
        return count;
    }

    /// <summary>
    /// Parse a .tool.json descriptor
    /// </summary>
    private DiscoveredTool? ParseToolDescriptor(string filePath)
    {
        var json = File.ReadAllText(filePath);
        var descriptor = JsonSerializer.Deserialize<ToolDescriptor>(json);

        if (descriptor == null)
            return null;

        return new DiscoveredTool
        {
            Name = descriptor.Name,
            Description = descriptor.Description,
            FilePath = filePath,
            Descriptor = descriptor
        };
    }

    /// <summary>
    /// Get all discovered tools
    /// </summary>
    public IReadOnlyDictionary<string, DiscoveredTool> GetTools() => _discoveredTools;
}

/// <summary>
/// Represents a discovered tool
/// </summary>
public class DiscoveredTool
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public ToolDescriptor Descriptor { get; set; } = new();
    public DateTime DiscoveredAt { get; set; } = DateTime.UtcNow;
}
