using Hazina.App.HazinaCoder.Core.Events;
using Hazina.App.HazinaCoder.Core.Vision.Providers;
using Spectre.Console;

namespace Hazina.App.HazinaCoder.Core.Vision;

/// <summary>
/// High-level vision tools for HazinaCoder
/// </summary>
public class VisionTools
{
    private readonly VisionAnalyzer _analyzer;
    private readonly IEventBus? _eventBus;

    public VisionTools(IEventBus? eventBus = null)
    {
        _eventBus = eventBus;

        // Try Claude first, fallback to OpenAI
        IVisionProvider provider;
        var claudeProvider = new ClaudeVisionProvider();
        var openAIProvider = new OpenAIVisionProvider();

        if (claudeProvider.IsAvailable)
        {
            provider = claudeProvider;
        }
        else if (openAIProvider.IsAvailable)
        {
            provider = openAIProvider;
        }
        else
        {
            throw new InvalidOperationException(
                "No vision provider available. Configure ANTHROPIC_API_KEY or OPENAI_API_KEY environment variable.");
        }

        var cache = new VisionCache();
        _analyzer = new VisionAnalyzer(provider, cache, eventBus);
    }

    public VisionTools(VisionAnalyzer analyzer, IEventBus? eventBus = null)
    {
        _analyzer = analyzer;
        _eventBus = eventBus;
    }

    /// <summary>
    /// Analyze screenshot tool
    /// </summary>
    public async Task<string> AnalyzeScreenshotAsync(string screenshotPath, string? specificQuery = null)
    {
        AnsiConsole.MarkupLine($"[yellow]Analyzing screenshot:[/] {screenshotPath}");

        var query = specificQuery ?? "Analyze this screenshot. Describe what you see, identify UI elements, and extract any visible text.";

        var result = await _analyzer.AnalyzeImageAsync(screenshotPath, query);

        if (result.Success)
        {
            AnsiConsole.MarkupLine("[green]✓[/] Analysis complete");
            return result.Analysis;
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]✗[/] Analysis failed: {result.Error}");
            return $"Error: {result.Error}";
        }
    }

    /// <summary>
    /// Extract text from image tool
    /// </summary>
    public async Task<string> ExtractTextAsync(string imagePath)
    {
        AnsiConsole.MarkupLine($"[yellow]Extracting text from:[/] {imagePath}");

        var result = await _analyzer.ExtractTextAsync(imagePath);

        if (result.Success)
        {
            AnsiConsole.MarkupLine("[green]✓[/] Text extracted");
            return result.Text;
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]✗[/] Extraction failed: {result.Error}");
            return $"Error: {result.Error}";
        }
    }

    /// <summary>
    /// Analyze UI elements tool
    /// </summary>
    public async Task<UIAnalysisResult> AnalyzeUIAsync(string screenshotPath)
    {
        AnsiConsole.MarkupLine($"[yellow]Analyzing UI elements:[/] {screenshotPath}");

        var result = await _analyzer.AnalyzeUIAsync(screenshotPath, UIAnalysisMode.Full);

        if (result.Success)
        {
            AnsiConsole.MarkupLine("[green]✓[/] UI analysis complete");

            // Display results in table
            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("Element")
                .AddColumn("Details");

            table.AddRow("Layout", result.Layout);
            if (result.Elements.Any())
            {
                table.AddRow("Elements Found", result.Elements.Count.ToString());
            }

            AnsiConsole.Write(table);
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]✗[/] UI analysis failed: {result.Error}");
        }

        return result;
    }

    /// <summary>
    /// Analyze diagram tool
    /// </summary>
    public async Task<DiagramAnalysisResult> AnalyzeDiagramAsync(
        string imagePath,
        DiagramType? expectedType = null)
    {
        var typeName = expectedType?.ToString() ?? "diagram";
        AnsiConsole.MarkupLine($"[yellow]Analyzing {typeName}:[/] {imagePath}");

        var result = await _analyzer.AnalyzeDiagramAsync(imagePath, expectedType);

        if (result.Success)
        {
            AnsiConsole.MarkupLine("[green]✓[/] Diagram analysis complete");

            // Display results
            var panel = new Panel(result.Description)
                .Header($"[yellow]{result.DiagramType}[/]")
                .Border(BoxBorder.Rounded);

            AnsiConsole.Write(panel);

            if (result.Elements.Any())
            {
                AnsiConsole.MarkupLine($"[dim]Elements: {result.Elements.Count}[/]");
            }

            if (result.Connections.Any())
            {
                AnsiConsole.MarkupLine($"[dim]Connections: {result.Connections.Count}[/]");
            }
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]✗[/] Diagram analysis failed: {result.Error}");
        }

        return result;
    }

    /// <summary>
    /// Compare two screenshots tool
    /// </summary>
    public async Task<ImageComparisonResult> CompareScreenshotsAsync(
        string screenshot1Path,
        string screenshot2Path)
    {
        AnsiConsole.MarkupLine("[yellow]Comparing screenshots...[/]");

        var result = await _analyzer.CompareImagesAsync(screenshot1Path, screenshot2Path);

        if (result.Success)
        {
            if (result.AreSimilar)
            {
                AnsiConsole.MarkupLine("[green]✓[/] Screenshots are similar");
            }
            else
            {
                AnsiConsole.MarkupLine("[yellow]⚠[/] Screenshots have differences:");

                foreach (var diff in result.Differences)
                {
                    AnsiConsole.MarkupLine($"  [dim]•[/] {diff}");
                }
            }
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]✗[/] Comparison failed: {result.Error}");
        }

        return result;
    }

    /// <summary>
    /// Batch analyze images tool
    /// </summary>
    public async Task<List<VisionAnalysisResult>> BatchAnalyzeAsync(
        List<string> imagePaths,
        string query)
    {
        AnsiConsole.MarkupLine($"[yellow]Batch analyzing {imagePaths.Count} images...[/]");

        var results = await _analyzer.AnalyzeBatchAsync(imagePaths, query);

        var successCount = results.Count(r => r.Success);
        var failCount = results.Count - successCount;

        AnsiConsole.MarkupLine($"[green]✓[/] Complete: {successCount} succeeded, {failCount} failed");

        return results;
    }

    /// <summary>
    /// Interactive image Q&A tool
    /// </summary>
    public async Task<string> InteractiveImageQAAsync(string imagePath)
    {
        AnsiConsole.Clear();
        AnsiConsole.MarkupLine($"[bold yellow]Interactive Image Q&A[/]");
        AnsiConsole.MarkupLine($"[dim]Image: {imagePath}[/]");
        AnsiConsole.WriteLine();

        while (true)
        {
            var question = AnsiConsole.Prompt(
                new TextPrompt<string>("[yellow]Ask about the image[/] (or 'exit'):")
                    .AllowEmpty()
            );

            if (string.IsNullOrWhiteSpace(question) || question.ToLower() == "exit")
            {
                break;
            }

            var result = await _analyzer.AnalyzeImageAsync(imagePath, question);

            if (result.Success)
            {
                var panel = new Panel(result.Analysis)
                    .Header("[green]Answer[/]")
                    .Border(BoxBorder.Rounded);

                AnsiConsole.Write(panel);
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] {result.Error}");
            }

            AnsiConsole.WriteLine();
        }

        return "Interactive session ended.";
    }

    /// <summary>
    /// Get vision cache statistics
    /// </summary>
    public VisionCacheStatistics GetCacheStatistics()
    {
        // Would need access to cache instance - simplified for now
        return new VisionCacheStatistics
        {
            TotalEntries = 0,
            TotalHits = 0,
            HitRate = 0,
            CacheSizeBytes = 0,
            MaxCacheSize = 1000,
            CacheDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".hazinacoder",
                "vision-cache"
            )
        };
    }
}
