using System.CommandLine;
using System.Security.Cryptography;
using System.Text;
using Hazina.CLI.Infrastructure;
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
            var vectorStorePath = Path.Combine(storeConfig.Path, "vectors");
            var embeddingStore = EmbeddingStoreFactory.CreateFromSpec(vectorStorePath, client);

            // Load existing chunk texts
            var chunksFilePath = Path.Combine(storeConfig.Path, "documents", "chunks.json");
            var chunkTexts = LoadChunkTexts(chunksFilePath);

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

                            // Chunk the text
                            var chunks = ChunkText(text, chunkSize, chunkSize / 5);
                            var chunkIds = new List<string>();
                            var relativePath = Path.GetRelativePath(basePath, filePath);

                            foreach (var (chunk, index) in chunks.Select((c, i) => (c, i)))
                            {
                                var chunkId = $"{ComputeHash(filePath)}_{index}";
                                chunkIds.Add(chunkId);

                                // Store embedding
                                await embeddingStore.StoreEmbedding(chunkId, chunk);

                                // Store chunk text for retrieval
                                chunkTexts[chunkId] = chunk;
                            }

                            // Update file index
                            storeConfig.FileIndex[relativePath] = new FileIndexEntry
                            {
                                RelativePath = relativePath,
                                Hash = ComputeFileHash(filePath),
                                LastModified = File.GetLastWriteTimeUtc(filePath),
                                ChunkCount = chunks.Count,
                                ChunkIds = chunkIds,
                                Tags = tags?.ToList() ?? new List<string>()
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

                    // Save chunk texts
                    SaveChunkTexts(chunksFilePath, chunkTexts);

                    registry.Save();

                    AnsiConsole.MarkupLine($"\n[green]✓[/] Indexed {indexed} files ({storeConfig.ChunkCount} chunks)");
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

        var cmd = new Command("query", "Query the document store")
        {
            queryArg,
            storeOption,
            topKOption,
            rawOption
        };

        cmd.SetHandler(async (string query, string storeName, int topK, bool raw) =>
        {
            var registry = StoreRegistry.Load();
            var storeConfig = registry.Stores.GetValueOrDefault(storeName);

            if (storeConfig == null)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Store '{storeName}' not found.");
                return;
            }

            // Get client
            var client = HazinaConfig.GetLLMClient();
            var vectorStorePath = Path.Combine(storeConfig.Path, "vectors");
            var embeddingStore = EmbeddingStoreFactory.CreateFromSpec(vectorStorePath, client);

            await AnsiConsole.Status()
                .StartAsync("Searching...", async ctx =>
                {
                    // Get query embedding
                    var queryEmbedding = await client.GenerateEmbedding(query);

                    // Search for similar chunks
                    var allEmbeddings = embeddingStore.Embeddings;
                    var results = new List<(string key, double score)>();

                    foreach (var emb in allEmbeddings)
                    {
                        var similarity = queryEmbedding.CosineSimilarity(emb.Data);
                        results.Add((emb.Key, similarity));
                    }

                    var topResults = results.OrderByDescending(r => r.score).Take(topK).ToList();

                    if (topResults.Count == 0)
                    {
                        AnsiConsole.MarkupLine("[yellow]No results found.[/]");
                        return;
                    }

                    // Load chunk texts from documents store
                    var chunksFilePath = Path.Combine(storeConfig.Path, "documents", "chunks.json");
                    var chunkTexts = LoadChunkTexts(chunksFilePath);

                    // Get texts for top results
                    var chunks = new List<string>();
                    foreach (var (key, score) in topResults)
                    {
                        if (chunkTexts.TryGetValue(key, out var text))
                        {
                            chunks.Add(text);
                        }
                    }

                    if (raw)
                    {
                        AnsiConsole.MarkupLine($"\n[bold]Top {chunks.Count} results:[/]\n");
                        for (int i = 0; i < chunks.Count; i++)
                        {
                            AnsiConsole.MarkupLine($"[dim]Score: {topResults[i].score:P0}[/]");
                            AnsiConsole.WriteLine(chunks[i]);
                            AnsiConsole.WriteLine();
                        }
                        return;
                    }

                    // Generate answer with LLM
                    ctx.Status("Generating answer...");

                    var context = string.Join("\n\n---\n\n", chunks);
                    var messages = new List<HazinaChatMessage>
                    {
                        new() { Role = HazinaMessageRole.System, Text = "Answer the question based on the provided context. Be concise and cite sources when relevant." },
                        new() { Role = HazinaMessageRole.User, Text = $"Context:\n{context}\n\nQuestion: {query}" }
                    };

                    var response = await client.GetResponse(messages, HazinaChatResponseFormat.Text, null, null, CancellationToken.None);

                    AnsiConsole.MarkupLine($"\n[bold]Answer:[/]\n");
                    AnsiConsole.WriteLine(response.Result);
                    AnsiConsole.MarkupLine($"\n[dim]Sources: {chunks.Count} chunks retrieved[/]");
                });

        }, queryArg, storeOption, topKOption, rawOption);

        return cmd;
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

    private static Dictionary<string, string> LoadChunkTexts(string chunksFilePath)
    {
        if (!File.Exists(chunksFilePath))
            return new Dictionary<string, string>();

        try
        {
            var json = File.ReadAllText(chunksFilePath);
            return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                ?? new Dictionary<string, string>();
        }
        catch
        {
            return new Dictionary<string, string>();
        }
    }

    private static void SaveChunkTexts(string chunksFilePath, Dictionary<string, string> chunkTexts)
    {
        var dir = Path.GetDirectoryName(chunksFilePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var json = System.Text.Json.JsonSerializer.Serialize(chunkTexts, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(chunksFilePath, json);
    }

    #endregion
}
