using Hazina.LLMs.OpenAI;
using System.IO;
using HazinaStore.Models;

namespace Hazina.Tools.Data
{
    public static class StoreProvider
    {
        /// <summary>
        /// Gets store setup using file-based storage (legacy method)
        /// </summary>
        public static StoreSetup GetStoreSetup(string folder, string apiKey)
        {
            // Legacy overload for backwards compatibility - create basic OpenAIConfig with just API key
            var config = new OpenAIConfig(apiKey);
            return GetStoreSetup(folder, config);
        }

        /// <summary>
        /// Gets store setup using file-based storage with full OpenAI configuration
        /// </summary>
        public static StoreSetup GetStoreSetup(string folder, OpenAIConfig openAIConfig)
        {
            var embeddingsFolder = Path.Combine(folder, "embeddings");
            var embeddingsPath = Path.Combine(embeddingsFolder, "embeddings.json");
            var partsPath = Path.Combine(folder, "parts");

            // Migrate old embeddings file to new structure
            var oldEmbeddingsFile = Path.Combine(folder, "embeddings");
            if (File.Exists(oldEmbeddingsFile))
            {
                try
                {
                    // Create embeddings directory if it doesn't exist
                    if (!Directory.Exists(embeddingsFolder))
                        Directory.CreateDirectory(embeddingsFolder);

                    // Move old file to new location
                    File.Move(oldEmbeddingsFile, embeddingsPath, overwrite: true);
                    System.Console.WriteLine($"Migrated embeddings file from '{oldEmbeddingsFile}' to '{embeddingsPath}'");
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine($"Warning: Could not migrate old embeddings file: {ex.Message}");
                }
            }

            var llmClient = new OpenAIClientWrapper(openAIConfig);
            var fileStore = new EmbeddingFileStore(embeddingsPath, llmClient);
            var textStore = new TextFileStore(folder);
            var partStore = new DocumentPartFileStore(partsPath);
            var chunkIndexPath = Path.Combine(partsPath, "chunks.json");
            var chunkStore = new ChunkFileStore(chunkIndexPath);

            var metadataPath = Path.Combine(folder, "metadata");
            var metadataStore = new QueryableMetadataFileStore(metadataPath);

            var store = new DocumentStore(fileStore, textStore, chunkStore, metadataStore, llmClient);

            // Create tag scoring components
            var tagRelevancePath = Path.Combine(folder, "tag_relevance");
            var tagRelevanceStore = new TagRelevanceFileStore(tagRelevancePath);
            var tagScoringService = new LLMTagScoringService(llmClient, tagRelevanceStore);
            var compositeScorer = new DefaultCompositeScorer();

            var setup = new StoreSetup()
            {
                LLMClient = llmClient,
                DocumentPartStore = partStore,
                TextStore = textStore,
                TextEmbeddingStore = fileStore,
                Store = store,
                QueryableMetadataStore = metadataStore,
                TagRelevanceStore = tagRelevanceStore,
                TagScoringService = tagScoringService,
                CompositeScorer = compositeScorer
            };

            return setup;
        }

        /// <summary>
        /// Gets store setup based on configuration settings.
        /// Supports SQLite, file-based, PostgreSQL, Supabase, and hybrid modes.
        /// </summary>
        /// <param name="config">Hazina configuration</param>
        /// <param name="folder">Folder for file-based storage (optional)</param>
        /// <param name="embeddingDimension">Embedding dimension (default 1536)</param>
        /// <returns>Configured StoreSetup</returns>
        public static StoreSetup GetStoreSetup(
            HazinaStoreConfig config,
            string folder = null,
            int embeddingDimension = 1536)
        {
            // Use OpenAI config from HazinaStoreConfig if available, otherwise fallback to ApiSettings
            var openAIConfig = config.OpenAI;
            if (openAIConfig == null || string.IsNullOrEmpty(openAIConfig.ApiKey))
            {
                // Fallback: create OpenAI config from ApiSettings (legacy support)
                var fallbackApiKey = config.ApiSettings?.OpenApiKey
                    ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
                    ?? throw new InvalidOperationException("OpenAI API key is required");
                openAIConfig = new OpenAIConfig(fallbackApiKey);
            }

            var apiKey = openAIConfig.ApiKey;

            // Check if SQLite is enabled (new default)
            if (config.SqliteSettings?.Enabled == true)
            {
                System.Console.WriteLine("Using SQLite backend for storage");

                var projectFolder = folder ?? config.ProjectSettings?.ProjectsFolder;

                if (config.SqliteSettings.EmbeddingsOptional)
                {
                    System.Console.WriteLine("Metadata-only mode (embeddings disabled)");
                    return SqliteStoreProvider.GetMetadataOnlyStoreSetup(
                        config.SqliteSettings,
                        apiKey,
                        projectFolder);
                }
                else
                {
                    return SqliteStoreProvider.GetSqliteStoreSetup(
                        config.SqliteSettings,
                        apiKey,
                        embeddingDimension,
                        projectFolder);
                }
            }

            // Check if Supabase is enabled
            if (config.SupabaseSettings?.Enabled == true)
            {
                System.Console.WriteLine("Using Supabase backend for storage");

                // If folder is provided, use hybrid mode (files + Supabase)
                if (!string.IsNullOrWhiteSpace(folder))
                {
                    System.Console.WriteLine($"Hybrid mode: Files in '{folder}', embeddings in Supabase");
                    return SupabaseStoreProvider.GetHybridStoreSetup(
                        folder,
                        config.SupabaseSettings,
                        apiKey,
                        embeddingDimension);
                }
                else
                {
                    System.Console.WriteLine("Full Supabase mode");
                    return SupabaseStoreProvider.GetSupabaseStoreSetup(
                        config.SupabaseSettings,
                        apiKey,
                        embeddingDimension);
                }
            }

            // Default to file-based storage
            var defaultProjectFolder = folder
                ?? config.ProjectSettings?.ProjectsFolder
                ?? throw new InvalidOperationException("Project folder is required for file-based storage");

            System.Console.WriteLine($"Using file-based storage in '{defaultProjectFolder}'");
            return GetStoreSetup(defaultProjectFolder, openAIConfig);
        }

        /// <summary>
        /// Gets store setup using Supabase backend
        /// </summary>
        /// <param name="supabaseSettings">Supabase configuration</param>
        /// <param name="apiKey">OpenAI API key</param>
        /// <param name="embeddingDimension">Embedding dimension (default 1536)</param>
        /// <returns>Configured StoreSetup</returns>
        public static StoreSetup GetSupabaseStoreSetup(
            SupabaseSettings supabaseSettings,
            string apiKey,
            int embeddingDimension = 1536)
        {
            return SupabaseStoreProvider.GetSupabaseStoreSetup(
                supabaseSettings,
                apiKey,
                embeddingDimension);
        }

        /// <summary>
        /// Gets store setup using hybrid file/Supabase storage
        /// </summary>
        /// <param name="folder">Local folder for file storage</param>
        /// <param name="supabaseSettings">Supabase configuration</param>
        /// <param name="apiKey">OpenAI API key</param>
        /// <param name="embeddingDimension">Embedding dimension (default 1536)</param>
        /// <returns>Configured StoreSetup</returns>
        public static StoreSetup GetHybridStoreSetup(
            string folder,
            SupabaseSettings supabaseSettings,
            string apiKey,
            int embeddingDimension = 1536)
        {
            return SupabaseStoreProvider.GetHybridStoreSetup(
                folder,
                supabaseSettings,
                apiKey,
                embeddingDimension);
        }
    }
}



