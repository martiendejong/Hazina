using System.CommandLine;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hazina.CLI.Infrastructure;
using Hazina.CLI.Models;
using Hazina.LLMs;
using Spectre.Console;
using Microsoft.Extensions.FileSystemGlobbing;

namespace Hazina.CLI.Commands;

/// <summary>
/// RAG command with full CRUD operations for document stores
/// </summary>
public static class RagCommand
{
    public static Command Create()
    {
        var command = new Command("rag", "RAG operations - query, index, and manage document stores");

        // Subcommands
        command.AddCommand(CreateInitCommand());
        command.AddCommand(CreateIndexCommand());
        command.AddCommand(CreateQueryCommand());
        command.AddCommand(CreateStatusCommand());
        command.AddCommand(CreateListStoresCommand());
        command.AddCommand(CreateSyncCommand());

        // Phase 3: Specialized query commands
        command.AddCommand(CreateFindSymbolCommand());
        command.AddCommand(CreateFindCallersCommand());
        command.AddCommand(CreateFindReferencesCommand());
        command.AddCommand(CreateExpandContextCommand());

        return command;
    }

    #region Init Command

    private static Command CreateInitCommand()
    {
        var storeNameArg = new Argument<string>("store-name", "Name for the new store");
        var pathOption = new Option<string?>("--path", "Path for storage");
        var embeddingModelOption = new Option<string>("--embedding-model", () => "text-embedding-3-small", "Embedding model to use");

        var cmd = new Command("init", "Initialize a new document store")
        {
            storeNameArg,
            pathOption,
            embeddingModelOption
        };

        cmd.SetHandler(async (string storeName, string? path, string embeddingModel) =>
        {
            var registry = StoreRegistry.Load();

            if (registry.Stores.ContainsKey(storeName))
            {
                AnsiConsole.MarkupLine($"[yellow]Warning:[/] Store '{storeName}' already exists.");
                return;
            }

            // Determine path
            if (string.IsNullOrEmpty(path))
            {
                path = Path.Combine(Environment.CurrentDirectory, ".hazina");
            }

            var config = new CliStoreConfig
            {
                Path = path,
                EmbeddingModel = embeddingModel,
                Created = DateTime.UtcNow
            };

            // Create directories
            Directory.CreateDirectory(Path.Combine(path, "vectors"));
            Directory.CreateDirectory(Path.Combine(path, "documents"));

            registry.Stores[storeName] = config;
            registry.Save();

            AnsiConsole.MarkupLine($"[green]✓[/] Store '[bold]{storeName}[/]' initialized");
            AnsiConsole.MarkupLine($"  Path: {path}");
            AnsiConsole.MarkupLine($"  Embedding model: {embeddingModel}");

        }, storeNameArg, pathOption, embeddingModelOption);

        return cmd;
    }

    #endregion

    #region Index Command

    private static Command CreateIndexCommand()
    {
        var pathArg = new Argument<string>("path", "Glob pattern for files to index (e.g., **/*.cs)");
        var storeOption = new Option<string>("--store", "Store name") { IsRequired = true };
        var tagsOption = new Option<string[]?>("--tags", "Tags to apply to indexed files");
        var chunkSizeOption = new Option<int>("--chunk-size", () => 500, "Chunk size in characters");

        var cmd = new Command("index", "Bulk index files into a store")
        {
            pathArg,
            storeOption,
            tagsOption,
            chunkSizeOption
        };

        cmd.SetHandler(async (string pathPattern, string storeName, string[]? tags, int chunkSize) =>
        {
            var registry = StoreRegistry.Load();
            var storeConfig = registry.Stores.GetValueOrDefault(storeName);

            if (storeConfig == null)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Store '{storeName}' not found. Run 'hazina rag init {storeName}' first.");
                return;
            }

            // Find matching files
            var basePath = Environment.CurrentDirectory;
            var matcher = new Matcher();
            matcher.AddInclude(pathPattern);
            var matches = matcher.GetResultsInFullPath(basePath).ToList();

            if (matches.Count == 0)
            {
                AnsiConsole.MarkupLine($"[yellow]No files matched pattern:[/] {pathPattern}");
                return;
            }

            AnsiConsole.MarkupLine($"Found [bold]{matches.Count}[/] files to index");

            // Get embedding client
            var client = HazinaConfig.GetLLMClient();

            // Create embedding store
            var vectorStorePath = Path.Combine(storeConfig.Path, "vectors", "embeddings.json");
            var embeddingStore = EmbeddingStoreFactory.CreateFromSpec(vectorStorePath, client);

            // Load existing chunk metadata (rich format)
            var chunksFilePath = Path.Combine(storeConfig.Path, "documents", "chunks.json");
            var chunkMetadataStore = LoadChunkMetadata(chunksFilePath);

            // Initialize symbol graph for multi-hop
            var symbolGraphPath = Path.Combine(storeConfig.Path, "documents", "symbol-graph.json");
            var symbolGraph = LoadSymbolGraph(symbolGraphPath);

            await AnsiConsole.Progress()
                .StartAsync(async ctx =>
                {
                    var task = ctx.AddTask("[green]Indexing files[/]", maxValue: matches.Count);
                    int indexed = 0;
                    int failed = 0;

                    foreach (var filePath in matches)
                    {
                        try
                        {
                            // Extract text
                            var text = await ExtractTextSimple(filePath);

                            if (string.IsNullOrWhiteSpace(text))
                            {
                                task.Increment(1);
                                continue;
                            }

                            var relativePath = Path.GetRelativePath(basePath, filePath);
                            var extension = Path.GetExtension(filePath);
                            var language = AutoTagger.GetLanguage(extension);
                            var layer = AutoTagger.GetLayer(relativePath);

                            // Auto-detect tags from path, extension, and content
                            var autoTags = AutoTagger.GetAllTags(relativePath, extension, text);

                            // Add user-specified tags
                            if (tags != null)
                            {
                                foreach (var tag in tags)
                                    autoTags.Add(tag);
                            }

                            // Extract symbols from file
                            var fileSymbols = SymbolExtractor.ExtractDefinedSymbols(text, language);
                            var fileReferences = SymbolExtractor.ExtractReferences(text, language);

                            // Chunk the text with line tracking
                            var chunks = ChunkTextWithLines(text, chunkSize, chunkSize / 5);
                            var chunkIds = new List<string>();

                            foreach (var (chunkText, lineStart, lineEnd, index) in chunks)
                            {
                                var chunkId = $"{ComputeHash(filePath)}_{index}";
                                chunkIds.Add(chunkId);

                                // Extract symbols specific to this chunk
                                var chunkSymbols = SymbolExtractor.ExtractDefinedSymbols(chunkText, language);
                                var chunkReferences = SymbolExtractor.ExtractReferences(chunkText, language);

                                // Store embedding
                                await embeddingStore.StoreEmbedding(chunkId, chunkText);

                                // Store rich metadata
                                chunkMetadataStore[chunkId] = new ChunkMetadata
                                {
                                    Text = chunkText,
                                    File = relativePath,
                                    LineStart = lineStart,
                                    LineEnd = lineEnd,
                                    Tags = autoTags.ToList(),
                                    Language = language,
                                    Symbols = chunkSymbols.ToList(),
                                    References = chunkReferences.ToList(),
                                    Layer = layer,
                                    Extension = extension,
                                    IndexedAt = DateTime.UtcNow
                                };

                                // Update symbol graph
                                foreach (var symbol in chunkSymbols)
                                {
                                    symbolGraph.AddDefinition(symbol, relativePath, chunkId);
                                }
                                foreach (var reference in chunkReferences)
                                {
                                    foreach (var definedSymbol in chunkSymbols)
                                    {
                                        symbolGraph.AddReference(definedSymbol, reference, relativePath);
                                    }
                                }
                            }

                            // Update file index
                            storeConfig.FileIndex[relativePath] = new FileIndexEntry
                            {
                                RelativePath = relativePath,
                                Hash = ComputeFileHash(filePath),
                                LastModified = File.GetLastWriteTimeUtc(filePath),
                                ChunkCount = chunks.Count,
                                ChunkIds = chunkIds,
                                Tags = autoTags.ToList()
                            };

                            indexed++;
                        }
                        catch (Exception ex)
                        {
                            AnsiConsole.MarkupLine($"[red]Failed:[/] {Path.GetFileName(filePath)} - {ex.Message}");
                            failed++;
                        }

                        task.Increment(1);
                    }

                    storeConfig.FileCount = storeConfig.FileIndex.Count;
                    storeConfig.ChunkCount = storeConfig.FileIndex.Values.Sum(f => f.ChunkCount);
                    storeConfig.LastSync = DateTime.UtcNow;

                    // Save chunk metadata
                    SaveChunkMetadata(chunksFilePath, chunkMetadataStore);

                    // Save symbol graph
                    SaveSymbolGraph(symbolGraphPath, symbolGraph);

                    registry.Save();

                    AnsiConsole.MarkupLine($"\n[green]✓[/] Indexed {indexed} files ({storeConfig.ChunkCount} chunks)");
                    AnsiConsole.MarkupLine($"[dim]Symbol graph: {symbolGraph.Symbols.Count} symbols tracked[/]");
                    if (failed > 0)
                    {
                        AnsiConsole.MarkupLine($"[yellow]![/] {failed} files failed");
                    }
                });

        }, pathArg, storeOption, tagsOption, chunkSizeOption);

        return cmd;
    }

    #endregion

    #region Query Command

    private static Command CreateQueryCommand()
    {
        var queryArg = new Argument<string>("query", "Question to ask");
        var storeOption = new Option<string>("--store", "Store name") { IsRequired = true };
        var topKOption = new Option<int>("--top-k", () => 5, "Number of results to retrieve");
        var rawOption = new Option<bool>("--raw", "Return raw chunks without LLM generation");
        var tagsOption = new Option<string[]?>("--tags", "Filter by tags (include only chunks with these tags)");
        var excludeTagsOption = new Option<string[]?>("--exclude-tags", "Exclude chunks with these tags");
        var layerOption = new Option<string?>("--layer", "Filter by architectural layer (api, domain, infrastructure, presentation, test)");
        var multiHopOption = new Option<bool>("--multi-hop", "Enable multi-hop retrieval (follow symbol references)");
        var maxHopsOption = new Option<int>("--max-hops", () => 2, "Maximum hops for multi-hop retrieval");

        var cmd = new Command("query", "Query the document store with tag filtering and multi-hop retrieval")
        {
            queryArg,
            storeOption,
            topKOption,
            rawOption,
            tagsOption,
            excludeTagsOption,
            layerOption,
            multiHopOption,
            maxHopsOption
        };

        cmd.SetHandler(async (context) =>
        {
            var query = context.ParseResult.GetValueForArgument(queryArg);
            var storeName = context.ParseResult.GetValueForOption(storeOption)!;
            var topK = context.ParseResult.GetValueForOption(topKOption);
            var raw = context.ParseResult.GetValueForOption(rawOption);
            var tags = context.ParseResult.GetValueForOption(tagsOption);
            var excludeTags = context.ParseResult.GetValueForOption(excludeTagsOption);
            var layer = context.ParseResult.GetValueForOption(layerOption);
            var multiHop = context.ParseResult.GetValueForOption(multiHopOption);
            var maxHops = context.ParseResult.GetValueForOption(maxHopsOption);

            var registry = StoreRegistry.Load();
            var storeConfig = registry.Stores.GetValueOrDefault(storeName);

            if (storeConfig == null)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Store '{storeName}' not found.");
                return;
            }

            // Get client
            var client = HazinaConfig.GetLLMClient();
            var vectorStorePath = Path.Combine(storeConfig.Path, "vectors", "embeddings.json");
            var embeddingStore = EmbeddingStoreFactory.CreateFromSpec(vectorStorePath, client);

            // Load chunk metadata
            var chunksFilePath = Path.Combine(storeConfig.Path, "documents", "chunks.json");
            var chunkMetadata = LoadChunkMetadata(chunksFilePath);

            // Load symbol graph for multi-hop
            var symbolGraphPath = Path.Combine(storeConfig.Path, "documents", "symbol-graph.json");
            var symbolGraph = LoadSymbolGraph(symbolGraphPath);

            await AnsiConsole.Status()
                .StartAsync("Searching...", async ctx =>
                {
                    // Build set of allowed chunk IDs based on tag filters
                    HashSet<string>? allowedChunkIds = null;
                    if (tags != null || excludeTags != null || layer != null)
                    {
                        allowedChunkIds = new HashSet<string>();
                        var tagSet = tags != null ? new HashSet<string>(tags, StringComparer.OrdinalIgnoreCase) : null;
                        var excludeTagSet = excludeTags != null ? new HashSet<string>(excludeTags, StringComparer.OrdinalIgnoreCase) : null;

                        foreach (var (chunkId, metadata) in chunkMetadata)
                        {
                            // Check layer filter
                            if (layer != null && !metadata.Layer.Equals(layer, StringComparison.OrdinalIgnoreCase))
                                continue;

                            // Check tag inclusion
                            if (tagSet != null && !metadata.Tags.Any(t => tagSet.Contains(t)))
                                continue;

                            // Check tag exclusion
                            if (excludeTagSet != null && metadata.Tags.Any(t => excludeTagSet.Contains(t)))
                                continue;

                            allowedChunkIds.Add(chunkId);
                        }

                        if (allowedChunkIds.Count == 0)
                        {
                            AnsiConsole.MarkupLine("[yellow]No chunks match the specified filters.[/]");
                            return;
                        }

                        AnsiConsole.MarkupLine($"[dim]Searching {allowedChunkIds.Count} chunks (filtered from {chunkMetadata.Count})[/]");
                    }

                    // Get query embedding
                    var queryEmbedding = await client.GenerateEmbedding(query);

                    // Search for similar chunks (with filtering)
                    var allEmbeddings = embeddingStore.Embeddings;
                    var results = new List<(string key, double score)>();

                    foreach (var emb in allEmbeddings)
                    {
                        // Apply filter if specified
                        if (allowedChunkIds != null && !allowedChunkIds.Contains(emb.Key))
                            continue;

                        var similarity = queryEmbedding.CosineSimilarity(emb.Data);
                        results.Add((emb.Key, similarity));
                    }

                    var topResults = results.OrderByDescending(r => r.score).Take(topK).ToList();

                    if (topResults.Count == 0)
                    {
                        AnsiConsole.MarkupLine("[yellow]No results found.[/]");
                        return;
                    }

                    // Collect chunk metadata for results
                    var retrievedChunks = new List<(string chunkId, ChunkMetadata metadata, double score)>();
                    foreach (var (key, score) in topResults)
                    {
                        if (chunkMetadata.TryGetValue(key, out var metadata))
                        {
                            retrievedChunks.Add((key, metadata, score));
                        }
                    }

                    // Multi-hop retrieval
                    if (multiHop && retrievedChunks.Count > 0)
                    {
                        ctx.Status("Performing multi-hop retrieval...");
                        var additionalChunks = await PerformMultiHopRetrieval(
                            retrievedChunks,
                            symbolGraph,
                            chunkMetadata,
                            maxHops,
                            topK
                        );

                        // Merge additional chunks (avoid duplicates)
                        var existingIds = new HashSet<string>(retrievedChunks.Select(c => c.chunkId));
                        foreach (var chunk in additionalChunks)
                        {
                            if (!existingIds.Contains(chunk.chunkId))
                            {
                                retrievedChunks.Add(chunk);
                                existingIds.Add(chunk.chunkId);
                            }
                        }

                        AnsiConsole.MarkupLine($"[dim]Multi-hop: expanded to {retrievedChunks.Count} chunks[/]");
                    }

                    if (raw)
                    {
                        AnsiConsole.MarkupLine($"\n[bold]Top {retrievedChunks.Count} results:[/]\n");
                        foreach (var (chunkId, metadata, score) in retrievedChunks)
                        {
                            AnsiConsole.MarkupLine($"[dim]Score: {score:P0} | File: {metadata.File}:{metadata.LineStart}-{metadata.LineEnd}[/]");
                            AnsiConsole.MarkupLine($"[dim]Tags: {string.Join(", ", metadata.Tags.Take(5))} | Symbols: {string.Join(", ", metadata.Symbols.Take(3))}[/]");
                            AnsiConsole.WriteLine(metadata.Text);
                            AnsiConsole.WriteLine();
                        }
                        return;
                    }

                    // Generate answer with LLM
                    ctx.Status("Generating answer...");

                    // Build rich context with file info
                    var contextParts = retrievedChunks.Select(c =>
                        $"[File: {c.metadata.File}:{c.metadata.LineStart}-{c.metadata.LineEnd}]\n{c.metadata.Text}");
                    var ragContext = string.Join("\n\n---\n\n", contextParts);

                    var messages = new List<HazinaChatMessage>
                    {
                        new() { Role = HazinaMessageRole.System, Text = "Answer the question based on the provided context. Be concise and cite sources with file paths and line numbers when relevant." },
                        new() { Role = HazinaMessageRole.User, Text = $"Context:\n{ragContext}\n\nQuestion: {query}" }
                    };

                    var response = await client.GetResponse(messages, HazinaChatResponseFormat.Text, null, null, CancellationToken.None);

                    AnsiConsole.MarkupLine($"\n[bold]Answer:[/]\n");
                    AnsiConsole.WriteLine(response.Result);

                    // Show source summary
                    var sourceFiles = retrievedChunks
                        .Select(c => c.metadata.File)
                        .Distinct()
                        .Take(5);
                    AnsiConsole.MarkupLine($"\n[dim]Sources ({retrievedChunks.Count} chunks from {sourceFiles.Count()} files):[/]");
                    foreach (var file in sourceFiles)
                    {
                        AnsiConsole.MarkupLine($"[dim]  - {file}[/]");
                    }
                });
        });

        return cmd;
    }

    private static Task<List<(string chunkId, ChunkMetadata metadata, double score)>> PerformMultiHopRetrieval(
        List<(string chunkId, ChunkMetadata metadata, double score)> initialChunks,
        SymbolGraph symbolGraph,
        Dictionary<string, ChunkMetadata> chunkMetadata,
        int maxHops,
        int maxAdditional)
    {
        var additionalChunks = new List<(string chunkId, ChunkMetadata metadata, double score)>();
        var visitedSymbols = new HashSet<string>();
        var visitedChunks = new HashSet<string>(initialChunks.Select(c => c.chunkId));

        // Collect symbols from initial chunks
        var symbolsToFollow = new Queue<(string symbol, int hop)>();
        foreach (var (_, metadata, _) in initialChunks)
        {
            foreach (var symbol in metadata.Symbols)
            {
                if (!visitedSymbols.Contains(symbol))
                {
                    symbolsToFollow.Enqueue((symbol, 0));
                    visitedSymbols.Add(symbol);
                }
            }
            foreach (var reference in metadata.References)
            {
                if (!visitedSymbols.Contains(reference))
                {
                    symbolsToFollow.Enqueue((reference, 0));
                    visitedSymbols.Add(reference);
                }
            }
        }

        // Follow symbol references
        while (symbolsToFollow.Count > 0 && additionalChunks.Count < maxAdditional)
        {
            var (symbol, hop) = symbolsToFollow.Dequeue();

            if (hop >= maxHops)
                continue;

            // Get chunks where this symbol is defined
            var definingChunks = symbolGraph.GetChunksForSymbol(symbol);
            foreach (var chunkId in definingChunks)
            {
                if (visitedChunks.Contains(chunkId))
                    continue;

                if (chunkMetadata.TryGetValue(chunkId, out var metadata))
                {
                    // Score based on hop distance (closer = higher score)
                    var hopScore = 0.8 - (hop * 0.2);
                    additionalChunks.Add((chunkId, metadata, hopScore));
                    visitedChunks.Add(chunkId);

                    // Queue related symbols for next hop
                    if (hop + 1 < maxHops)
                    {
                        foreach (var relatedSymbol in metadata.References)
                        {
                            if (!visitedSymbols.Contains(relatedSymbol))
                            {
                                symbolsToFollow.Enqueue((relatedSymbol, hop + 1));
                                visitedSymbols.Add(relatedSymbol);
                            }
                        }
                    }
                }
            }

            // Also check callers/callees
            var callers = symbolGraph.GetCallers(symbol);
            var callees = symbolGraph.GetCallees(symbol);

            foreach (var related in callers.Concat(callees).Take(3))
            {
                var relatedChunks = symbolGraph.GetChunksForSymbol(related);
                foreach (var chunkId in relatedChunks.Take(2))
                {
                    if (visitedChunks.Contains(chunkId))
                        continue;

                    if (chunkMetadata.TryGetValue(chunkId, out var metadata))
                    {
                        var hopScore = 0.7 - (hop * 0.2);
                        additionalChunks.Add((chunkId, metadata, hopScore));
                        visitedChunks.Add(chunkId);
                    }
                }
            }
        }

        return Task.FromResult(additionalChunks.Take(maxAdditional).ToList());
    }

    #endregion

    #region Sync Command

    private static Command CreateSyncCommand()
    {
        var storeOption = new Option<string>("--store", "Store name") { IsRequired = true };
        var dryRunOption = new Option<bool>("--dry-run", "Preview changes without applying");

        var cmd = new Command("sync", "Sync store with file system changes")
        {
            storeOption,
            dryRunOption
        };

        cmd.SetHandler((string storeName, bool dryRun) =>
        {
            var registry = StoreRegistry.Load();
            var storeConfig = registry.Stores.GetValueOrDefault(storeName);

            if (storeConfig == null)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Store '{storeName}' not found.");
                return;
            }

            var basePath = Environment.CurrentDirectory;
            var modified = new List<string>();
            var deleted = new List<string>();

            foreach (var (relativePath, entry) in storeConfig.FileIndex)
            {
                var fullPath = Path.Combine(basePath, relativePath);

                if (!File.Exists(fullPath))
                {
                    deleted.Add(relativePath);
                }
                else
                {
                    var currentHash = ComputeFileHash(fullPath);
                    if (currentHash != entry.Hash)
                    {
                        modified.Add(relativePath);
                    }
                }
            }

            if (modified.Count == 0 && deleted.Count == 0)
            {
                AnsiConsole.MarkupLine("[green]✓[/] Store is up to date");
                return;
            }

            var table = new Table();
            table.AddColumn("Action");
            table.AddColumn("File");

            foreach (var file in modified)
                table.AddRow("[yellow]~[/]", file);
            foreach (var file in deleted)
                table.AddRow("[red]-[/]", file);

            AnsiConsole.Write(table);
            AnsiConsole.MarkupLine($"\nSummary: [yellow]~{modified.Count}[/] modified, [red]-{deleted.Count}[/] deleted");

            if (dryRun)
            {
                AnsiConsole.MarkupLine("\n[dim](dry run - no changes applied)[/]");
            }

        }, storeOption, dryRunOption);

        return cmd;
    }

    #endregion

    #region Status & List Commands

    private static Command CreateStatusCommand()
    {
        var storeOption = new Option<string>("--store", "Store name") { IsRequired = true };

        var cmd = new Command("status", "Show store status")
        {
            storeOption
        };

        cmd.SetHandler((string storeName) =>
        {
            var registry = StoreRegistry.Load();
            var storeConfig = registry.Stores.GetValueOrDefault(storeName);

            if (storeConfig == null)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Store '{storeName}' not found.");
                return;
            }

            AnsiConsole.MarkupLine($"[bold]Store:[/] {storeName}");
            AnsiConsole.MarkupLine($"[bold]Path:[/] {storeConfig.Path}");
            AnsiConsole.MarkupLine($"[bold]Embedding model:[/] {storeConfig.EmbeddingModel}");
            AnsiConsole.MarkupLine($"[bold]Files indexed:[/] {storeConfig.FileCount}");
            AnsiConsole.MarkupLine($"[bold]Total chunks:[/] {storeConfig.ChunkCount}");
            AnsiConsole.MarkupLine($"[bold]Created:[/] {storeConfig.Created:yyyy-MM-dd HH:mm}");
            AnsiConsole.MarkupLine($"[bold]Last sync:[/] {(storeConfig.LastSync == default ? "Never" : storeConfig.LastSync.ToString("yyyy-MM-dd HH:mm"))}");

        }, storeOption);

        return cmd;
    }

    private static Command CreateListStoresCommand()
    {
        var cmd = new Command("list", "List all configured stores");

        cmd.SetHandler(() =>
        {
            var registry = StoreRegistry.Load();

            if (registry.Stores.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No stores configured.[/]");
                return;
            }

            var table = new Table();
            table.AddColumn("Store");
            table.AddColumn("Files");
            table.AddColumn("Chunks");
            table.AddColumn("Last Sync");

            foreach (var (name, config) in registry.Stores.OrderBy(s => s.Key))
            {
                table.AddRow(
                    name,
                    config.FileCount.ToString(),
                    config.ChunkCount.ToString(),
                    config.LastSync == default ? "Never" : config.LastSync.ToString("MM-dd HH:mm")
                );
            }

            AnsiConsole.Write(table);
        });

        return cmd;
    }

    #endregion

    #region Phase 3: Specialized Query Commands

    private static Command CreateFindSymbolCommand()
    {
        var symbolArg = new Argument<string>("symbol", "Symbol name to find (class, method, interface)");
        var storeOption = new Option<string>("--store", "Store name") { IsRequired = true };

        var cmd = new Command("find-symbol", "Find where a symbol is defined")
        {
            symbolArg,
            storeOption
        };

        cmd.SetHandler((string symbol, string storeName) =>
        {
            var registry = StoreRegistry.Load();
            var storeConfig = registry.Stores.GetValueOrDefault(storeName);

            if (storeConfig == null)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Store '{storeName}' not found.");
                return;
            }

            var symbolGraphPath = Path.Combine(storeConfig.Path, "documents", "symbol-graph.json");
            var symbolGraph = LoadSymbolGraph(symbolGraphPath);

            var chunksFilePath = Path.Combine(storeConfig.Path, "documents", "chunks.json");
            var chunkMetadata = LoadChunkMetadata(chunksFilePath);

            if (!symbolGraph.Symbols.TryGetValue(symbol, out var symbolInfo))
            {
                AnsiConsole.MarkupLine($"[yellow]Symbol '{symbol}' not found in graph.[/]");
                AnsiConsole.MarkupLine("[dim]Hint: Try a partial match:[/]");

                // Show partial matches
                var partialMatches = symbolGraph.Symbols.Keys
                    .Where(s => s.Contains(symbol, StringComparison.OrdinalIgnoreCase))
                    .Take(10)
                    .ToList();

                if (partialMatches.Any())
                {
                    foreach (var match in partialMatches)
                        AnsiConsole.MarkupLine($"  - {match}");
                }
                return;
            }

            AnsiConsole.MarkupLine($"\n[bold]Symbol: {symbol}[/]\n");

            // Show where it's defined
            AnsiConsole.MarkupLine($"[bold]Defined in:[/]");
            foreach (var file in symbolInfo.DefinedIn)
            {
                AnsiConsole.MarkupLine($"  - {file}");
            }

            // Show chunks
            AnsiConsole.MarkupLine($"\n[bold]Chunks ({symbolInfo.ChunkIds.Count}):[/]");
            foreach (var chunkId in symbolInfo.ChunkIds.Take(5))
            {
                if (chunkMetadata.TryGetValue(chunkId, out var metadata))
                {
                    AnsiConsole.MarkupLine($"  [dim]{metadata.File}:{metadata.LineStart}-{metadata.LineEnd}[/]");
                    var preview = metadata.Text.Length > 100 ? metadata.Text.Substring(0, 100) + "..." : metadata.Text;
                    AnsiConsole.MarkupLine($"    {preview.Replace("\n", " ").Replace("\r", "")}");
                }
            }

        }, symbolArg, storeOption);

        return cmd;
    }

    private static Command CreateFindCallersCommand()
    {
        var symbolArg = new Argument<string>("symbol", "Symbol to find callers of");
        var storeOption = new Option<string>("--store", "Store name") { IsRequired = true };
        var depthOption = new Option<int>("--depth", () => 1, "How many levels of callers to show");

        var cmd = new Command("find-callers", "Find all callers of a symbol")
        {
            symbolArg,
            storeOption,
            depthOption
        };

        cmd.SetHandler((string symbol, string storeName, int depth) =>
        {
            var registry = StoreRegistry.Load();
            var storeConfig = registry.Stores.GetValueOrDefault(storeName);

            if (storeConfig == null)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Store '{storeName}' not found.");
                return;
            }

            var symbolGraphPath = Path.Combine(storeConfig.Path, "documents", "symbol-graph.json");
            var symbolGraph = LoadSymbolGraph(symbolGraphPath);

            var callers = symbolGraph.GetCallers(symbol);

            if (callers.Count == 0)
            {
                AnsiConsole.MarkupLine($"[yellow]No callers found for '{symbol}'.[/]");
                return;
            }

            AnsiConsole.MarkupLine($"\n[bold]Callers of {symbol} ({callers.Count}):[/]\n");

            var tree = new Tree($"[bold]{symbol}[/]");
            AddCallersToTree(tree, symbol, symbolGraph, depth, new HashSet<string>());
            AnsiConsole.Write(tree);

        }, symbolArg, storeOption, depthOption);

        return cmd;
    }

    private static void AddCallersToTree(IHasTreeNodes parent, string symbol, SymbolGraph graph, int depth, HashSet<string> visited)
    {
        if (depth <= 0 || visited.Contains(symbol))
            return;

        visited.Add(symbol);
        var callers = graph.GetCallers(symbol);

        foreach (var caller in callers.Take(10))
        {
            var node = parent.AddNode($"[blue]{caller}[/]");
            if (depth > 1)
            {
                AddCallersToTree(node, caller, graph, depth - 1, visited);
            }
        }
    }

    private static Command CreateFindReferencesCommand()
    {
        var symbolArg = new Argument<string>("symbol", "Symbol to find references to");
        var storeOption = new Option<string>("--store", "Store name") { IsRequired = true };

        var cmd = new Command("find-references", "Find all references to a symbol")
        {
            symbolArg,
            storeOption
        };

        cmd.SetHandler((string symbol, string storeName) =>
        {
            var registry = StoreRegistry.Load();
            var storeConfig = registry.Stores.GetValueOrDefault(storeName);

            if (storeConfig == null)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Store '{storeName}' not found.");
                return;
            }

            var chunksFilePath = Path.Combine(storeConfig.Path, "documents", "chunks.json");
            var chunkMetadata = LoadChunkMetadata(chunksFilePath);

            // Find all chunks that reference this symbol
            var referencingChunks = chunkMetadata
                .Where(kv => kv.Value.References.Contains(symbol, StringComparer.OrdinalIgnoreCase))
                .ToList();

            if (referencingChunks.Count == 0)
            {
                AnsiConsole.MarkupLine($"[yellow]No references found to '{symbol}'.[/]");
                return;
            }

            AnsiConsole.MarkupLine($"\n[bold]References to {symbol} ({referencingChunks.Count}):[/]\n");

            var table = new Table();
            table.AddColumn("File");
            table.AddColumn("Lines");
            table.AddColumn("Defined Symbols");

            foreach (var (_, metadata) in referencingChunks.Take(20))
            {
                table.AddRow(
                    metadata.File,
                    $"{metadata.LineStart}-{metadata.LineEnd}",
                    string.Join(", ", metadata.Symbols.Take(3))
                );
            }

            AnsiConsole.Write(table);

        }, symbolArg, storeOption);

        return cmd;
    }

    private static Command CreateExpandContextCommand()
    {
        var fileArg = new Argument<string>("file", "File path to expand context around");
        var lineOption = new Option<int>("--line", "Line number to center on") { IsRequired = true };
        var storeOption = new Option<string>("--store", "Store name") { IsRequired = true };
        var radiusOption = new Option<int>("--radius", () => 2, "Number of chunks to include before/after");

        var cmd = new Command("expand-context", "Get expanded context around a specific location")
        {
            fileArg,
            storeOption,
            lineOption,
            radiusOption
        };

        cmd.SetHandler((string file, string storeName, int line, int radius) =>
        {
            var registry = StoreRegistry.Load();
            var storeConfig = registry.Stores.GetValueOrDefault(storeName);

            if (storeConfig == null)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Store '{storeName}' not found.");
                return;
            }

            var chunksFilePath = Path.Combine(storeConfig.Path, "documents", "chunks.json");
            var chunkMetadata = LoadChunkMetadata(chunksFilePath);

            // Find chunks in this file
            var fileChunks = chunkMetadata
                .Where(kv => kv.Value.File.EndsWith(file, StringComparison.OrdinalIgnoreCase) ||
                             kv.Value.File.Equals(file, StringComparison.OrdinalIgnoreCase))
                .OrderBy(kv => kv.Value.LineStart)
                .ToList();

            if (fileChunks.Count == 0)
            {
                AnsiConsole.MarkupLine($"[yellow]No chunks found for file '{file}'.[/]");
                return;
            }

            // Find the chunk containing the target line
            var centerIndex = fileChunks.FindIndex(kv =>
                kv.Value.LineStart <= line && kv.Value.LineEnd >= line);

            if (centerIndex == -1)
            {
                // Find nearest chunk
                centerIndex = fileChunks.FindIndex(kv => kv.Value.LineStart > line);
                if (centerIndex == -1) centerIndex = fileChunks.Count - 1;
                else if (centerIndex > 0) centerIndex--;
            }

            // Get chunks in radius
            var startIndex = Math.Max(0, centerIndex - radius);
            var endIndex = Math.Min(fileChunks.Count - 1, centerIndex + radius);

            AnsiConsole.MarkupLine($"\n[bold]Context around {file}:{line}[/]\n");
            AnsiConsole.MarkupLine($"[dim]Showing chunks {startIndex + 1} to {endIndex + 1} of {fileChunks.Count}[/]\n");

            for (int i = startIndex; i <= endIndex; i++)
            {
                var (chunkId, metadata) = fileChunks[i];
                var isCurrent = i == centerIndex;

                if (isCurrent)
                    AnsiConsole.MarkupLine($"[bold green]>>> {metadata.File}:{metadata.LineStart}-{metadata.LineEnd} <<<[/]");
                else
                    AnsiConsole.MarkupLine($"[dim]{metadata.File}:{metadata.LineStart}-{metadata.LineEnd}[/]");

                AnsiConsole.WriteLine(metadata.Text);
                AnsiConsole.WriteLine();
            }

        }, fileArg, storeOption, lineOption, radiusOption);

        return cmd;
    }

    #endregion

    #region Helper Methods

    private static List<string> ChunkText(string text, int chunkSize, int overlap)
    {
        var chunks = new List<string>();
        var lines = text.Split('\n');
        var currentChunk = new StringBuilder();
        var currentSize = 0;

        foreach (var line in lines)
        {
            if (currentSize + line.Length > chunkSize && currentSize > 0)
            {
                chunks.Add(currentChunk.ToString().Trim());
                var overlapText = currentChunk.ToString();
                currentChunk.Clear();
                if (overlapText.Length > overlap)
                {
                    currentChunk.Append(overlapText.Substring(overlapText.Length - overlap));
                }
                currentSize = currentChunk.Length;
            }

            currentChunk.AppendLine(line);
            currentSize += line.Length + 1;
        }

        if (currentChunk.Length > 0)
        {
            chunks.Add(currentChunk.ToString().Trim());
        }

        return chunks;
    }

    private static List<(string text, int lineStart, int lineEnd, int index)> ChunkTextWithLines(string text, int chunkSize, int overlap)
    {
        var chunks = new List<(string text, int lineStart, int lineEnd, int index)>();
        var lines = text.Split('\n');
        var currentChunk = new StringBuilder();
        var currentSize = 0;
        var chunkStartLine = 1;
        var currentLine = 1;
        var chunkIndex = 0;

        foreach (var line in lines)
        {
            if (currentSize + line.Length > chunkSize && currentSize > 0)
            {
                chunks.Add((currentChunk.ToString().Trim(), chunkStartLine, currentLine - 1, chunkIndex));
                chunkIndex++;

                // Calculate overlap in lines
                var overlapText = currentChunk.ToString();
                currentChunk.Clear();
                if (overlapText.Length > overlap)
                {
                    currentChunk.Append(overlapText.Substring(overlapText.Length - overlap));
                    // Estimate lines in overlap
                    var overlapLines = overlapText.Substring(overlapText.Length - overlap).Count(c => c == '\n');
                    chunkStartLine = currentLine - overlapLines;
                }
                else
                {
                    chunkStartLine = currentLine;
                }
                currentSize = currentChunk.Length;
            }

            currentChunk.AppendLine(line);
            currentSize += line.Length + 1;
            currentLine++;
        }

        if (currentChunk.Length > 0)
        {
            chunks.Add((currentChunk.ToString().Trim(), chunkStartLine, currentLine - 1, chunkIndex));
        }

        return chunks;
    }

    private static string ComputeHash(string input)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).Substring(0, 16).ToLowerInvariant();
    }

    private static string ComputeFileHash(string filePath)
    {
        using var sha = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var bytes = sha.ComputeHash(stream);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static async Task<string> ExtractTextSimple(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        var textExtensions = new HashSet<string>
        {
            ".txt", ".md", ".cs", ".js", ".ts", ".tsx", ".jsx", ".py", ".rb", ".go",
            ".java", ".c", ".cpp", ".h", ".hpp", ".rs", ".swift", ".kt", ".scala",
            ".html", ".htm", ".css", ".scss", ".json", ".xml", ".yaml", ".yml",
            ".toml", ".ini", ".sh", ".ps1", ".bat", ".sql", ".vue", ".svelte"
        };

        if (textExtensions.Contains(ext))
        {
            return await File.ReadAllTextAsync(filePath);
        }

        return "";
    }

    #region Chunk Metadata Storage

    private static Dictionary<string, ChunkMetadata> LoadChunkMetadata(string chunksFilePath)
    {
        if (!File.Exists(chunksFilePath))
            return new Dictionary<string, ChunkMetadata>();

        try
        {
            var json = File.ReadAllText(chunksFilePath);

            // Try to load as new format (ChunkMetadata)
            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, ChunkMetadata>>(json)
                    ?? new Dictionary<string, ChunkMetadata>();
            }
            catch
            {
                // Fallback: try to load as old format (simple string) and convert
                var oldFormat = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (oldFormat != null)
                {
                    var converted = new Dictionary<string, ChunkMetadata>();
                    foreach (var (key, text) in oldFormat)
                    {
                        converted[key] = new ChunkMetadata
                        {
                            Text = text,
                            IndexedAt = DateTime.UtcNow
                        };
                    }
                    return converted;
                }
                return new Dictionary<string, ChunkMetadata>();
            }
        }
        catch
        {
            return new Dictionary<string, ChunkMetadata>();
        }
    }

    private static void SaveChunkMetadata(string chunksFilePath, Dictionary<string, ChunkMetadata> chunkMetadata)
    {
        var dir = Path.GetDirectoryName(chunksFilePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var json = JsonSerializer.Serialize(chunkMetadata, new JsonSerializerOptions
        {
            WriteIndented = false, // Save space - can be large
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });
        File.WriteAllText(chunksFilePath, json);
    }

    #endregion

    #region Symbol Graph Storage

    private static SymbolGraph LoadSymbolGraph(string symbolGraphPath)
    {
        if (!File.Exists(symbolGraphPath))
            return new SymbolGraph();

        try
        {
            var json = File.ReadAllText(symbolGraphPath);
            return JsonSerializer.Deserialize<SymbolGraph>(json) ?? new SymbolGraph();
        }
        catch
        {
            return new SymbolGraph();
        }
    }

    private static void SaveSymbolGraph(string symbolGraphPath, SymbolGraph symbolGraph)
    {
        var dir = Path.GetDirectoryName(symbolGraphPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var json = JsonSerializer.Serialize(symbolGraph, new JsonSerializerOptions
        {
            WriteIndented = false
        });
        File.WriteAllText(symbolGraphPath, json);
    }

    #endregion

    #region Legacy Support

    private static Dictionary<string, string> LoadChunkTexts(string chunksFilePath)
    {
        // Load metadata and extract just the text (for backward compatibility)
        var metadata = LoadChunkMetadata(chunksFilePath);
        return metadata.ToDictionary(kv => kv.Key, kv => kv.Value.Text);
    }

    private static void SaveChunkTexts(string chunksFilePath, Dictionary<string, string> chunkTexts)
    {
        // Convert to metadata format
        var metadata = chunkTexts.ToDictionary(
            kv => kv.Key,
            kv => new ChunkMetadata { Text = kv.Value, IndexedAt = DateTime.UtcNow }
        );
        SaveChunkMetadata(chunksFilePath, metadata);
    }

    #endregion

    #endregion
}
