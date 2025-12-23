public class StoreConfig
{
    public string Name { get; set; }
    public string Description { get; set; }
    public string Path { get; set; }
    public string[] FileFilters { get; set; } = [];
    public string SubDirectory { get; set; } = "";
    public string[] ExcludePattern { get; set; } = [];
}