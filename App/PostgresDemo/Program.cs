using System.Security.Cryptography;

class DummyLLMClient : ILLMClient
{
    private readonly int _dimension;
    public DummyLLMClient(int dimension) { _dimension = dimension; }

    public Task<Embedding> GenerateEmbedding(string data)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(data));
        // Expand to desired dimension deterministically
        var values = new double[_dimension];
        for (int i = 0; i < _dimension; i++)
        {
            // Map 4 bytes chunk to a double in [0,1)
            int b0 = bytes[i % bytes.Length];
            int b1 = bytes[(i * 3 + 7) % bytes.Length];
            int b2 = bytes[(i * 5 + 13) % bytes.Length];
            int b3 = bytes[(i * 11 + 29) % bytes.Length];
            uint u = (uint)((b0 & 0xFF) | ((b1 & 0xFF) << 8) | ((b2 & 0xFF) << 16) | ((b3 & 0xFF) << 24));
            values[i] = (u / (double)uint.MaxValue);
        }
        return Task.FromResult(new Embedding(values));
    }

    public Task<LLMResponse<DevGPTGeneratedImage>> GetImage(string prompt, DevGPTChatResponseFormat responseFormat, IToolsContext? toolsContext, List<ImageData>? images, CancellationToken cancel)
        => throw new NotImplementedException();

    public Task<LLMResponse<string>> GetResponse(List<DevGPTChatMessage> messages, DevGPTChatResponseFormat responseFormat, IToolsContext? toolsContext, List<ImageData>? images, CancellationToken cancel)
        => throw new NotImplementedException();

    public Task<LLMResponse<ResponseType?>> GetResponse<ResponseType>(List<DevGPTChatMessage> messages, IToolsContext? toolsContext, List<ImageData>? images, CancellationToken cancel) where ResponseType : ChatResponse<ResponseType>, new()
        => throw new NotImplementedException();

    public Task<LLMResponse<string>> GetResponseStream(List<DevGPTChatMessage> messages, Action<string> onChunkReceived, DevGPTChatResponseFormat responseFormat, IToolsContext? toolsContext, List<ImageData>? images, CancellationToken cancel)
        => throw new NotImplementedException();

    public Task<LLMResponse<ResponseType?>> GetResponseStream<ResponseType>(List<DevGPTChatMessage> messages, Action<string> onChunkReceived, IToolsContext? toolsContext, List<ImageData>? images, CancellationToken cancel) where ResponseType : ChatResponse<ResponseType>, new()
        => throw new NotImplementedException();

    public Task SpeakStream(string text, string voice, Action<byte[]> onAudioChunk, string mimeType, CancellationToken cancel)
        => Task.CompletedTask;
}

class Program
{
    static async Task<int> Main(string[] args)
    {
        // Configure: Use env var DEVGPT_PG_CONN or fallback to local default
        var conn = Environment.GetEnvironmentVariable("DEVGPT_PG_CONN")
                  ?? "Host=localhost;Username=postgres;Password=postgres;Database=devgpt";

        // Keep the vector small for the demo so pgvector deployment is easy
        const int dimension = 8;

        // Instantiate stores
        var llm = new DummyLLMClient(dimension);
        var embeddingStore = new PgVectorTextEmbeddingStore(conn, llm, dimension);
        var textStore = new PostgresTextStore(conn);
        var chunkStore = new PostgresChunkStore(conn);
        var metadataStore = new PostgresDocumentMetadataStore(conn);
        var store = new DocumentStore(embeddingStore, textStore, chunkStore, metadataStore, llm) { Name = "postgres-demo" };

        // Example document
        var docName = "examples/demo.txt";
        var content = "Dit is een voorbeeldtekst opgeslagen in PostgreSQL met pgvector embeddings.";

        Console.WriteLine($"Storing '{docName}' to Postgres...");
        await store.Store(docName, content, split: false);

        // Retrieve to prove it works
        var fetched = await store.Get(docName);
        Console.WriteLine("Fetched content:");
        Console.WriteLine(fetched);

        // Show where it lives
        Console.WriteLine($"Path: {store.GetPath(docName)}");
        Console.WriteLine("Done.");
        return 0;
    }
}


