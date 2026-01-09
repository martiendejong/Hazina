using Hazina.LLMs.OpenAI;
using System.IO;
using Hazina.Store;
using Hazina.Store.EmbeddingStore;
using Hazina.Store.Sqlite;
using HazinaStore.Models;

namespace Hazina.Tools.Data
{
    /// <summary>
    /// Provides store setup for SQLite backend.
    /// SQLite offers single-file database storage with FTS5 full-text search,
    /// ideal for local development, embedded scenarios, and portable projects.
    /// </summary>
    public static class SqliteStoreProvider
    {
        /// <summary>
        /// Creates a store setup using SQLite as the backend
        /// </summary>
        /// <param name="sqliteSettings">SQLite configuration</param>
        /// <param name="apiKey">OpenAI API key for embeddings</param>
        /// <param name="embeddingDimension">Dimension of embeddings (default 1536 for text-embedding-3-small)</param>
        /// <param name="projectFolder">Project folder for resolving relative database paths (optional)</param>
        /// <returns>Configured StoreSetup</returns>
        public static StoreSetup GetSqliteStoreSetup(
            SqliteSettings sqliteSettings,
            string apiKey,
            int embeddingDimension = 1536,
            string? projectFolder = null)
        {
            if (!sqliteSettings.Enabled)
            {
                throw new InvalidOperationException("SQLite backend is not enabled in settings.");
            }

            sqliteSettings.Validate();

            // Get connection string
            var connectionString = sqliteSettings.GetConnectionString(projectFolder);

            // Create LLM client for embeddings
            var config = new OpenAIConfig(apiKey);
            var llmClient = new OpenAIClientWrapper(config);

            // Create embedding generator for the adapter
            var embeddingGenerator = new LLMEmbeddingGenerator(llmClient, embeddingDimension);

            // Create SQLite-based stores
            var sqliteEmbeddingStore = new SqliteEmbeddingStore(connectionString);

            // Wrap in legacy adapter for backward compatibility
            var embeddingStore = new LegacyTextEmbeddingStoreAdapter(sqliteEmbeddingStore, embeddingGenerator);

            var textStore = new SqliteTextStore(connectionString);
            var chunkStore = new SqliteChunkStore(connectionString);
            var metadataStore = new SqliteMetadataStore(connectionString);

            // Create document store
            var documentStore = new DocumentStore(embeddingStore, textStore, chunkStore, metadataStore, llmClient);

            // Create tag scoring components
            // Note: TagRelevanceStore uses temporary file storage
            // Could be migrated to SQLite tables in the future if needed
            var tempPath = Path.Combine(Path.GetTempPath(), "hazina_tag_relevance");
            var tagRelevanceStore = new TagRelevanceFileStore(tempPath);
            var tagScoringService = new LLMTagScoringService(llmClient, tagRelevanceStore);
            var compositeScorer = new DefaultCompositeScorer();

            var setup = new StoreSetup()
            {
                LLMClient = llmClient,
                TextEmbeddingStore = embeddingStore,
                TextStore = textStore,
                Store = documentStore,
                QueryableMetadataStore = metadataStore,
                TagRelevanceStore = tagRelevanceStore,
                TagScoringService = tagScoringService,
                CompositeScorer = compositeScorer,
                DocumentPartStore = null // Not needed for SQLite backend
            };

            System.Console.WriteLine($"SQLite backend initialized: {connectionString}");

            return setup;
        }

        /// <summary>
        /// Creates a store setup using SQLite with optional embeddings (metadata-first mode).
        /// When embeddings are disabled, the system uses only metadata and full-text search.
        /// </summary>
        /// <param name="sqliteSettings">SQLite configuration</param>
        /// <param name="apiKey">OpenAI API key (still required for LLM operations)</param>
        /// <param name="projectFolder">Project folder for resolving relative database paths (optional)</param>
        /// <returns>Configured StoreSetup without embedding generation</returns>
        public static StoreSetup GetMetadataOnlyStoreSetup(
            SqliteSettings sqliteSettings,
            string apiKey,
            string? projectFolder = null)
        {
            if (!sqliteSettings.EmbeddingsOptional)
            {
                throw new InvalidOperationException(
                    "EmbeddingsOptional must be true to use metadata-only mode.");
            }

            sqliteSettings.Validate();

            var connectionString = sqliteSettings.GetConnectionString(projectFolder);

            // Create LLM client (still needed for LLM operations, just not embeddings)
            var config = new OpenAIConfig(apiKey);
            var llmClient = new OpenAIClientWrapper(config);

            // Create stores without embedding support
            var textStore = new SqliteTextStore(connectionString);
            var chunkStore = new SqliteChunkStore(connectionString);
            var metadataStore = new SqliteMetadataStore(connectionString);

            // Create a minimal embedding store that doesn't generate embeddings
            // This is a placeholder to satisfy DocumentStore constructor
            var memoryEmbeddingStore = new EmbeddingMemoryStore();
            var embeddingGenerator = new LLMEmbeddingGenerator(llmClient, 1536);
            var noOpEmbeddingStore = new LegacyTextEmbeddingStoreAdapter(memoryEmbeddingStore, embeddingGenerator);

            var documentStore = new DocumentStore(noOpEmbeddingStore, textStore, chunkStore, metadataStore, llmClient);

            // Create tag scoring components (using temporary file storage)
            var tempPath = Path.Combine(Path.GetTempPath(), "hazina_tag_relevance");
            var tagRelevanceStore = new TagRelevanceFileStore(tempPath);
            var tagScoringService = new LLMTagScoringService(llmClient, tagRelevanceStore);
            var compositeScorer = new DefaultCompositeScorer();

            var setup = new StoreSetup()
            {
                LLMClient = llmClient,
                TextEmbeddingStore = noOpEmbeddingStore,
                TextStore = textStore,
                Store = documentStore,
                QueryableMetadataStore = metadataStore,
                TagRelevanceStore = tagRelevanceStore,
                TagScoringService = tagScoringService,
                CompositeScorer = compositeScorer,
                DocumentPartStore = null
            };

            System.Console.WriteLine($"SQLite backend initialized (metadata-only mode): {connectionString}");

            return setup;
        }
    }
}
