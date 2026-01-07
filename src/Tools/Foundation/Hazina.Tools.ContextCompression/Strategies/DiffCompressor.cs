using Hazina.Tools.ContextCompression.Interfaces;
using Hazina.Tools.ContextCompression.Models;
using LibGit2Sharp;
using System.Text;

namespace Hazina.Tools.ContextCompression.Strategies;

/// <summary>
/// Diff-based compression that extracts only changed code with minimal context
/// </summary>
public class DiffCompressor : IContextCompressor
{
    private readonly ITokenCounter _tokenCounter;

    public DiffCompressor(ITokenCounter tokenCounter)
    {
        _tokenCounter = tokenCounter;
    }

    public bool SupportsStrategy(CompressionStrategy strategy)
    {
        return strategy == CompressionStrategy.Diff;
    }

    public async Task<CompressedContext> CompressAsync(
        CompressionRequest request,
        CompressionOptions options)
    {
        var result = new CompressedContext
        {
            StrategyUsed = CompressionStrategy.Diff,
            IncludedFiles = new List<string>(request.FilePaths)
        };

        var contentBuilder = new StringBuilder();
        int originalTokenCount = 0;

        // If repository path is provided, use git diff
        if (!string.IsNullOrEmpty(request.RepositoryPath) && Directory.Exists(Path.Combine(request.RepositoryPath, ".git")))
        {
            using var repo = new Repository(request.RepositoryPath);
            var diff = GenerateGitDiff(repo, request.FilePaths, options);
            contentBuilder.Append(diff);

            // Count original tokens from the files
            foreach (var filePath in request.FilePaths)
            {
                if (File.Exists(filePath))
                {
                    var code = await File.ReadAllTextAsync(filePath);
                    originalTokenCount += _tokenCounter.Count(code);
                }
            }
        }
        else
        {
            // Fall back to showing entire files with line numbers
            foreach (var filePath in request.FilePaths)
            {
                if (!File.Exists(filePath))
                    continue;

                var code = await File.ReadAllTextAsync(filePath);
                originalTokenCount += _tokenCounter.Count(code);

                var compressed = FormatFileWithLineNumbers(code, filePath, options);
                contentBuilder.AppendLine(compressed);
            }
        }

        result.Content = contentBuilder.ToString();
        result.OriginalTokens = originalTokenCount;
        result.CompressedTokens = _tokenCounter.Count(result.Content);

        return result;
    }

    private string GenerateGitDiff(Repository repo, List<string> filePaths, CompressionOptions options)
    {
        var output = new StringBuilder();

        // Get changes between working directory and HEAD
        var changes = repo.Diff.Compare<TreeChanges>(repo.Head.Tip?.Tree, DiffTargets.WorkingDirectory);

        foreach (var change in changes)
        {
            var fullPath = Path.Combine(repo.Info.WorkingDirectory, change.Path);

            // Only include requested files
            if (filePaths.Count > 0 && !filePaths.Any(f => Path.GetFullPath(f) == Path.GetFullPath(fullPath)))
                continue;

            output.AppendLine($"File: {change.Path}");
            output.AppendLine($"Status: {change.Status}");
            output.AppendLine();

            // Get the patch
            var patch = repo.Diff.Compare<Patch>(repo.Head.Tip?.Tree, DiffTargets.WorkingDirectory, new[] { change.Path });

            foreach (var patchEntry in patch)
            {
                output.AppendLine(FormatPatch(patchEntry, options));
            }

            output.AppendLine();
        }

        // If no changes found, show staged changes
        if (output.Length == 0)
        {
            var stagedChanges = repo.Diff.Compare<TreeChanges>(repo.Head.Tip?.Tree, DiffTargets.Index);
            foreach (var change in stagedChanges)
            {
                var fullPath = Path.Combine(repo.Info.WorkingDirectory, change.Path);

                if (filePaths.Count > 0 && !filePaths.Any(f => Path.GetFullPath(f) == Path.GetFullPath(fullPath)))
                    continue;

                output.AppendLine($"File: {change.Path} (staged)");
                output.AppendLine($"Status: {change.Status}");
                output.AppendLine();

                var patch = repo.Diff.Compare<Patch>(repo.Head.Tip?.Tree, DiffTargets.Index, new[] { change.Path });

                foreach (var patchEntry in patch)
                {
                    output.AppendLine(FormatPatch(patchEntry, options));
                }

                output.AppendLine();
            }
        }

        return output.ToString();
    }

    private string FormatPatch(PatchEntryChanges patch, CompressionOptions options)
    {
        var output = new StringBuilder();

        output.AppendLine($"@@ Changes in {patch.Path} @@");
        output.AppendLine(patch.Patch);

        return output.ToString();
    }

    private string FormatFileWithLineNumbers(string code, string filePath, CompressionOptions options)
    {
        var output = new StringBuilder();

        if (options.IncludeFilePaths)
        {
            output.AppendLine($"// File: {filePath}");
        }

        var lines = code.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            if (options.IncludeLineNumbers)
            {
                output.AppendLine($"{i + 1,4}: {lines[i]}");
            }
            else
            {
                output.AppendLine(lines[i]);
            }
        }

        return output.ToString();
    }
}
