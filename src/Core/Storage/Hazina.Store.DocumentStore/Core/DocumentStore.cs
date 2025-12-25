using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Hazina.Store.EmbeddingStore;

public class DocumentStore : IDocumentStore
{
    public string Name { get; set; } = Guid.NewGuid().ToString();

    public EmbeddingMatcher EmbeddingMatcher = new EmbeddingMatcher();
    public ITextEmbeddingStore EmbeddingStore { get; set; }
    public IChunkStore ChunkStore { get; set; }
    public DocumentSplitter DocumentSplitter = new DocumentSplitter();
    public ITextStore TextStore { get; set; }
    public ILLMClient LLMClient { get; set; }
    public IDocumentMetadataStore MetadataStore { get; set; }
    public BinaryDocumentProcessor BinaryProcessor { get; set; }

    // New architecture components (optional for backward compatibility)
    private readonly IVectorSearchStore? _vectorSearchStore;
    private readonly IEmbeddingGenerator? _embeddingGenerator;

    // Legacy constructor for backward compatibility
    public DocumentStore(ITextEmbeddingStore embeddingStore, ITextStore textStore, IChunkStore chunkStore, IDocumentMetadataStore metadataStore, ILLMClient llmClient)
        : this(embeddingStore, textStore, chunkStore, metadataStore, llmClient, null, null)
    {
    }

    // New constructor with refactored architecture support
    public DocumentStore(
        ITextEmbeddingStore embeddingStore,
        ITextStore textStore,
        IChunkStore chunkStore,
        IDocumentMetadataStore metadataStore,
        ILLMClient llmClient,
        IVectorSearchStore? vectorSearchStore,
        IEmbeddingGenerator? embeddingGenerator)
    {
        LLMClient = llmClient;
        EmbeddingStore = embeddingStore;
        TextStore = textStore;
        ChunkStore = chunkStore;
        MetadataStore = metadataStore;
        BinaryProcessor = new BinaryDocumentProcessor(llmClient);
        _vectorSearchStore = vectorSearchStore;
        _embeddingGenerator = embeddingGenerator;
    }


    public async Task UpdateEmbeddings()
    {
        foreach (var embedding in EmbeddingStore.Embeddings) await Embed(embedding.Key);
    }

    public string Sanitize(string name)
    {
        return name;
    }

    public async Task<bool> Embed(string name)
    {
        name = Sanitize(name);
        List<string> partKeys = [name];
        var content = await TextStore.Get(name);
        if (content == null)
        {
            await EmbeddingStore.RemoveEmbedding(name);
            return false;
        }
        var embed = EmbeddingMatcher.CutOffQuery(content);
        return await EmbeddingStore.StoreEmbedding(name, embed);
    }

    public async Task<bool> Store(string name, string content, Dictionary<string, string>? metadata = null, bool split = true)
    {
        name = Sanitize(name);

        // Store metadata
        var docMetadata = new DocumentMetadata
        {
            Id = name,
            OriginalPath = "",
            MimeType = "text/plain",
            Size = content.Length,
            Created = DateTime.UtcNow,
            CustomMetadata = metadata ?? new Dictionary<string, string>(),
            IsBinary = false,
            Summary = null
        };
        await MetadataStore.Store(name, docMetadata);

        var chunkKeys = new List<string>();

        // First, store metadata as a searchable chunk
        var metadataKey = $"{name}.metadata";
        var metadataChunk = docMetadata.ToChunkText();
        await EmbeddingStore.StoreEmbedding(metadataKey, metadataChunk);
        await TextStore.Store(metadataKey, metadataChunk);
        chunkKeys.Add(metadataKey);

        // Then store content chunks
        var chunks = split ? DocumentSplitter.SplitDocument(content) : [EmbeddingMatcher.CutOffQuery(content)];
        if (chunks.Count == 1)
        {
            await EmbeddingStore.StoreEmbedding(name, content);
            await TextStore.Store(name, content);
            chunkKeys.Add(name);
        }
        else
        {
            for (var i = 0; i < chunks.Count; ++i)
            {
                var chunkKey = $"{name} chunk {i}";
                await EmbeddingStore.StoreEmbedding(chunkKey, chunks[i]);
                await TextStore.Store(chunkKey, chunks[i]);
                chunkKeys.Add(chunkKey);
            }
        }
        await ChunkStore.Store(name, chunkKeys);
        return true;
    }

    public async Task<bool> Store(string name, byte[] content, string mimeType, Dictionary<string, string>? metadata = null)
    {
        name = Sanitize(name);

        // Process binary content
        var textContent = await BinaryProcessor.ExtractText(content, mimeType);
        var summary = BinaryProcessor.IsBinary(mimeType) ? await BinaryProcessor.GenerateSummary(content, mimeType) : null;

        // Store metadata
        var docMetadata = new DocumentMetadata
        {
            Id = name,
            OriginalPath = "",
            MimeType = mimeType,
            Size = content.Length,
            Created = DateTime.UtcNow,
            CustomMetadata = metadata ?? new Dictionary<string, string>(),
            IsBinary = BinaryProcessor.IsBinary(mimeType),
            Summary = summary
        };
        await MetadataStore.Store(name, docMetadata);

        var chunkKeys = new List<string>();

        // Store metadata as a searchable chunk
        var metadataKey = $"{name}.metadata";
        var metadataChunk = docMetadata.ToChunkText();
        await EmbeddingStore.StoreEmbedding(metadataKey, metadataChunk);
        await TextStore.Store(metadataKey, metadataChunk);
        chunkKeys.Add(metadataKey);

        // Store the extracted/summarized text content
        var contentToStore = string.IsNullOrEmpty(summary) ? textContent : $"{summary}\n\nExtracted content:\n{textContent}";
        var chunks = DocumentSplitter.SplitDocument(contentToStore);

        if (chunks.Count == 1)
        {
            await EmbeddingStore.StoreEmbedding(name, contentToStore);
            await TextStore.Store(name, contentToStore);
            chunkKeys.Add(name);
        }
        else
        {
            for (var i = 0; i < chunks.Count; ++i)
            {
                var chunkKey = $"{name} chunk {i}";
                await EmbeddingStore.StoreEmbedding(chunkKey, chunks[i]);
                await TextStore.Store(chunkKey, chunks[i]);
                chunkKeys.Add(chunkKey);
            }
        }

        await ChunkStore.Store(name, chunkKeys);
        return true;
    }

    public async Task<bool> StoreFromFile(string name, string filePath, Dictionary<string, string>? metadata = null)
    {
        if (!File.Exists(filePath))
        {
            return false;
        }

        name = Sanitize(name);

        // Detect MIME type
        var mimeType = BinaryProcessor.DetectMimeType(filePath);
        var fileInfo = new FileInfo(filePath);

        // Read file content
        var content = await File.ReadAllBytesAsync(filePath);

        // Update metadata with file info
        var updatedMetadata = metadata ?? new Dictionary<string, string>();
        updatedMetadata["OriginalPath"] = filePath;
        updatedMetadata["FileName"] = fileInfo.Name;
        updatedMetadata["FileExtension"] = fileInfo.Extension;

        // Determine if binary or text
        if (BinaryProcessor.IsBinary(mimeType))
        {
            return await Store(name, content, mimeType, updatedMetadata);
        }
        else
        {
            var textContent = await File.ReadAllTextAsync(filePath);
            return await Store(name, textContent, updatedMetadata, true);
        }
    }

    public async Task<DocumentMetadata?> GetMetadata(string id)
    {
        id = Sanitize(id);
        return await MetadataStore.Get(id);
    }

    public async Task<bool> Remove(string name)
    {
        name = Sanitize(name);

        // Remove metadata
        await MetadataStore.Remove(name);

        // Remove all embeddings and text
        await EmbeddingStore.RemoveEmbedding(name);
        await TextStore.Remove(name);

        var chunks = await ChunkStore.Get(name);
        foreach (var chunk in chunks)
        {
            await EmbeddingStore.RemoveEmbedding(chunk);
            await TextStore.Remove(chunk);
        }

        return true;
    }

    public async Task<bool> Move(string name, string newName, bool split = true)
    {
        var content = await TextStore.Get(name);
        if (content == null) return false;

        // Get and update metadata
        var metadata = await MetadataStore.Get(name);
        var customMetadata = metadata?.CustomMetadata ?? new Dictionary<string, string>();

        await Store(newName, content, customMetadata, split);
        await Remove(name);
        return true;
    }

    public async Task<List<TreeNode<string>>> Tree()
    {
        var names = await ChunkStore.ListNames();
        return TreeMaker.GetTree(names.Select(n => n).ToList());
    }

    public async Task<List<string>> List(string folder = "", bool recursive = false)
    {
        var names = (await ChunkStore.ListNames()).ToList();
        if (string.IsNullOrWhiteSpace(folder))
        {
            if (recursive) return names;
            return names.Where(p => !p.Contains('/') && !p.Contains('\\')).ToList();
        }
        // Normalize folder separators for matching
        var f1 = folder.Replace('\\','/').TrimEnd('/');
        return names.Where(p => {
            var np = p.Replace('\\','/');
            if (!np.StartsWith(f1)) return false;
            if (recursive) return true;
            if (np.Length <= f1.Length) return false;
            var rest = np.Substring(f1.Length).TrimStart('/');
            return !rest.Contains('/');
        }).ToList();
    }

    public async Task<List<string>> RelevantItems(string query)
    {
        var cutOffQuery = EmbeddingMatcher.CutOffQuery(query);

        // Use new architecture if available
        if (_vectorSearchStore != null && _embeddingGenerator != null)
        {
            var queryEmbedding = await _embeddingGenerator.GenerateAsync(cutOffQuery);
            var scoredResults = await _vectorSearchStore.SearchSimilarAsync(queryEmbedding, topK: 1000, minSimilarity: 0.0);
            var r = scoredResults.Select(scored => new RelevantEmbedding
            {
                Similarity = scored.Similarity,
                StoreName = Name,
                Document = scored.Info,
                GetText = async (string a) => await TextStore.Get(a)
            }).ToList();
            var items = await EmbeddingMatcher.TakeTop(r);
            return items;
        }

        // Fall back to old architecture
        var embed = await LLMClient.GenerateEmbedding(cutOffQuery);
        var list = EmbeddingMatcher.GetEmbeddingsWithSimilarity(embed, EmbeddingStore.Embeddings);
        var legacyResults = list.Select(item => new RelevantEmbedding { Similarity = item.similarity, StoreName = Name, Document = item.document, GetText = async (string a) => await TextStore.Get(a) }).ToList();
        var legacyItems = await EmbeddingMatcher.TakeTop(legacyResults);
        return legacyItems;
    }

    public async Task<List<RelevantEmbedding>> Embeddings(string query)
    {
        var cutOffQuery = EmbeddingMatcher.CutOffQuery(query);

        // Use new architecture if available (native vector search)
        if (_vectorSearchStore != null && _embeddingGenerator != null)
        {
            // Generate query embedding using the new architecture
            var queryEmbedding = await _embeddingGenerator.GenerateAsync(cutOffQuery);

            // Perform native vector search (10-100x faster for large datasets)
            var scoredResults = await _vectorSearchStore.SearchSimilarAsync(
                queryEmbedding,
                topK: 1000, // Get many results, will be filtered by token limit later
                minSimilarity: 0.0
            );

            // Convert to legacy RelevantEmbedding format
            var r = new List<RelevantEmbedding>();
            foreach (var scored in scoredResults)
            {
                var parentKey = await ChunkStore.GetParentDocument(scored.Info.Key);
                r.Add(new RelevantEmbedding
                {
                    Similarity = scored.Similarity,
                    StoreName = Name,
                    Document = scored.Info,
                    ParentDocumentKey = parentKey,
                    GetText = async (string a) => await TextStore.Get(a)
                });
            }

            return r;
        }

        // Fall back to old architecture (in-memory search)
        var embed = await LLMClient.GenerateEmbedding(cutOffQuery);
        var list = EmbeddingMatcher.GetEmbeddingsWithSimilarity(embed, EmbeddingStore.Embeddings);
        var legacyResults = new List<RelevantEmbedding>();

        foreach (var item in list)
        {
            var chunkKey = item.document.Key;
            var parentKey = await ChunkStore.GetParentDocument(chunkKey);

            legacyResults.Add(new RelevantEmbedding
            {
                Similarity = item.similarity,
                StoreName = Name,
                Document = item.document,
                ParentDocumentKey = parentKey,
                GetText = async (string a) => await TextStore.Get(a)
            });
        }

        return legacyResults;
    }

    public string GetPath(string name)
    {
        return TextStore.GetPath(Sanitize(name));
    }

    public async Task<string> Get(string name)
    {
        return await TextStore.Get(Sanitize(name));
    }

    public async Task<string?> GetChunk(string chunkKey)
    {
        chunkKey = Sanitize(chunkKey);
        return await TextStore.Get(chunkKey);
    }

    public async Task<DocumentWithChunks?> GetDocumentWithChunks(string key)
    {
        key = Sanitize(key);

        // Get the document content
        var content = await TextStore.Get(key);
        if (content == null)
        {
            return null;
        }

        // Get metadata
        var metadata = await MetadataStore.Get(key);

        // Get chunk keys
        var chunkKeys = (await ChunkStore.Get(key)).ToList();

        return new DocumentWithChunks
        {
            Key = key,
            Content = content,
            Metadata = metadata,
            ChunkKeys = chunkKeys
        };
    }
}
