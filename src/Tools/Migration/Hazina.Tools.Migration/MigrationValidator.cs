using System.Text.Json;
using Microsoft.Extensions.Logging;
using Hazina.Store.Sqlite;

namespace Hazina.Tools.Migration;

/// <summary>
/// Validates that data was migrated correctly from file-based storage to SQLite.
/// Compares source and destination data to ensure integrity.
/// </summary>
public class MigrationValidator
{
    private readonly string _sourceFolder;
    private readonly string _sqliteConnectionString;
    private readonly ILogger? _logger;

    public MigrationValidator(
        string sourceFolder,
        string sqliteConnectionString,
        ILogger? logger = null)
    {
        _sourceFolder = sourceFolder;
        _sqliteConnectionString = sqliteConnectionString;
        _logger = logger;
    }

    /// <summary>
    /// Validates the migration by comparing source and destination data.
    /// </summary>
    /// <returns>Validation result with any discrepancies found</returns>
    public async Task<ValidationResult> ValidateAsync(CancellationToken cancellationToken = default)
    {
        var result = new ValidationResult();

        try
        {
            _logger?.LogInformation("Starting migration validation");

            var metadataStore = new SqliteMetadataStore(_sqliteConnectionString, _logger);
            var chunkStore = new SqliteChunkStore(_sqliteConnectionString, _logger);
            var embeddingStore = new SqliteEmbeddingStore(_sqliteConnectionString, _logger);

            // Get all metadata files from source
            var metadataFolder = Path.Combine(_sourceFolder, "metadata");
            if (!Directory.Exists(metadataFolder))
            {
                result.Errors.Add("Source metadata folder not found");
                return result;
            }

            var metadataFiles = Directory.GetFiles(metadataFolder, "*.metadata.json");
            result.TotalDocuments = metadataFiles.Length;

            // Validate each document
            foreach (var metadataFile in metadataFiles)
            {
                if (cancellationToken.IsCancellationRequested) break;

                var docId = Path.GetFileNameWithoutExtension(metadataFile).Replace(".metadata", "");
                await ValidateDocumentAsync(docId, metadataFile, metadataStore, chunkStore, embeddingStore, result);
            }

            result.IsValid = result.Errors.Count == 0;
            _logger?.LogInformation("Validation complete: {Result}", result);

            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Validation failed");
            result.Errors.Add($"Validation failed: {ex.Message}");
            return result;
        }
    }

    private async Task ValidateDocumentAsync(
        string docId,
        string metadataFile,
        SqliteMetadataStore metadataStore,
        SqliteChunkStore chunkStore,
        SqliteEmbeddingStore embeddingStore,
        ValidationResult result)
    {
        try
        {
            // 1. Validate metadata exists in SQLite
            var metadata = await metadataStore.Get(docId);
            if (metadata == null)
            {
                result.Errors.Add($"Document {docId}: metadata missing in SQLite");
                result.MissingDocuments++;
                return;
            }

            // 2. Validate metadata content matches
            var sourceJson = await File.ReadAllTextAsync(metadataFile);
            var sourceMetadata = JsonSerializer.Deserialize<DocumentMetadata>(sourceJson);

            if (sourceMetadata != null)
            {
                if (metadata.MimeType != sourceMetadata.MimeType)
                {
                    result.Warnings.Add($"Document {docId}: MimeType mismatch");
                }

                if (metadata.Tags.Count != sourceMetadata.Tags.Count)
                {
                    result.Warnings.Add($"Document {docId}: Tag count mismatch ({metadata.Tags.Count} vs {sourceMetadata.Tags.Count})");
                }
            }

            // 3. Validate chunks if they exist
            var chunksFile = Path.Combine(_sourceFolder, "parts", "chunks.json");
            if (File.Exists(chunksFile))
            {
                var chunksJson = await File.ReadAllTextAsync(chunksFile);
                var sourceChunks = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(chunksJson);

                if (sourceChunks?.TryGetValue(docId, out var sourceChunkKeys) == true)
                {
                    var destChunkKeys = (await chunkStore.Get(docId)).ToList();

                    if (destChunkKeys.Count != sourceChunkKeys.Count)
                    {
                        result.Warnings.Add($"Document {docId}: Chunk count mismatch ({destChunkKeys.Count} vs {sourceChunkKeys.Count})");
                    }
                }
            }

            // 4. Validate embeddings if they exist
            var embeddingsFile = Path.Combine(_sourceFolder, "embeddings", "embeddings.json");
            if (File.Exists(embeddingsFile))
            {
                var embeddingsJson = await File.ReadAllTextAsync(embeddingsFile);
                var sourceEmbeddings = JsonSerializer.Deserialize<List<EmbeddingInfoJson>>(embeddingsJson);
                var sourceEmbedding = sourceEmbeddings?.FirstOrDefault(e => e.Key == docId);

                if (sourceEmbedding != null)
                {
                    var destEmbedding = await embeddingStore.GetAsync(docId);

                    if (destEmbedding == null)
                    {
                        result.Warnings.Add($"Document {docId}: Embedding missing in SQLite");
                    }
                    else if (sourceEmbedding.Data != null && destEmbedding.Data.Count != sourceEmbedding.Data.Count)
                    {
                        result.Warnings.Add($"Document {docId}: Embedding dimension mismatch ({destEmbedding.Data.Count} vs {sourceEmbedding.Data.Count})");
                    }
                }
            }

            result.ValidatedDocuments++;
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Document {docId}: Validation error - {ex.Message}");
        }
    }

    private class EmbeddingInfoJson
    {
        public string Key { get; set; } = "";
        public string? Checksum { get; set; }
        public List<double>? Data { get; set; }
    }
}

/// <summary>
/// Result of migration validation.
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; set; }
    public int TotalDocuments { get; set; }
    public int ValidatedDocuments { get; set; }
    public int MissingDocuments { get; set; }

    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();

    public override string ToString()
    {
        return $"Validation: {(IsValid ? "PASSED" : "FAILED")}, " +
               $"{ValidatedDocuments}/{TotalDocuments} documents validated, " +
               $"{MissingDocuments} missing, {Errors.Count} errors, {Warnings.Count} warnings";
    }
}
