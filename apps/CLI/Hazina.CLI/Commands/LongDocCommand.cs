using System.CommandLine;
using Hazina.CLI.Infrastructure;
using Hazina.LLMs;
using Spectre.Console;

namespace Hazina.CLI.Commands;

/// <summary>
/// Long document processing command for massive files
/// </summary>
public static class LongDocCommand
{
    public static Command Create()
    {
        var documentArg = new Argument<string>("document", "Path to document or directory");
        var queryArg = new Argument<string>("query", "Question to answer about the document");
        var strategyOption = new Option<string>("--strategy", () => "recursive", "Processing strategy: single, recursive");
        var chunkSizeOption = new Option<int>("--chunk-size", () => 50000, "Chunk size in characters");

        var command = new Command("longdoc", "Process large documents")
        {
            documentArg,
            queryArg,
            strategyOption,
            chunkSizeOption
        };

        command.SetHandler(async (string documentPath, string query, string strategy, int chunkSize) =>
        {
            try
            {
                if (!File.Exists(documentPath) && !Directory.Exists(documentPath))
                {
                    AnsiConsole.MarkupLine($"[red]Error:[/] Path not found: {documentPath}");
                    return;
                }

                var client = HazinaConfig.GetLLMClient();

                // Collect all text
                var allText = new List<(string source, string text)>();

                await AnsiConsole.Status()
                    .StartAsync("Extracting text...", async ctx =>
                    {
                        if (File.Exists(documentPath))
                        {
                            var text = await ExtractTextSimple(documentPath);
                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                allText.Add((documentPath, text));
                            }
                        }
                        else
                        {
                            var files = Directory.GetFiles(documentPath, "*", SearchOption.AllDirectories);
                            foreach (var file in files)
                            {
                                try
                                {
                                    var text = await ExtractTextSimple(file);
                                    if (!string.IsNullOrWhiteSpace(text))
                                    {
                                        allText.Add((file, text));
                                    }
                                }
                                catch { }
                            }
                        }
                    });

                var totalChars = allText.Sum(t => t.text.Length);
                var estimatedTokens = totalChars / 4;

                AnsiConsole.MarkupLine($"[bold]Documents:[/] {allText.Count}");
                AnsiConsole.MarkupLine($"[bold]Total size:[/] {totalChars:N0} chars (~{estimatedTokens:N0} tokens)");
                AnsiConsole.MarkupLine($"[bold]Strategy:[/] {strategy}");
                AnsiConsole.MarkupLine($"[bold]Query:[/] {query}");
                AnsiConsole.WriteLine();

                string finalAnswer;

                if (strategy == "single" && totalChars < 100000)
                {
                    finalAnswer = await ProcessSingleShot(client, string.Join("\n\n", allText.Select(t => t.text)), query);
                }
                else
                {
                    finalAnswer = await ProcessRecursive(client, allText, query, chunkSize);
                }

                AnsiConsole.MarkupLine("\n[bold]Answer:[/]");
                AnsiConsole.WriteLine(finalAnswer);
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            }

        }, documentArg, queryArg, strategyOption, chunkSizeOption);

        return command;
    }

    private static async Task<string> ProcessSingleShot(ILLMClient client, string text, string query)
    {
        var messages = new List<HazinaChatMessage>
        {
            new() { Role = HazinaMessageRole.System, Text = "Answer the question based on the provided document. Be thorough and cite specific sections when relevant." },
            new() { Role = HazinaMessageRole.User, Text = $"Document:\n{text}\n\nQuestion: {query}" }
        };

        var response = await client.GetResponse(messages, HazinaChatResponseFormat.Text, null, null, CancellationToken.None);
        return response.Result;
    }

    private static async Task<string> ProcessRecursive(ILLMClient client, List<(string source, string text)> documents, string query, int chunkSize)
    {
        var allText = string.Join("\n\n", documents.Select(d => $"=== {d.source} ===\n{d.text}"));

        if (allText.Length <= chunkSize * 2)
        {
            return await ProcessSingleShot(client, allText, query);
        }

        // Chunk and summarize
        var chunks = ChunkText(allText, chunkSize);
        var summaries = new List<string>();

        await AnsiConsole.Status()
            .StartAsync($"Processing {chunks.Count} chunks...", async ctx =>
            {
                for (int i = 0; i < chunks.Count; i++)
                {
                    ctx.Status($"Processing chunk {i + 1}/{chunks.Count}...");

                    var messages = new List<HazinaChatMessage>
                    {
                        new() { Role = HazinaMessageRole.System, Text = $"Summarize this chunk, focusing on information relevant to: {query}" },
                        new() { Role = HazinaMessageRole.User, Text = chunks[i] }
                    };

                    var response = await client.GetResponse(messages, HazinaChatResponseFormat.Text, null, null, CancellationToken.None);
                    summaries.Add(response.Result);
                }
            });

        var combinedSummaries = string.Join("\n\n", summaries);

        if (combinedSummaries.Length > chunkSize * 2)
        {
            return await ProcessRecursive(client, new List<(string, string)> { ("summaries", combinedSummaries) }, query, chunkSize);
        }

        return await ProcessSingleShot(client, combinedSummaries, query);
    }

    private static List<string> ChunkText(string text, int chunkSize)
    {
        var chunks = new List<string>();
        var paragraphs = text.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);
        var currentChunk = new System.Text.StringBuilder();

        foreach (var para in paragraphs)
        {
            if (currentChunk.Length + para.Length > chunkSize && currentChunk.Length > 0)
            {
                chunks.Add(currentChunk.ToString());
                currentChunk.Clear();
            }
            currentChunk.AppendLine(para);
        }

        if (currentChunk.Length > 0)
        {
            chunks.Add(currentChunk.ToString());
        }

        return chunks;
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
}
