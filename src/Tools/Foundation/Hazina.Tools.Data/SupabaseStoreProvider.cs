using Hazina.Store;
using Hazina.Store.EmbeddingStore;
using HazinaStore.Models;

namespace Hazina.Tools.Data
{
    /// <summary>
    /// Provides store setup for Supabase backend.
    /// Since Supabase is PostgreSQL-based with pgvector support, this provider
    /// wraps the existing PostgreSQL stores with Supabase configuration.
    /// </summary>
    public static class SupabaseStoreProvider
    {
        /// <summary>
        /// Creates a store setup using Supabase as the backend
        /// </summary>
        /// <param name="supabaseSettings">Supabase configuration</param>
        /// <param name="apiKey">OpenAI API key for embeddings</param>
        /// <param name="embeddingDimension">Dimension of embeddings (default 1536 for text-embedding-3-small)</param>
        /// <returns>Configured StoreSetup</returns>
        public static StoreSetup GetSupabaseStoreSetup(
            SupabaseSettings supabaseSettings,
            string apiKey,
            int embeddingDimension = 1536)
        {
            if (!supabaseSettings.IsValid())
            {
                throw new InvalidOperationException(
                    "Supabase settings are not valid. Ensure Url and AnonKey are configured.");
            }

            // Get connection string
            var connectionString = supabaseSettings.GetConnectionString();

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "Supabase connection string is not configured. Set SupabaseSettings.ConnectionString or SUPABASE_CONNECTION_STRING environment variable.");
            }

            // Create LLM client for embeddings
            var config = new OpenAIConfig(apiKey);
            var llmClient = new OpenAIClientWrapper(config);

            // Create embedding generator for the adapter
            var embeddingGenerator = new LLMEmbeddingGenerator(llmClient, embeddingDimension);

            // Create PostgreSQL-based stores using Supabase connection
            // Supabase is PostgreSQL, so we use the existing PgVector stores
            var pgVectorStore = new PgVectorStore(connectionString, embeddingDimension);

            // Wrap in legacy adapter for backward compatibility
            var embeddingStore = new LegacyTextEmbeddingStoreAdapter(pgVectorStore, embeddingGenerator);

            var textStore = new PostgresTextStore(connectionString);
            var chunkStore = new PostgresChunkStore(connectionString);
            var metadataStore = new PostgresDocumentMetadataStore(connectionString);

            // Create document store
            var documentStore = new DocumentStore(embeddingStore, textStore, chunkStore, metadataStore, llmClient);

            var setup = new StoreSetup()
            {
                LLMClient = llmClient,
                TextEmbeddingStore = embeddingStore,
                TextStore = textStore,
                Store = documentStore,
                DocumentPartStore = null // TODO: Add DocumentPartStore if needed
            };

            return setup;
        }

        /// <summary>
        /// Creates a store setup using Supabase with hybrid file/database storage.
        /// Files are stored locally, but embeddings and indexes use Supabase.
        /// </summary>
        /// <param name="folder">Local folder for file storage</param>
        /// <param name="supabaseSettings">Supabase configuration</param>
        /// <param name="apiKey">OpenAI API key for embeddings</param>
        /// <param name="embeddingDimension">Dimension of embeddings (default 1536)</param>
        /// <returns>Configured StoreSetup with hybrid storage</returns>
        public static StoreSetup GetHybridStoreSetup(
            string folder,
            SupabaseSettings supabaseSettings,
            string apiKey,
            int embeddingDimension = 1536)
        {
            if (!supabaseSettings.IsValid())
            {
                throw new InvalidOperationException(
                    "Supabase settings are not valid. Ensure Url and AnonKey are configured.");
            }

            var connectionString = supabaseSettings.GetConnectionString();

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "Supabase connection string is not configured. Set SupabaseSettings.ConnectionString or SUPABASE_CONNECTION_STRING environment variable.");
            }

            // Create LLM client
            var config = new OpenAIConfig(apiKey);
            var llmClient = new OpenAIClientWrapper(config);

            // Create embedding generator for the adapter
            var embeddingGenerator = new LLMEmbeddingGenerator(llmClient, embeddingDimension);

            // Supabase stores for embeddings and search
            var pgVectorStore = new PgVectorStore(connectionString, embeddingDimension);

            // Wrap in legacy adapter for backward compatibility
            var embeddingStore = new LegacyTextEmbeddingStoreAdapter(pgVectorStore, embeddingGenerator);

            var chunkStore = new PostgresChunkStore(connectionString);
            var metadataStore = new PostgresDocumentMetadataStore(connectionString);

            // File-based stores for text and parts
            var textStore = new TextFileStore(folder);
            var partsPath = Path.Combine(folder, "parts");
            var partStore = new DocumentPartFileStore(partsPath);

            // Create document store with hybrid storage
            var documentStore = new DocumentStore(embeddingStore, textStore, chunkStore, metadataStore, llmClient);

            var setup = new StoreSetup()
            {
                LLMClient = llmClient,
                TextEmbeddingStore = embeddingStore,
                TextStore = textStore,
                DocumentPartStore = partStore,
                Store = documentStore
            };

            return setup;
        }

        /// <summary>
        /// Initializes Supabase database schema (creates tables if they don't exist)
        /// </summary>
        /// <param name="connectionString">Supabase PostgreSQL connection string</param>
        /// <param name="embeddingDimension">Dimension of embeddings (default 1536)</param>
        public static async Task InitializeSupabaseSchemaAsync(string connectionString, int embeddingDimension = 1536)
        {
            using var connection = new Npgsql.NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            // Enable pgvector extension (Supabase has this pre-installed)
            await using (var cmd = new Npgsql.NpgsqlCommand("CREATE EXTENSION IF NOT EXISTS vector;", connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            // Create embeddings table
            var createEmbeddingsTable = $@"
                CREATE TABLE IF NOT EXISTS embeddings (
                    key TEXT PRIMARY KEY,
                    checksum TEXT NOT NULL,
                    embedding vector({embeddingDimension}) NOT NULL,
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                );

                CREATE INDEX IF NOT EXISTS idx_embeddings_embedding ON embeddings
                USING ivfflat (embedding vector_cosine_ops) WITH (lists = 100);
            ";

            await using (var cmd = new Npgsql.NpgsqlCommand(createEmbeddingsTable, connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            // Create document chunks table
            var createChunksTable = @"
                CREATE TABLE IF NOT EXISTS document_chunks (
                    name TEXT NOT NULL,
                    chunk_key TEXT NOT NULL,
                    PRIMARY KEY (name, chunk_key)
                );

                CREATE INDEX IF NOT EXISTS idx_document_chunks_name ON document_chunks(name);
            ";

            await using (var cmd = new Npgsql.NpgsqlCommand(createChunksTable, connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            // Create document metadata table
            var createMetadataTable = @"
                CREATE TABLE IF NOT EXISTS document_metadata (
                    name TEXT PRIMARY KEY,
                    metadata JSONB NOT NULL,
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                );
            ";

            await using (var cmd = new Npgsql.NpgsqlCommand(createMetadataTable, connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            // Create text store table
            var createTextTable = @"
                CREATE TABLE IF NOT EXISTS texts (
                    key TEXT PRIMARY KEY,
                    content TEXT NOT NULL,
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                );
            ";

            await using (var cmd = new Npgsql.NpgsqlCommand(createTextTable, connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            Console.WriteLine("Supabase schema initialized successfully.");
        }

        /// <summary>
        /// Tests the Supabase connection
        /// </summary>
        /// <param name="connectionString">Supabase PostgreSQL connection string</param>
        /// <returns>True if connection is successful</returns>
        public static async Task<bool> TestConnectionAsync(string connectionString)
        {
            try
            {
                using var connection = new Npgsql.NpgsqlConnection(connectionString);
                await connection.OpenAsync();

                await using var cmd = new Npgsql.NpgsqlCommand("SELECT version();", connection);
                var version = await cmd.ExecuteScalarAsync();

                Console.WriteLine($"Connected to Supabase PostgreSQL: {version}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to connect to Supabase: {ex.Message}");
                return false;
            }
        }
    }
}
