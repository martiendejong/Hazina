using System.Security.Cryptography;

class Program
{
    private static void PrintUsage()
    {
        Console.WriteLine("Usage: FolderToPostgres <folder> [--pattern <glob>] [--recurse] [--no-split]");
        Console.WriteLine("Env: DEVGPT_PG_CONN=Postgres connection string (default local)\n      PROVIDER=openai|hf|dummy (auto by API keys)\n      OPENAI_API_KEY / DEVGPT_OPENAI_API_KEY\n      OPENAI_EMBED_MODEL (e.g. text-embedding-3-small|large)\n      HUGGINGFACE_API_KEY / HF_API_KEY\n      HUGGINGFACE_ENDPOINT (default https://api-inference.huggingface.co)\n      DEVGPT_EMBED_DIM=embedding dimension override");
    }

    static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || args.Contains("-h") || args.Contains("--help"))
        {
            PrintUsage();
            return 1;
        }

        var folder = args[0];
        if (!Directory.Exists(folder))
        {
            Console.Error.WriteLine($"Folder not found: {folder}");
            return 2;
        }

        string pattern = "*";
        bool recurse = false;
        bool split = true;
        for (int i = 1; i < args.Length; i++)
        {
            if (args[i] == "--pattern" && i + 1 < args.Length) { pattern = args[++i]; continue; }
            if (args[i] == "--recurse") { recurse = true; continue; }
            if (args[i] == "--no-split") { split = false; continue; }
        }

        var conn = Environment.GetEnvironmentVariable("DEVGPT_PG_CONN")
                  ?? "Host=localhost;Username=postgres;Password=postgres;Database=devgpt";
        // Choose embedding provider and sensible default dimension
        var llm = CreateEmbeddingClient(out var defaultDimension);
        var dimStr = Environment.GetEnvironmentVariable("DEVGPT_EMBED_DIM");
        int dimension = defaultDimension;
        if (!string.IsNullOrWhiteSpace(dimStr) && int.TryParse(dimStr, out var d) && d > 0) dimension = d;

        Console.WriteLine($"Connecting to Postgres (pgvector dim {dimension})...");

        var embeddingStore = new PgVectorTextEmbeddingStore(conn, llm, dimension);
        var textStore = new PostgresTextStore(conn);
        var chunkStore = new PostgresChunkStore(conn);
        var metadataStore = new PostgresDocumentMetadataStore(conn);
        var store = new DocumentStore(embeddingStore, textStore, chunkStore, metadataStore, llm) { Name = "folder-loader" };

        var searchOption = recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var files = Directory.EnumerateFiles(folder, pattern, searchOption).ToList();
        if (files.Count == 0)
        {
            Console.WriteLine("No files matched.");
            return 0;
        }

        Console.WriteLine($"Found {files.Count} files. Ingesting...");

        int ok = 0, fail = 0;
        foreach (var file in files)
        {
            try
            {
                // Compute store key as relative path with forward slashes
                var rel = Path.GetRelativePath(folder, file);
                var key = rel.Replace('\\', '/');
                string content;
                try
                {
                    content = File.ReadAllText(file);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Skip (read error): {rel} -> {ex.Message}");
                    fail++;
                    continue;
                }

                await store.Store(key, content, split: split);
                Console.WriteLine($"Stored: {key}");
                ok++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error storing '{file}': {ex.Message}");
                fail++;
            }
        }

        Console.WriteLine($"Done. Stored={ok}, Failed={fail}");
        return fail == 0 ? 0 : 3;
    }

    private static ILLMClient CreateEmbeddingClient(out int defaultDimension)
    {
        string? provider = Environment.GetEnvironmentVariable("PROVIDER")?.ToLowerInvariant();

        // OpenAI
        var openAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
                        ?? Environment.GetEnvironmentVariable("DEVGPT_OPENAI_API_KEY");
        if (provider == "openai" || (!string.IsNullOrWhiteSpace(openAiKey) && provider != "hf"))
        {
            var embedModel = Environment.GetEnvironmentVariable("OPENAI_EMBED_MODEL") ?? "text-embedding-3-small";
            var chatModel = Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "gpt-4o-mini";
            var imageModel = Environment.GetEnvironmentVariable("OPENAI_IMAGE_MODEL") ?? "gpt-image-1";
            var config = new OpenAIConfig(openAiKey ?? string.Empty, embedModel, chatModel, imageModel);
            defaultDimension = GetOpenAiDefaultDim(embedModel);
            Console.WriteLine($"Provider: OpenAI ({embedModel}, dim {defaultDimension})");
            return new OpenAIClientWrapper(config);
        }

        // HuggingFace support not included in this build to avoid extra package feeds.
        // Add DevGPT.HuggingFace project reference and enable here if desired.

        // Fallback: dummy
        defaultDimension = 8;
        Console.WriteLine("Provider: Dummy deterministic embeddings (for local testing)");
        return new DummyLLMClient(defaultDimension);
    }

    private static int GetOpenAiDefaultDim(string embedModel)
    {
        var m = embedModel.ToLowerInvariant();
        if (m.Contains("3-large")) return 3072;
        // ada-002 and 3-small both 1536
        return 1536;
    }
}

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

