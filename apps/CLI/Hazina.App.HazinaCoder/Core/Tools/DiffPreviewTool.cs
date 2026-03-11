using DiffPlex;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using Spectre.Console;
using System.Text;

namespace Hazina.App.HazinaCoder.Core.Tools;

/// <summary>
/// Interactive diff preview tool with syntax highlighting
/// </summary>
public class DiffPreviewTool
{
    private readonly IInlineDiffBuilder _diffBuilder;
    private readonly ISideBySideDiffBuilder _sideBySideDiffBuilder;

    public DiffPreviewTool()
    {
        var differ = new Differ();
        _diffBuilder = new InlineDiffBuilder(differ);
        _sideBySideDiffBuilder = new SideBySideDiffBuilder(differ);
    }

    /// <summary>
    /// Preview file changes before applying
    /// </summary>
    public async Task<DiffApprovalResult> PreviewAndApproveAsync(string filePath, string newContent)
    {
        if (!File.Exists(filePath))
        {
            AnsiConsole.MarkupLine($"[yellow]File doesn't exist yet: {filePath}[/]");
            AnsiConsole.MarkupLine("[dim]This will create a new file.[/]");

            return await PromptForApprovalAsync(filePath, "", newContent, isNewFile: true);
        }

        var oldContent = await File.ReadAllTextAsync(filePath);

        if (oldContent == newContent)
        {
            AnsiConsole.MarkupLine("[green]No changes detected[/]");
            return new DiffApprovalResult(DiffAction.Skip, "No changes");
        }

        return await PromptForApprovalAsync(filePath, oldContent, newContent, isNewFile: false);
    }

    /// <summary>
    /// Show unified diff with syntax highlighting
    /// </summary>
    public void ShowUnifiedDiff(string filePath, string oldContent, string newContent)
    {
        var diff = _diffBuilder.BuildDiffModel(oldContent, newContent);

        var panel = new Panel(RenderDiffLines(diff.Lines, filePath))
            .Header($"[bold yellow]Changes to {Path.GetFileName(filePath)}[/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Yellow);

        AnsiConsole.Write(panel);

        // Show stats
        var addedLines = diff.Lines.Count(l => l.Type == ChangeType.Inserted);
        var deletedLines = diff.Lines.Count(l => l.Type == ChangeType.Deleted);
        var modifiedLines = diff.Lines.Count(l => l.Type == ChangeType.Modified);

        AnsiConsole.MarkupLine($"[dim]Lines: [green]+{addedLines}[/] [red]-{deletedLines}[/] [yellow]~{modifiedLines}[/][/]");
    }

    /// <summary>
    /// Show side-by-side diff
    /// </summary>
    public void ShowSideBySideDiff(string filePath, string oldContent, string newContent)
    {
        var diff = _sideBySideDiffBuilder.BuildDiffModel(oldContent, newContent);

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn(new TableColumn("[bold]Before[/]").Width(60))
            .AddColumn(new TableColumn("[bold]After[/]").Width(60));

        var oldLines = diff.OldText.Lines;
        var newLines = diff.NewText.Lines;
        var maxLines = Math.Max(oldLines.Count, newLines.Count);

        for (int i = 0; i < maxLines && i < 50; i++) // Limit to 50 lines for display
        {
            var oldLine = i < oldLines.Count ? FormatDiffLine(oldLines[i]) : "";
            var newLine = i < newLines.Count ? FormatDiffLine(newLines[i]) : "";
            table.AddRow(oldLine, newLine);
        }

        if (maxLines > 50)
        {
            table.AddRow("[dim]... (truncated)[/]", "[dim]... (truncated)[/]");
        }

        AnsiConsole.Write(table);
    }

    /// <summary>
    /// Render diff lines with color coding
    /// </summary>
    private string RenderDiffLines(List<DiffPiece> lines, string filePath)
    {
        var sb = new StringBuilder();
        var extension = Path.GetExtension(filePath).ToLower();

        // Show first 100 lines only
        var displayLines = lines.Take(100).ToList();

        foreach (var line in displayLines)
        {
            var text = line.Text ?? "";
            var escapedText = text.Replace("[", "[[").Replace("]", "]]");

            switch (line.Type)
            {
                case ChangeType.Inserted:
                    sb.AppendLine($"[green]+{escapedText}[/]");
                    break;
                case ChangeType.Deleted:
                    sb.AppendLine($"[red]-{escapedText}[/]");
                    break;
                case ChangeType.Modified:
                    sb.AppendLine($"[yellow]~{escapedText}[/]");
                    break;
                case ChangeType.Imaginary:
                    // Skip imaginary lines
                    break;
                default:
                    sb.AppendLine($"[dim] {escapedText}[/]");
                    break;
            }
        }

        if (lines.Count > 100)
        {
            sb.AppendLine($"[dim]... ({lines.Count - 100} more lines)[/]");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Format single diff line
    /// </summary>
    private string FormatDiffLine(DiffPiece line)
    {
        if (line == null || line.Text == null)
            return "";

        var text = line.Text.Replace("[", "[[").Replace("]", "]]");

        return line.Type switch
        {
            ChangeType.Inserted => $"[green]{text}[/]",
            ChangeType.Deleted => $"[red]{text}[/]",
            ChangeType.Modified => $"[yellow]{text}[/]",
            _ => $"[dim]{text}[/]"
        };
    }

    /// <summary>
    /// Prompt user for approval
    /// </summary>
    private async Task<DiffApprovalResult> PromptForApprovalAsync(
        string filePath,
        string oldContent,
        string newContent,
        bool isNewFile)
    {
        if (!isNewFile)
        {
            ShowUnifiedDiff(filePath, oldContent, newContent);
        }
        else
        {
            AnsiConsole.MarkupLine("[bold yellow]New file content:[/]");
            var lines = newContent.Split('\n').Take(30);
            foreach (var line in lines)
            {
                var escaped = line.Replace("[", "[[").Replace("]", "]]");
                AnsiConsole.MarkupLine($"[dim]{escaped}[/]");
            }
            if (newContent.Split('\n').Length > 30)
            {
                AnsiConsole.MarkupLine($"[dim]... ({newContent.Split('\n').Length - 30} more lines)[/]");
            }
        }

        AnsiConsole.WriteLine();

        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[bold]What would you like to do?[/]")
                .AddChoices(new[]
                {
                    "✓ Apply changes",
                    "✗ Reject changes",
                    "📝 Edit changes",
                    "👁 View full diff",
                    "🔀 Side-by-side view",
                    "💾 Save to file"
                })
        );

        return choice switch
        {
            "✓ Apply changes" => new DiffApprovalResult(DiffAction.Apply, newContent),
            "✗ Reject changes" => new DiffApprovalResult(DiffAction.Reject, "User rejected"),
            "📝 Edit changes" => await HandleEditAsync(filePath, newContent),
            "👁 View full diff" => await HandleViewFullAsync(filePath, oldContent, newContent),
            "🔀 Side-by-side view" => await HandleSideBySideAsync(filePath, oldContent, newContent),
            "💾 Save to file" => await HandleSaveAsync(filePath, newContent),
            _ => new DiffApprovalResult(DiffAction.Reject, "Invalid choice")
        };
    }

    /// <summary>
    /// Handle edit action
    /// </summary>
    private async Task<DiffApprovalResult> HandleEditAsync(string filePath, string newContent)
    {
        AnsiConsole.MarkupLine("[yellow]Opening in default editor...[/]");

        // Create temporary file
        var tempPath = Path.Combine(Path.GetTempPath(), $"hazinacoder-edit-{Guid.NewGuid()}.txt");
        await File.WriteAllTextAsync(tempPath, newContent);

        try
        {
            // Open in default editor
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = tempPath,
                UseShellExecute = true
            };
            var process = System.Diagnostics.Process.Start(psi);
            if (process != null)
            {
                await process.WaitForExitAsync();
            }

            // Read edited content
            var editedContent = await File.ReadAllTextAsync(tempPath);

            // Show changes again
            return await PreviewAndApproveAsync(filePath, editedContent);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    /// <summary>
    /// Handle view full diff
    /// </summary>
    private async Task<DiffApprovalResult> HandleViewFullAsync(string filePath, string oldContent, string newContent)
    {
        var diff = _diffBuilder.BuildDiffModel(oldContent, newContent);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Full Diff:[/]");
        AnsiConsole.WriteLine();

        foreach (var line in diff.Lines)
        {
            var text = line.Text?.Replace("[", "[[").Replace("]", "]]") ?? "";

            switch (line.Type)
            {
                case ChangeType.Inserted:
                    AnsiConsole.MarkupLine($"[green]+{text}[/]");
                    break;
                case ChangeType.Deleted:
                    AnsiConsole.MarkupLine($"[red]-{text}[/]");
                    break;
                case ChangeType.Modified:
                    AnsiConsole.MarkupLine($"[yellow]~{text}[/]");
                    break;
                default:
                    AnsiConsole.MarkupLine($"[dim] {text}[/]");
                    break;
            }
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Press any key to continue...[/]");
        Console.ReadKey(true);

        return await PromptForApprovalAsync(filePath, oldContent, newContent, false);
    }

    /// <summary>
    /// Handle side-by-side view
    /// </summary>
    private async Task<DiffApprovalResult> HandleSideBySideAsync(string filePath, string oldContent, string newContent)
    {
        ShowSideBySideDiff(filePath, oldContent, newContent);
        AnsiConsole.WriteLine();

        return await PromptForApprovalAsync(filePath, oldContent, newContent, false);
    }

    /// <summary>
    /// Handle save to file
    /// </summary>
    private async Task<DiffApprovalResult> HandleSaveAsync(string filePath, string newContent)
    {
        var savePath = AnsiConsole.Ask<string>("[yellow]Save path:[/]", Path.Combine(
            Path.GetDirectoryName(filePath) ?? "",
            $"{Path.GetFileNameWithoutExtension(filePath)}_preview{Path.GetExtension(filePath)}"
        ));

        await File.WriteAllTextAsync(savePath, newContent);
        AnsiConsole.MarkupLine($"[green]Saved to: {savePath}[/]");

        return await PromptForApprovalAsync(filePath, File.ReadAllText(filePath), newContent, false);
    }

    /// <summary>
    /// Generate unified diff patch
    /// </summary>
    public string GenerateUnifiedDiff(string filePath, string oldContent, string newContent)
    {
        var diff = _diffBuilder.BuildDiffModel(oldContent, newContent);
        var sb = new StringBuilder();

        sb.AppendLine($"--- a/{filePath}");
        sb.AppendLine($"+++ b/{filePath}");
        sb.AppendLine($"@@ -1,{oldContent.Split('\n').Length} +1,{newContent.Split('\n').Length} @@");

        foreach (var line in diff.Lines)
        {
            var prefix = line.Type switch
            {
                ChangeType.Inserted => "+",
                ChangeType.Deleted => "-",
                _ => " "
            };

            sb.AppendLine($"{prefix}{line.Text}");
        }

        return sb.ToString();
    }
}

// ===== Data Models =====

public enum DiffAction
{
    Apply,
    Reject,
    Skip,
    Edit
}

public record DiffApprovalResult(DiffAction Action, string Content);
