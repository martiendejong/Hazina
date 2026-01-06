using System.Text.Json;
using Microsoft.Extensions.Logging;
using Hazina.Store.Sqlite;

namespace Hazina.Tools.Migration;

/// <summary>
/// Migrates data from file-based storage to SQLite.
/// Reads embeddings.json, chunks.json, and .metadata.json files and transfers to SQLite database.
/// </summary>
public class FileToSqliteMigrationEngine
{
    private readonly string _sourceFolder;
    private readonly string _sqliteConnectionString;
    private readonly ILogger? _logger;
    private readonly MigrationProgress _progress;

    public event EventHandler<MigrationProgressEventArgs>? ProgressChanged;

    public FileToSqliteMigrationEngine(
        string sourceFolder,
        string sqliteConnectionString,
        ILogger? logger = null)
    {
        _sourceFolder = sourceFolder ?? throw new ArgumentNullException(nameof(sourceFolder));
        _sqliteConnectionString = sqliteConnectionString ?? throw new ArgumentNullException(nameof(sqliteConnectionString));
        _logger = logger;
        _progress = new MigrationProgress { StartTime = DateTime.UtcNow };
    }

    /// <summary>
    /// Performs the migration from file-based storage to SQLite.
    /// </summary>
    /// <param name="validateOnly">If true, only validates source data without migrating</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Migration progress with results</returns>
    public async Task<MigrationProgress> MigrateAsync(
        bool validateOnly = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.LogInformation("Starting migration from '{Source}' to SQLite", _sourceFolder);

            if (!Directory.Exists(_sourceFolder))
            {
                throw new DirectoryNotFoundException($"Source folder not found: {_sourceFolder}");
            }

            // Step 1: Scan and count items
            var sourceData = await ScanSourceDataAsync(cancellationToken);
            _progress.TotalItems = sourceData.MetadataFiles.Count;

            _logger?.LogInformation("Found {Count} documents to migrate", _progress.TotalItems);
            ReportProgress("Scan complete");

            if (validateOnly)
            {
                _logger?.LogInformation("Validation mode - skipping migration");
                _progress.EndTime = DateTime.UtcNow;
                return _progress;
            }

            // Step 2: Initialize SQLite stores
            var metadataStore = new SqliteMetadataStore(_sqliteConnectionString, _logger);
            var chunkStore = new SqliteChunkStore(_sqliteConnectionString, _logger);
            var textStore = new SqliteTextStore(_sqliteConnectionString, _logger);
            var embeddingStore = new SqliteEmbeddingStore(_sqliteConnectionString, _logger);

            // Step 3: Migrate each document
            foreach (var metadataFile in sourceData.MetadataFiles)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger?.LogWarning("Migration cancelled by user");
                    break;
                }

                await MigrateDocumentAsync(
                    metadataFile,
                    sourceData,
                    metadataStore,
                    chunkStore,
                    textStore,
                    embeddingStore,
                    cancellationToken);

                _progress.ProcessedItems++;
                ReportProgress($"Migrated: {Path.GetFileName(metadataFile)}");
            }

            _progress.EndTime = DateTime.UtcNow;
            _logger?.LogInformation("Migration complete: {Status}", _progress);

            return _progress;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Migration failed");
            _progress.Errors.Add($"Migration failed: {ex.Message}");
            _progress.EndTime = DateTime.UtcNow;
            throw;
        }
    }

    private async Task<SourceData> ScanSourceDataAsync(CancellationToken cancellationToken)
    {
        var sourceData = new SourceData();

        // Find metadata files
        var metadataFolder = Path.Combine(_sourceFolder, "metadata");
        if (Directory.Exists(metadataFolder))
        {
            sourceData.MetadataFiles = Directory.GetFiles(metadataFolder, "*.metadata.json").ToList();
        }

        // Load embeddings.json if exists
        var embeddingsFile = Path.Combine(_sourceFolder, "embeddings", "embeddings.json");
        if (File.Exists(embeddingsFile))
        {
            try
            {
                var json = await File.ReadAllTextAsync(embeddingsFile, cancellationToken);
                sourceData.Embeddings = JsonSerializer.Deserialize<List<EmbeddingInfoJson>>(json) ?? new();
                _logger?.LogInformation("Loaded {Count} embeddings from {File}", sourceData.Embeddings.Count, embeddingsFile);
            }
            catch (Exception ex)
            {
                _progress.Warnings.Add($"Failed to load embeddings.json: {ex.Message}");
                _logger?.LogWarning(ex, "Failed to load embeddings.json");
            }
        }

        // Load chunks.json if exists
        var chunksFile = Path.Combine(_sourceFolder, "parts", "chunks.json");
        if (File.Exists(chunksFile))
        {
            try
            {
                var json = await File.ReadAllTextAsync(chunksFile, cancellationToken);
                sourceData.Chunks = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json)
                    ?? new();
                _logger?.LogInformation("Loaded chunks for {Count} documents from {File}",
                    sourceData.Chunks.Count, chunksFile);
            }
            catch (Exception ex)
            {
                _progress.Warnings.Add($"Failed to load chunks.json: {ex.Message}");
                _logger?.LogWarning(ex, "Failed to load chunks.json");
            }
        }

        return sourceData;
    }

    private async Task MigrateDocumentAsync(
        string metadataFilePath,
        SourceData sourceData,
        SqliteMetadataStore metadataStore,
        SqliteChunkStore chunkStore,
        SqliteTextStore textStore,
        SqliteEmbeddingStore embeddingStore,
        CancellationToken cancellationToken)
    {
        var docId = Path.GetFileNameWithoutExtension(metadataFilePath).Replace(".metadata", "");

        try
        {
            // 1. Load and migrate metadata
            var metadataJson = await File.ReadAllTextAsync(metadataFilePath, cancellationToken);
            var metadata = JsonSerializer.Deserialize<DocumentMetadata>(metadataJson);

            if (metadata == null)
            {
                _progress.Warnings.Add($"Skipped {docId}: metadata is null");
                _progress.SkippedItems++;
                return;
            }

            metadata.Id = docId; // Ensure ID matches
            await metadataStore.Store(docId, metadata);

            // 2. Migrate chunks if they exist
            if (sourceData.Chunks.TryGetValue(docId, out var chunkKeys))
            {
                await chunkStore.Store(docId, chunkKeys);

                // 3. Migrate chunk text content
                var partsFolder = Path.Combine(_sourceFolder, "parts");
                foreach (var chunkKey in chunkKeys)
                {
                    var textFile = Path.Combine(partsFolder, chunkKey);
                    if (File.Exists(textFile))
                    {
                        var content = await File.ReadAllTextAsync(textFile, cancellationToken);
                        await textStore.Store(chunkKey, content);
                    }
                }
            }

            // 4. Migrate embeddings if they exist
            var embeddingInfo = sourceData.Embeddings.FirstOrDefault(e => e.Key == docId);
            if (embeddingInfo != null && embeddingInfo.Data != null)
            {
                var embedding = new Embedding(embeddingInfo.Data);
                await embeddingStore.StoreAsync(docId, embedding, embeddingInfo.Checksum ?? "unknown");
            }

            _progress.SuccessfulItems++;
        }
        catch (Exception ex)
        {
            _progress.FailedItems++;
            _progress.Errors.Add($"Failed to migrate {docId}: {ex.Message}");
            _logger?.LogError(ex, "Failed to migrate document {DocId}", docId);
        }
    }

    private void ReportProgress(string? message = null, string? currentItem = null)
    {
        ProgressChanged?.Invoke(this, new MigrationProgressEventArgs(_progress, currentItem, message));
    }

    private class SourceData
    {
        public List<string> MetadataFiles { get; set; } = new();
        public List<EmbeddingInfoJson> Embeddings { get; set; } = new();
        public Dictionary<string, List<string>> Chunks { get; set; } = new();
    }

    private class EmbeddingInfoJson
    {
        public string Key { get; set; } = "";
        public string? Checksum { get; set; }
        public List<double>? Data { get; set; }
    }
}
