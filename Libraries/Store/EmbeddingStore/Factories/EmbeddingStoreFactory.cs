using DevGPT.Store.EmbeddingStore;

public static class EmbeddingStoreFactory
{
    // Legacy registry for backward compatibility
    private static readonly Dictionary<string, Func<string, ILLMClient, ITextEmbeddingStore>> _registry
        = new(StringComparer.InvariantCultureIgnoreCase)
        {
            { "file", (spec, provider) => new EmbeddingFileStore(spec, provider) },
            { "memory", (spec, provider) => new TextEmbeddingMemoryStore(provider) },
            { "sqlite", (spec, provider) => new SqliteTextEmbeddingStore(spec, provider) },
            { "pgvector", (spec, provider) => new PgVectorTextEmbeddingStore(spec, provider) },
            { "faiss", (spec, provider) => new FaissTextEmbeddingStore(spec, provider) },
        };

    // New registry for refactored architecture
    private static readonly Dictionary<string, Func<string, IEmbeddingGenerator, IEmbeddingStore>> _newRegistry
        = new(StringComparer.InvariantCultureIgnoreCase)
        {
            { "file", (spec, generator) => new EmbeddingJsonFileStore(spec) },
            { "memory", (spec, generator) => new EmbeddingMemoryStore() },
            { "pgvector", (spec, generator) => new PgVectorStore(spec, generator.Dimensions) },
        };

    /// <summary>
    /// Registers a legacy factory for backward compatibility.
    /// </summary>
    public static void Register(string scheme, Func<string, ILLMClient, ITextEmbeddingStore> factory)
    {
        _registry[scheme] = factory;
    }

    /// <summary>
    /// Registers a new factory using the refactored architecture.
    /// </summary>
    public static void RegisterNew(string scheme, Func<string, IEmbeddingGenerator, IEmbeddingStore> factory)
    {
        _newRegistry[scheme] = factory;
    }

    /// <summary>
    /// Creates a legacy ITextEmbeddingStore (backward compatible).
    /// </summary>
    [Obsolete("Use CreateNew() with IEmbeddingGenerator for better separation of concerns")]
    public static ITextEmbeddingStore CreateFromSpec(string embeddingsSpec, ILLMClient provider)
    {
        if (string.IsNullOrWhiteSpace(embeddingsSpec))
            throw new ArgumentException("embeddingsSpec must be provided", nameof(embeddingsSpec));

        // Detect explicit scheme prefixes. Defaults to file store for normal paths (e.g., C:\...)
        if (embeddingsSpec.StartsWith("sqlite:", StringComparison.InvariantCultureIgnoreCase))
        {
            var spec = embeddingsSpec.Substring("sqlite:".Length);
            return _registry["sqlite"].Invoke(spec, provider);
        }
        if (embeddingsSpec.StartsWith("pgvector:", StringComparison.InvariantCultureIgnoreCase))
        {
            var spec = embeddingsSpec.Substring("pgvector:".Length);
            return _registry["pgvector"].Invoke(spec, provider);
        }
        if (embeddingsSpec.StartsWith("faiss:", StringComparison.InvariantCultureIgnoreCase))
        {
            var spec = embeddingsSpec.Substring("faiss:".Length);
            return _registry["faiss"].Invoke(spec, provider);
        }

        // Default to file-backed store
        return _registry["file"].Invoke(embeddingsSpec, provider);
    }

    /// <summary>
    /// Creates a new IEmbeddingStore using the refactored architecture.
    /// Returns both the store and vector search capability if supported.
    /// </summary>
    public static (IEmbeddingStore store, IVectorSearchStore? vectorSearch) CreateNew(
        string embeddingsSpec,
        IEmbeddingGenerator generator)
    {
        if (string.IsNullOrWhiteSpace(embeddingsSpec))
            throw new ArgumentException("embeddingsSpec must be provided", nameof(embeddingsSpec));

        if (generator == null)
            throw new ArgumentNullException(nameof(generator));

        string scheme;
        string spec;

        // Parse scheme
        if (embeddingsSpec.StartsWith("pgvector:", StringComparison.InvariantCultureIgnoreCase))
        {
            scheme = "pgvector";
            spec = embeddingsSpec.Substring("pgvector:".Length);
        }
        else if (embeddingsSpec.StartsWith("memory:", StringComparison.InvariantCultureIgnoreCase))
        {
            scheme = "memory";
            spec = embeddingsSpec.Substring("memory:".Length);
        }
        else if (embeddingsSpec.StartsWith("file:", StringComparison.InvariantCultureIgnoreCase))
        {
            scheme = "file";
            spec = embeddingsSpec.Substring("file:".Length);
        }
        else
        {
            // Default to file
            scheme = "file";
            spec = embeddingsSpec;
        }

        if (!_newRegistry.TryGetValue(scheme, out var factory))
        {
            throw new NotSupportedException($"Scheme '{scheme}' is not registered in the new architecture. Supported: {string.Join(", ", _newRegistry.Keys)}");
        }

        var store = factory(spec, generator);

        // Return vector search capability if supported
        var vectorSearch = store as IVectorSearchStore;

        return (store, vectorSearch);
    }

    /// <summary>
    /// Creates a legacy adapter from the new architecture for backward compatibility.
    /// Use this when you need ITextEmbeddingStore but want to use the new architecture.
    /// </summary>
    public static ITextEmbeddingStore CreateLegacyAdapter(
        string embeddingsSpec,
        ILLMClient llmClient,
        int dimensions = 1536)
    {
        var generator = new LLMEmbeddingGenerator(llmClient, dimensions);
        var (store, _) = CreateNew(embeddingsSpec, generator);
        return new LegacyTextEmbeddingStoreAdapter(store, generator);
    }
}

