using Hazina.App.HazinaCoder.Core.Events;
using Hazina.App.HazinaCoder.Core.Providers;
using Spectre.Console;

namespace Hazina.App.HazinaCoder.Core.Testing;

/// <summary>
/// High-level tool for test generation
/// Provides CLI-friendly interface for generating tests
/// </summary>
public class TestGeneratorTool
{
    private readonly TestGenerator _generator;
    private readonly IEventBus? _eventBus;

    public TestGeneratorTool(ProviderRouter providerRouter, IEventBus? eventBus = null)
    {
        _generator = new TestGenerator(providerRouter, TestFramework.XUnit, eventBus);
        _eventBus = eventBus;
    }

    public TestGeneratorTool(TestGenerator generator, IEventBus? eventBus = null)
    {
        _generator = generator;
        _eventBus = eventBus;
    }

    /// <summary>
    /// Generate tests for a file
    /// </summary>
    public async Task<string> GenerateTestsForFileAsync(
        string sourceFile,
        string? outputFile = null,
        TestGenerationOptions? options = null)
    {
        AnsiConsole.MarkupLine($"[yellow]Generating tests for:[/] {sourceFile}");

        if (!File.Exists(sourceFile))
        {
            AnsiConsole.MarkupLine($"[red]✗[/] File not found: {sourceFile}");
            return string.Empty;
        }

        var sourceCode = await File.ReadAllTextAsync(sourceFile);
        var className = Path.GetFileNameWithoutExtension(sourceFile);

        options ??= new TestGenerationOptions();
        options.ClassName = className;

        var result = await _generator.GenerateTestsAsync(sourceCode, options);

        if (result.Success)
        {
            AnsiConsole.MarkupLine($"[green]✓[/] Generated {result.TestCount} tests ({result.CoverageEstimate}% estimated coverage)");
            AnsiConsole.MarkupLine($"[dim]Cost: ${result.LLMCost:F4}, Duration: {result.Duration.TotalSeconds:F1}s[/]");

            // Save to file if specified
            if (outputFile != null)
            {
                await File.WriteAllTextAsync(outputFile, result.TestCode);
                AnsiConsole.MarkupLine($"[green]✓[/] Saved to: {outputFile}");
            }
            else
            {
                // Display in console
                var panel = new Panel(result.TestCode)
                    .Header($"[yellow]Generated Tests for {className}[/]")
                    .Border(BoxBorder.Rounded);

                AnsiConsole.Write(panel);
            }

            return result.TestCode;
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]✗[/] Test generation failed: {result.Error}");
            return string.Empty;
        }
    }

    /// <summary>
    /// Generate tests for entire project
    /// </summary>
    public async Task GenerateTestsForProjectAsync(
        string projectDirectory,
        string outputDirectory,
        string? filePattern = "*.cs")
    {
        AnsiConsole.MarkupLine($"[yellow]Generating tests for project:[/] {projectDirectory}");

        if (!Directory.Exists(projectDirectory))
        {
            AnsiConsole.MarkupLine($"[red]✗[/] Directory not found: {projectDirectory}");
            return;
        }

        // Find all source files
        var sourceFiles = Directory.GetFiles(projectDirectory, filePattern!, SearchOption.AllDirectories)
            .Where(f => !f.Contains("\\bin\\") && !f.Contains("\\obj\\") && !f.Contains("\\Tests\\"))
            .ToList();

        if (!sourceFiles.Any())
        {
            AnsiConsole.MarkupLine($"[yellow]⚠[/] No source files found matching pattern: {filePattern}");
            return;
        }

        AnsiConsole.MarkupLine($"[dim]Found {sourceFiles.Count} files to process[/]");
        Directory.CreateDirectory(outputDirectory);

        var results = await _generator.GenerateBatchTestsAsync(sourceFiles);

        // Save results
        var successCount = 0;
        var totalTests = 0;
        var totalCost = 0.0;

        for (int i = 0; i < sourceFiles.Count; i++)
        {
            var sourceFile = sourceFiles[i];
            var result = results[i];

            if (result.Success)
            {
                var relativePath = Path.GetRelativePath(projectDirectory, sourceFile);
                var testFileName = Path.GetFileNameWithoutExtension(sourceFile) + "Tests.cs";
                var testFilePath = Path.Combine(outputDirectory, testFileName);

                await File.WriteAllTextAsync(testFilePath, result.TestCode);

                successCount++;
                totalTests += result.TestCount;
                totalCost += result.LLMCost;

                AnsiConsole.MarkupLine($"[green]✓[/] {relativePath} → {testFileName} ({result.TestCount} tests)");
            }
            else
            {
                var relativePath = Path.GetRelativePath(projectDirectory, sourceFile);
                AnsiConsole.MarkupLine($"[red]✗[/] {relativePath}: {result.Error}");
            }
        }

        // Summary
        AnsiConsole.WriteLine();
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Metric")
            .AddColumn("Value");

        table.AddRow("Files Processed", sourceFiles.Count.ToString());
        table.AddRow("Successful", successCount.ToString());
        table.AddRow("Failed", (sourceFiles.Count - successCount).ToString());
        table.AddRow("Total Tests Generated", totalTests.ToString());
        table.AddRow("Total Cost", $"${totalCost:F4}");
        table.AddRow("Output Directory", outputDirectory);

        AnsiConsole.Write(table);
    }

    /// <summary>
    /// Interactive test generation mode
    /// </summary>
    public async Task InteractiveModeAsync()
    {
        AnsiConsole.Clear();
        AnsiConsole.MarkupLine("[bold yellow]Interactive Test Generation[/]");
        AnsiConsole.WriteLine();

        while (true)
        {
            var action = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[yellow]What would you like to do?[/]")
                    .AddChoices(
                        "Generate tests for a file",
                        "Generate tests for a project",
                        "Configure options",
                        "Exit"
                    )
            );

            if (action == "Exit")
            {
                break;
            }

            switch (action)
            {
                case "Generate tests for a file":
                    await HandleGenerateFileAsync();
                    break;

                case "Generate tests for a project":
                    await HandleGenerateProjectAsync();
                    break;

                case "Configure options":
                    HandleConfigureOptions();
                    break;
            }

            AnsiConsole.WriteLine();
        }
    }

    private async Task HandleGenerateFileAsync()
    {
        var sourceFile = AnsiConsole.Prompt(
            new TextPrompt<string>("[yellow]Enter source file path:[/]")
        );

        var shouldSave = AnsiConsole.Confirm("Save to file?", true);

        string? outputFile = null;
        if (shouldSave)
        {
            var defaultOutput = Path.GetFileNameWithoutExtension(sourceFile) + "Tests.cs";
            outputFile = AnsiConsole.Prompt(
                new TextPrompt<string>("[yellow]Output file:[/]")
                    .DefaultValue(defaultOutput)
            );
        }

        await GenerateTestsForFileAsync(sourceFile, outputFile);
    }

    private async Task HandleGenerateProjectAsync()
    {
        var projectDir = AnsiConsole.Prompt(
            new TextPrompt<string>("[yellow]Enter project directory:[/]")
                .DefaultValue(Directory.GetCurrentDirectory())
        );

        var outputDir = AnsiConsole.Prompt(
            new TextPrompt<string>("[yellow]Output directory:[/]")
                .DefaultValue(Path.Combine(projectDir, "GeneratedTests"))
        );

        var pattern = AnsiConsole.Prompt(
            new TextPrompt<string>("[yellow]File pattern:[/]")
                .DefaultValue("*.cs")
        );

        await GenerateTestsForProjectAsync(projectDir, outputDir, pattern);
    }

    private void HandleConfigureOptions()
    {
        AnsiConsole.MarkupLine("[yellow]Configuration options coming soon...[/]");
    }
}
