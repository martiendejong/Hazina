using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hazina.CLI.Infrastructure;

/// <summary>
/// Registry of configured document stores
/// </summary>
public class StoreRegistry
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public Dictionary<string, CliStoreConfig> Stores { get; set; } = new();

    /// <summary>
    /// Load registry from disk
    /// </summary>
    public static StoreRegistry Load(string? path = null)
    {
        path ??= HazinaConfig.GetStoresConfigPath();

        if (!File.Exists(path))
        {
            return new StoreRegistry();
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<StoreRegistry>(json, JsonOptions) ?? new StoreRegistry();
    }

    /// <summary>
    /// Save registry to disk
    /// </summary>
    public void Save(string? path = null)
    {
        path ??= HazinaConfig.GetStoresConfigPath();

        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var json = JsonSerializer.Serialize(this, JsonOptions);
        File.WriteAllText(path, json);
    }

    /// <summary>
    /// Get a store by name
    /// </summary>
    public CliStoreConfig? GetStore(string name)
    {
        return Stores.TryGetValue(name, out var store) ? store : null;
    }

    /// <summary>
    /// Add or update a store
    /// </summary>
    public void SetStore(string name, CliStoreConfig config)
    {
        Stores[name] = config;
    }
}

/// <summary>
/// Configuration for a single document store
/// </summary>
public class CliStoreConfig
{
    public string Backend { get; set; } = "file";
    public string Path { get; set; } = "";
    public string? ConnectionString { get; set; }
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";
    public int ChunkSize { get; set; } = 500;
    public int ChunkOverlap { get; set; } = 50;
    public DateTime Created { get; set; } = DateTime.UtcNow;
    public DateTime LastSync { get; set; }
    public int FileCount { get; set; }
    public int ChunkCount { get; set; }

    /// <summary>
    /// Index of files with their hashes for sync detection
    /// </summary>
    public Dictionary<string, FileIndexEntry> FileIndex { get; set; } = new();
}

/// <summary>
/// Entry for a file in the index
/// </summary>
public class FileIndexEntry
{
    public string RelativePath { get; set; } = "";
    public string Hash { get; set; } = "";
    public DateTime LastModified { get; set; }
    public int ChunkCount { get; set; }
    public List<string> ChunkIds { get; set; } = new();
    public List<string> Tags { get; set; } = new();
}
