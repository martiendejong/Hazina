public static class StoreFactory
{
    public static ITextStore CreateTextStore(string spec)
    {
        if (spec.StartsWith("postgres:", StringComparison.InvariantCultureIgnoreCase))
        {
            var cs = spec.Substring("postgres:".Length);
            return new PostgresTextStore(cs);
        }
        // default to file path
        return new TextFileStore(spec);
    }

    public static IChunkStore CreateChunkStore(string spec)
    {
        if (string.Equals(spec, "memory:", StringComparison.InvariantCultureIgnoreCase) || string.Equals(spec, "memory", StringComparison.InvariantCultureIgnoreCase))
        {
            return new ChunkMemoryStore();
        }
        if (spec.StartsWith("postgres:", StringComparison.InvariantCultureIgnoreCase))
        {
            var cs = spec.Substring("postgres:".Length);
            return new PostgresChunkStore(cs);
        }
        // default to file path
        return new ChunkFileStore(spec);
    }

    public static IDocumentMetadataStore CreateMetadataStore(string spec)
    {
        if (string.Equals(spec, "memory:", StringComparison.InvariantCultureIgnoreCase) || string.Equals(spec, "memory", StringComparison.InvariantCultureIgnoreCase))
        {
            return new DocumentMetadataMemoryStore();
        }
        if (spec.StartsWith("postgres:", StringComparison.InvariantCultureIgnoreCase))
        {
            var cs = spec.Substring("postgres:".Length);
            return new PostgresDocumentMetadataStore(cs);
        }
        // For file-based stores, create a metadata subfolder
        var metadataPath = System.IO.Path.Combine(spec, "metadata");
        return new DocumentMetadataFileStore(metadataPath);
    }
}

