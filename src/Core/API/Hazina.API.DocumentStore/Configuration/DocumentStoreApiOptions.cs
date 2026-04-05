using Hazina.API.DocumentStore.Integration;
using Hazina.API.DocumentStore.Services.FormatHandlers;
using Hazina.AI.Providers.Core;
using Hazina.LLMs;

namespace Hazina.API.DocumentStore.Configuration;

/// <summary>
/// Configuration options for Hazina Document Store API
/// </summary>
public class DocumentStoreApiOptions
{
    /// <summary>
    /// Base path for file storage. Default: "./data"
    /// </summary>
    public string FileStoragePath { get; set; } = "./data";

    /// <summary>
    /// Maximum upload size in MB. Default: 100
    /// </summary>
    public int MaxUploadSizeMB { get; set; } = 100;

    /// <summary>
    /// Embedding vector dimensions. Default: 1536
    /// </summary>
    public int EmbeddingDimensions { get; set; } = 1536;

    /// <summary>
    /// Default LLM model name. Default: "gpt-4o"
    /// </summary>
    public string DefaultLLMModel { get; set; } = "gpt-4o";

    /// <summary>
    /// Default embedding model name. Default: "text-embedding-3-small"
    /// </summary>
    public string DefaultEmbeddingModel { get; set; } = "text-embedding-3-small";

    /// <summary>
    /// SQLite connection string for metadata storage. Default: "Data Source=documentstore.db"
    /// </summary>
    public string MetadataConnectionString { get; set; } = "Data Source=documentstore.db";

    /// <summary>
    /// Automatically create/migrate the database on startup. Default: true
    /// </summary>
    public bool AutoMigrateDatabase { get; set; } = true;

    /// <summary>
    /// Path to Tesseract OCR data files. Default: "./tessdata"
    /// </summary>
    public string TesseractDataPath { get; set; } = "./tessdata";

    /// <summary>
    /// Whether to include the built-in AuthController. Default: true
    /// Set to false if you provide your own authentication.
    /// </summary>
    public bool IncludeAuthController { get; set; } = true;

    /// <summary>
    /// Factory to create a custom ILLMClient. If null, default OpenAI client is created from environment.
    /// </summary>
    public Func<IServiceProvider, ILLMClient>? LLMClientFactory { get; set; }

    /// <summary>
    /// Factory to create a custom IProviderOrchestrator. If null, default OpenAI orchestrator is created.
    /// </summary>
    public Func<IServiceProvider, IProviderOrchestrator>? ProviderOrchestratorFactory { get; set; }

    /// <summary>
    /// Factory to override the store factory. If null, default HazinaStoreFactory is used.
    /// </summary>
    public Func<IServiceProvider, IHazinaStoreFactory>? StoreFactoryOverride { get; set; }

    /// <summary>
    /// Additional format handler types to register. Must implement IFormatHandler.
    /// </summary>
    public List<Type> AdditionalFormatHandlers { get; set; } = new();

    /// <summary>
    /// Whether to register the built-in format handlers (Text, PDF, DOCX, Image). Default: true
    /// </summary>
    public bool RegisterBuiltInFormatHandlers { get; set; } = true;
}
