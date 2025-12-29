using Hazina.Tools.Data;
using Hazina.Store;
using Hazina.Store.EmbeddingStore;
using HazinaStore.Models;
using System.Security.Cryptography;

namespace Hazina.Demo.Supabase;

/// <summary>
/// Dummy LLM client for demo purposes (generates deterministic embeddings)
/// In production, use OpenAIClientWrapper or another real LLM client
/// </summary>
class DummyLLMClient : ILLMClient
{
    private readonly int _dimension;
    public DummyLLMClient(int dimension) { _dimension = dimension; }

    public Task<Embedding> GenerateEmbedding(string data)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(data));
        var values = new double[_dimension];
        for (int i = 0; i < _dimension; i++)
        {
            int b0 = bytes[i % bytes.Length];
            int b1 = bytes[(i * 3 + 7) % bytes.Length];
            int b2 = bytes[(i * 5 + 13) % bytes.Length];
            int b3 = bytes[(i * 11 + 29) % bytes.Length];
            uint u = (uint)((b0 & 0xFF) | ((b1 & 0xFF) << 8) | ((b2 & 0xFF) << 16) | ((b3 & 0xFF) << 24));
            values[i] = (u / (double)uint.MaxValue);
        }
        return Task.FromResult(new Embedding(values));
    }

    public Task<LLMResponse<HazinaGeneratedImage>> GetImage(string prompt, HazinaChatResponseFormat responseFormat, IToolsContext? toolsContext, List<ImageData>? images, CancellationToken cancel)
        => throw new NotImplementedException();

    public Task<LLMResponse<string>> GetResponse(List<HazinaChatMessage> messages, HazinaChatResponseFormat responseFormat, IToolsContext? toolsContext, List<ImageData>? images, CancellationToken cancel)
        => throw new NotImplementedException();

    public Task<LLMResponse<ResponseType?>> GetResponse<ResponseType>(List<HazinaChatMessage> messages, IToolsContext? toolsContext, List<ImageData>? images, CancellationToken cancel) where ResponseType : ChatResponse<ResponseType>, new()
        => throw new NotImplementedException();

    public Task<LLMResponse<string>> GetResponseStream(List<HazinaChatMessage> messages, Action<string> onChunkReceived, HazinaChatResponseFormat responseFormat, IToolsContext? toolsContext, List<ImageData>? images, CancellationToken cancel)
        => throw new NotImplementedException();

    public Task<LLMResponse<ResponseType?>> GetResponseStream<ResponseType>(List<HazinaChatMessage> messages, Action<string> onChunkReceived, IToolsContext? toolsContext, List<ImageData>? images, CancellationToken cancel) where ResponseType : ChatResponse<ResponseType>, new()
        => throw new NotImplementedException();

    public Task SpeakStream(string text, string voice, Action<byte[]> onAudioChunk, string mimeType, CancellationToken cancel)
        => Task.CompletedTask;
}

class Program
{
    static async Task<int> Main(string[] args)
    {
        Console.WriteLine("=== Hazina Supabase Integration Demo ===\n");

        // Configuration - can be provided via environment variables or command line
        var supabaseUrl = Environment.GetEnvironmentVariable("SUPABASE_URL")
            ?? "https://your-project.supabase.co";

        var supabaseAnonKey = Environment.GetEnvironmentVariable("SUPABASE_ANON_KEY")
            ?? "your-anon-key";

        var connectionString = Environment.GetEnvironmentVariable("SUPABASE_CONNECTION_STRING")
            ?? Environment.GetEnvironmentVariable("SUPABASE_DB_URL")
            ?? "Host=db.your-project.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=your-password";

        // Check if real credentials are provided
        if (supabaseUrl.Contains("your-project") || connectionString.Contains("your-project"))
        {
            Console.WriteLine("⚠️  WARNING: Using placeholder Supabase credentials!");
            Console.WriteLine("\nTo run this demo with real Supabase:");
            Console.WriteLine("  1. Create a project at https://supabase.com");
            Console.WriteLine("  2. Set environment variables:");
            Console.WriteLine("     - SUPABASE_URL=https://your-project.supabase.co");
            Console.WriteLine("     - SUPABASE_ANON_KEY=your-anon-key");
            Console.WriteLine("     - SUPABASE_CONNECTION_STRING=Host=db.your-project.supabase.co;...");
            Console.WriteLine("\nFor now, this demo will show the setup process.\n");

            // Show configuration example
            ShowConfigurationExample();
            return 1;
        }

        Console.WriteLine("Configuration:");
        Console.WriteLine($"  Supabase URL: {supabaseUrl}");
        Console.WriteLine($"  Connection String: {MaskConnectionString(connectionString)}");
        Console.WriteLine();

        // Create Supabase settings
        var supabaseSettings = new SupabaseSettings
        {
            Enabled = true,
            Url = supabaseUrl,
            AnonKey = supabaseAnonKey,
            ConnectionString = connectionString
        };

        // Validate settings
        if (!supabaseSettings.IsValid())
        {
            Console.WriteLine("❌ Invalid Supabase settings. Please check your configuration.");
            return 1;
        }

        try
        {
            // Step 1: Test connection
            Console.WriteLine("Step 1: Testing Supabase connection...");
            var connected = await SupabaseStoreProvider.TestConnectionAsync(connectionString);
            if (!connected)
            {
                Console.WriteLine("❌ Failed to connect to Supabase. Please check your connection string.");
                return 1;
            }
            Console.WriteLine("✅ Connected successfully!\n");

            // Step 2: Initialize schema
            Console.WriteLine("Step 2: Initializing Supabase schema...");
            const int embeddingDimension = 8; // Small dimension for demo
            await SupabaseStoreProvider.InitializeSupabaseSchemaAsync(connectionString, embeddingDimension);
            Console.WriteLine("✅ Schema initialized!\n");

            // Step 3: Create store setup
            Console.WriteLine("Step 3: Creating Supabase store setup...");

            // Using dummy LLM client for demo - in production use real API key
            var llmClient = new DummyLLMClient(embeddingDimension);

            // Create embedding generator for the adapter
            var embeddingGenerator = new LLMEmbeddingGenerator(llmClient, embeddingDimension);

            // Create stores directly
            var pgVectorStore = new PgVectorStore(connectionString, embeddingDimension);

            // Wrap in legacy adapter for backward compatibility
            var embeddingStore = new LegacyTextEmbeddingStoreAdapter(pgVectorStore, embeddingGenerator);

            var textStore = new PostgresTextStore(connectionString);
            var chunkStore = new PostgresChunkStore(connectionString);
            var metadataStore = new PostgresDocumentMetadataStore(connectionString);
            var documentStore = new DocumentStore(embeddingStore, textStore, chunkStore, metadataStore, llmClient)
            {
                Name = "supabase-demo"
            };

            Console.WriteLine("✅ Store setup created!\n");

            // Step 4: Store a document
            Console.WriteLine("Step 4: Storing a test document...");
            var docName = "examples/supabase-demo.txt";
            var content = "This is a test document stored in Supabase using Hazina. " +
                         "Supabase provides PostgreSQL with pgvector for semantic search. " +
                         "This demo shows how to integrate Hazina with Supabase for cloud-based RAG.";

            Console.WriteLine($"  Document: {docName}");
            Console.WriteLine($"  Content: {content.Substring(0, Math.Min(80, content.Length))}...");

            await documentStore.Store(docName, content, split: false);
            Console.WriteLine("✅ Document stored!\n");

            // Step 5: Retrieve the document
            Console.WriteLine("Step 5: Retrieving the document...");
            var retrieved = await documentStore.Get(docName);
            Console.WriteLine($"  Retrieved: {retrieved}");
            Console.WriteLine("✅ Document retrieved successfully!\n");

            // Step 6: Semantic search (store multiple documents first)
            Console.WriteLine("Step 6: Demonstrating semantic search...");

            var docs = new Dictionary<string, string>
            {
                ["docs/postgres.txt"] = "PostgreSQL is a powerful open-source relational database system.",
                ["docs/supabase.txt"] = "Supabase is an open-source Firebase alternative built on PostgreSQL.",
                ["docs/pgvector.txt"] = "pgvector is a PostgreSQL extension for vector similarity search.",
                ["docs/embeddings.txt"] = "Embeddings are numerical representations of text for semantic search.",
                ["docs/hazina.txt"] = "Hazina is a RAG system that supports multiple storage backends."
            };

            foreach (var doc in docs)
            {
                await documentStore.Store(doc.Key, doc.Value, split: false);
                Console.WriteLine($"  Stored: {doc.Key}");
            }

            Console.WriteLine("\n  Searching for documents similar to: 'database system'");
            var queryEmbedding = await llmClient.GenerateEmbedding("database system");
            var searchResults = await pgVectorStore.SearchSimilarAsync(queryEmbedding, topK: 3);

            Console.WriteLine($"  Found {searchResults.Count} results:");
            foreach (var result in searchResults)
            {
                Console.WriteLine($"    - {result.Info.Key} (similarity: {result.Similarity:F4})");
            }
            Console.WriteLine("✅ Semantic search complete!\n");

            // Step 7: Show storage statistics
            Console.WriteLine("Step 7: Storage statistics...");
            Console.WriteLine($"  Total documents stored: {docs.Count + 1}");
            Console.WriteLine($"  Embedding dimension: {embeddingDimension}");
            Console.WriteLine($"  Storage backend: Supabase (PostgreSQL + pgvector)");
            Console.WriteLine($"  Document store: {documentStore.Name}\n");

            Console.WriteLine("=== Demo completed successfully! ===");
            Console.WriteLine("\nNext steps:");
            Console.WriteLine("  1. View your data in Supabase dashboard: Table Editor");
            Console.WriteLine("  2. Query embeddings using SQL: SELECT * FROM embeddings;");
            Console.WriteLine("  3. Integrate with your application using StoreProvider");
            Console.WriteLine("  4. See docs/SUPABASE_SETUP.md for full integration guide");

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ Error: {ex.Message}");
            Console.WriteLine($"\nStack trace:\n{ex.StackTrace}");
            return 1;
        }
    }

    static void ShowConfigurationExample()
    {
        Console.WriteLine("Configuration Example (appsettings.json):");
        Console.WriteLine(@"{
  ""ApiSettings"": {
    ""OpenApiKey"": ""sk-your-openai-api-key""
  },
  ""SupabaseSettings"": {
    ""Enabled"": true,
    ""Url"": ""https://your-project.supabase.co"",
    ""AnonKey"": ""your-anon-key"",
    ""ConnectionString"": ""Host=db.your-project.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=your-password""
  }
}");

        Console.WriteLine("\nCode Example:");
        Console.WriteLine(@"var config = new HazinaStoreConfig
{
    ApiSettings = new ApiSettings { OpenApiKey = ""sk-..."" },
    SupabaseSettings = new SupabaseSettings
    {
        Enabled = true,
        Url = ""https://your-project.supabase.co"",
        AnonKey = ""your-anon-key"",
        ConnectionString = ""Host=db.your-project.supabase.co;...""
    }
};

var storeSetup = StoreProvider.GetStoreSetup(config);
");
    }

    static string MaskConnectionString(string connString)
    {
        // Mask password in connection string for security
        if (connString.Contains("Password="))
        {
            var parts = connString.Split(';');
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Trim().StartsWith("Password=", StringComparison.OrdinalIgnoreCase))
                {
                    parts[i] = "Password=***";
                }
            }
            return string.Join(";", parts);
        }
        return connString;
    }
}
