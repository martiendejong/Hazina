using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Hazina.LLMs;

/// <summary>
/// Legacy file-based embedding store.
/// </summary>
/// <remarks>
/// This class is obsolete. Use EmbeddingJsonFileStore instead for:
/// - Better separation of concerns (no embedding generation in storage)
/// - Cleaner API with focused interfaces
/// - Thread-safe operations
/// - Better error handling
/// </remarks>
[Obsolete("Use EmbeddingJsonFileStore with EmbeddingService instead. See Store/EmbeddingStore/EmbeddingJsonFileStore.cs")]
public class EmbeddingFileStore : AbstractTextEmbeddingStore, ITextEmbeddingStore
{
    public string EmbeddingsFilePath { get; set; }

    public override EmbeddingInfo[] Embeddings
    {
        get
        {
            // Note: This is synchronous for backward compatibility, but may return empty array
            // if called before async initialization. Prefer using GetEmbedding() for async access.
            if (!_initialized)
            {
                // Attempt synchronous initialization (fallback)
                Console.WriteLine("[EmbeddingFileStore] WARNING: Sync property access before async init. Consider calling GetEmbedding() instead.");
                _embeddings = ReadEmbeddingsFile();
                _initialized = true;
            }
            return _embeddings.ToArray();
        }
    }

    public List<EmbeddingInfo> _embeddings;
    private bool _initialized = false;
    private readonly SemaphoreSlim _initLock = new SemaphoreSlim(1, 1);

    // Constructor for full functionality (with embedding provider)
    public EmbeddingFileStore(string embeddingsFilePath, ILLMClient embeddingProvider) : base(embeddingProvider)
    {
        EmbeddingsFilePath = embeddingsFilePath;
        _embeddings = new List<EmbeddingInfo>(); // Initialize empty, load async
    }

    // Constructor for read-only scenarios or where embedding is not needed
    public EmbeddingFileStore(string embeddingsFilePath) : base(null)
    {
        EmbeddingsFilePath = embeddingsFilePath;
        _embeddings = new List<EmbeddingInfo>(); // Initialize empty, load async
    }

    /// <summary>
    /// Ensures the embedding store is initialized by loading embeddings from disk.
    /// Thread-safe and idempotent - safe to call multiple times.
    /// </summary>
    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;

        await _initLock.WaitAsync();
        try
        {
            if (_initialized) return; // Double-check after acquiring lock

            _embeddings = await Task.Run(() => ReadEmbeddingsFile());
            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public List<EmbeddingInfo> ReadEmbeddingsFile()
    {
        if (File.Exists(EmbeddingsFilePath))
        {
            try
            {
                var data = File.ReadAllText(EmbeddingsFilePath);

                // Configure JSON options to handle Embedding (which extends List<double>)
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    WriteIndented = false,
                    Converters = { new EmbeddingJsonConverter() }
                };

                try
                {
                    var e = JsonSerializer.Deserialize<List<EmbeddingInfo>>(data, options);
                    if (e != null && e.Count > 0)
                    {
                        Console.WriteLine($"[EmbeddingFileStore] Successfully loaded {e.Count} embeddings from {EmbeddingsFilePath}");
                        return e;
                    }

                    Console.WriteLine($"[EmbeddingFileStore] WARNING: Deserialized list is null or empty from {EmbeddingsFilePath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[EmbeddingFileStore] ERROR deserializing JSON: {ex.Message}");
                    Console.WriteLine($"[EmbeddingFileStore] Stack trace: {ex.StackTrace}");

                    // Try legacy CSV fallback format (unlikely but kept for compatibility)
                    try
                    {
                        var q = new List<EmbeddingInfo>();
                        var lines = data.Split('\n');
                        foreach (var line in lines.Where(l => !string.IsNullOrWhiteSpace(l)))
                        {
                            var parts = line.Split(",");
                            if (parts.Length < 4) continue;
                            var b = new EmbeddingInfo(parts[0], parts[2], [.. parts.Skip(3).Select(p => double.Parse(p)).ToList()]);
                            q.Add(b);
                        }

                        if (q.Count > 0)
                        {
                            Console.WriteLine($"[EmbeddingFileStore] Loaded {q.Count} embeddings from legacy CSV format");
                            return q;
                        }
                    }
                    catch (Exception csvEx)
                    {
                        Console.WriteLine($"[EmbeddingFileStore] ERROR parsing CSV fallback: {csvEx.Message}");
                    }
                }
            }
            catch (IOException fileEx)
            {
                Console.WriteLine($"[EmbeddingFileStore] ERROR reading file {EmbeddingsFilePath}: {fileEx.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EmbeddingFileStore] Unexpected error: {ex.Message}");
            }
        }
        else
        {
            Console.WriteLine($"[EmbeddingFileStore] File does not exist: {EmbeddingsFilePath}");
        }

        return new List<EmbeddingInfo>();
    }

    public void LoadEmbeddingsFile()
    {
        _embeddings = ReadEmbeddingsFile();
    }

    /// <summary>
    /// Saves embeddings to file using atomic write-temp-rename pattern with checksum validation.
    /// </summary>
    public async Task StoreEmbeddingsFile()
    {
        var directory = Path.GetDirectoryName(EmbeddingsFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Use consistent serialization options matching ReadEmbeddingsFile
        var options = new JsonSerializerOptions
        {
            WriteIndented = false,
            Converters = { new EmbeddingJsonConverter() }
        };

        var json = JsonSerializer.Serialize(Embeddings, options);

        // Compute checksum
        var checksum = ComputeSha256(json);

        // ATOMIC WRITE PATTERN: Write to temp file first
        var tempPath = EmbeddingsFilePath + ".tmp";
        var checksumPath = EmbeddingsFilePath + ".sha256";
        var tempChecksumPath = checksumPath + ".tmp";

        try
        {
            // Write data to temp file
            await File.WriteAllTextAsync(tempPath, json);

            // Write checksum to temp file
            await File.WriteAllTextAsync(tempChecksumPath, checksum);

            // Atomic rename: move temp files to final destination
            File.Move(tempPath, EmbeddingsFilePath, overwrite: true);
            File.Move(tempChecksumPath, checksumPath, overwrite: true);
        }
        catch
        {
            // Clean up temp files if something went wrong
            if (File.Exists(tempPath))
                File.Delete(tempPath);
            if (File.Exists(tempChecksumPath))
                File.Delete(tempChecksumPath);

            throw;
        }
    }

    /// <summary>
    /// Computes SHA256 checksum of a string.
    /// </summary>
    private static string ComputeSha256(string data)
    {
        var bytes = Encoding.UTF8.GetBytes(data);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public override async Task<EmbeddingInfo?> GetEmbedding(string key)
    {
        await EnsureInitializedAsync();
        return _embeddings.FirstOrDefault(e => e.Key == key);
    }

    public override async Task<bool> RemoveEmbedding(string key)
    {
        await EnsureInitializedAsync();
        var embedding = await GetEmbedding(key);
        if (embedding == null) return false;
        _embeddings.Remove(embedding);
        await StoreEmbeddingsFile();
        return true;
    }

    protected override async Task UpdateEmbedding(EmbeddingInfo embedding) {
        await EnsureInitializedAsync();
        await StoreEmbeddingsFile();
    }

    protected override async Task AddEmbedding(EmbeddingInfo embeddingInfo)
    {
        await EnsureInitializedAsync();
        _embeddings.Add(embeddingInfo);
        await StoreEmbeddingsFile();
    }
}
