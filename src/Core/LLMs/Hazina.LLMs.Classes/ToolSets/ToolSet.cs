/// <summary>
/// Represents a named collection of tools that can be enabled/disabled as a group
/// </summary>
public class ToolSet
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public List<string> Tags { get; set; } = new();
    public List<string> ToolNames { get; set; } = new();
    public bool EnabledByDefault { get; set; } = false;
    public bool RequiresOptIn { get; set; } = true;
    public List<string> Dependencies { get; set; } = new();
}
