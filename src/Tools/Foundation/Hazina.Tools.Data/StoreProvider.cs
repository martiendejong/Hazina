using System.IO;

namespace DevGPT.GenerationTools.Data
{
    public static class StoreProvider
    {
        public static StoreSetup GetStoreSetup(string folder, string apiKey)
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

            var config = new OpenAIConfig(apiKey);
            var llmClient = new OpenAIClientWrapper(config);
            var fileStore = new EmbeddingFileStore(embeddingsPath, llmClient);
            var textStore = new TextFileStore(folder);
            var partStore = new DocumentPartFileStore(partsPath);
            var chunkIndexPath = Path.Combine(partsPath, "chunks.json");
            var chunkStore = new ChunkFileStore(chunkIndexPath);

            var metadataPath = Path.Combine(folder, "metadata");
            var metadataStore = new DocumentMetadataFileStore(metadataPath);

            var store = new DocumentStore(fileStore, textStore, chunkStore, metadataStore, llmClient);

            var setup = new StoreSetup()
            {
                LLMClient = llmClient,
                DocumentPartStore = partStore,
                TextStore = textStore,
                TextEmbeddingStore = fileStore,
                Store = store
            };

            return setup;
        }
    }
}



