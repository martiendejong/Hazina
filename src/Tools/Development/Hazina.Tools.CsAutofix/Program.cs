using System;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Formatting;

namespace CsAutoFix
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            // Simple command line parsing
            string projectPath = Directory.GetCurrentDirectory();
            bool fixUsings = true;
            bool dryRun = false;
            bool verbose = false;

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--project":
                    case "-p":
                        if (i + 1 < args.Length)
                            projectPath = args[++i];
                        break;
                    case "--no-fix-usings":
                        fixUsings = false;
                        break;
                    case "--dry-run":
                    case "-d":
                        dryRun = true;
                        break;
                    case "--verbose":
                    case "-v":
                        verbose = true;
                        break;
                    case "--help":
                    case "-h":
                        ShowHelp();
                        return 0;
                }
            }

            await RunAutoFix(projectPath, fixUsings, dryRun, verbose);
            return 0;
        }

        static void ShowHelp()
        {
            Console.WriteLine("CS-AutoFix - Automatically fix common C# compile errors");
            Console.WriteLine();
            Console.WriteLine("Usage: cs-autofix [options]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --project, -p <path>    Path to .csproj or .sln file (default: current directory)");
            Console.WriteLine("  --no-fix-usings         Skip fixing usings");
            Console.WriteLine("  --dry-run, -d           Show what would be fixed without making changes");
            Console.WriteLine("  --verbose, -v           Show detailed output");
            Console.WriteLine("  --help, -h              Show this help message");
        }

        static async Task RunAutoFix(string projectPath, bool fixUsings, bool dryRun, bool verbose)
        {
            try
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] CS-AutoFix Starting...");
                Console.WriteLine($"  Project: {projectPath}");
                Console.WriteLine($"  Fix Usings: {fixUsings}");
                Console.WriteLine($"  Dry Run: {dryRun}");
                Console.WriteLine();

                // Find project/solution file
                string? targetFile = FindProjectFile(projectPath);
                if (targetFile == null)
                {
                    Console.WriteLine("❌ No .csproj or .sln file found");
                    return;
                }

                Console.WriteLine($"  Target: {targetFile}");
                Console.WriteLine();

                // Load workspace
                using var workspace = MSBuildWorkspace.Create();
#pragma warning disable CS0618
                workspace.WorkspaceFailed += (sender, e) =>
                {
                    if (verbose)
                    {
                        Console.WriteLine($"  ⚠️  Workspace warning: {e.Diagnostic.Message}");
                    }
                };
#pragma warning restore CS0618

                Console.WriteLine("Loading workspace...");
                Project? project = null;

                if (targetFile.EndsWith(".sln"))
                {
                    var solution = await workspace.OpenSolutionAsync(targetFile);
                    project = solution.Projects.FirstOrDefault();
                    Console.WriteLine($"  Solution loaded: {solution.Projects.Count()} projects");
                }
                else
                {
                    project = await workspace.OpenProjectAsync(targetFile);
                    Console.WriteLine($"  Project loaded: {project.Name}");
                }

                if (project == null)
                {
                    Console.WriteLine("❌ Could not load project");
                    return;
                }

                Console.WriteLine();

                // Process documents
                int filesProcessed = 0;
                int filesFixed = 0;

                foreach (var document in project.Documents)
                {
                    if (!document.FilePath?.EndsWith(".cs") ?? true)
                        continue;

                    filesProcessed++;

                    var syntaxTree = await document.GetSyntaxTreeAsync();
                    if (syntaxTree == null)
                        continue;

                    var root = await syntaxTree.GetRootAsync();
                    var originalRoot = root;

                    if (fixUsings)
                    {
                        // Remove unused usings
                        var semanticModel = await document.GetSemanticModelAsync();
                        if (semanticModel != null)
                        {
                            root = RemoveUnusedUsings(root, semanticModel);
                        }

                        // TODO: Add missing usings (requires more complex analysis)
                    }

                    if (root != originalRoot)
                    {
                        filesFixed++;

                        if (verbose)
                        {
                            Console.WriteLine($"  ✓ Fixed: {Path.GetFileName(document.FilePath)}");
                        }

                        if (!dryRun)
                        {
                            var formattedRoot = Formatter.Format(root, workspace);
                            var newText = formattedRoot.ToFullString();
                            File.WriteAllText(document.FilePath!, newText);
                        }
                    }
                }

                Console.WriteLine();
                Console.WriteLine($"✓ Processing complete");
                Console.WriteLine($"  Files processed: {filesProcessed}");
                Console.WriteLine($"  Files fixed: {filesFixed}");

                if (dryRun && filesFixed > 0)
                {
                    Console.WriteLine();
                    Console.WriteLine("  (Dry run - no files were modified)");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
                if (verbose)
                {
                    Console.WriteLine(ex.StackTrace);
                }
            }
        }

        static string? FindProjectFile(string path)
        {
            if (File.Exists(path) && (path.EndsWith(".csproj") || path.EndsWith(".sln")))
            {
                return path;
            }

            if (Directory.Exists(path))
            {
                // Try .sln first
                var slnFiles = Directory.GetFiles(path, "*.sln");
                if (slnFiles.Length > 0)
                    return slnFiles[0];

                // Try .csproj
                var csprojFiles = Directory.GetFiles(path, "*.csproj");
                if (csprojFiles.Length > 0)
                    return csprojFiles[0];

                // Try subdirectories
                var allCsprojFiles = Directory.GetFiles(path, "*.csproj", SearchOption.AllDirectories);
                if (allCsprojFiles.Length > 0)
                    return allCsprojFiles[0];
            }

            return null;
        }

        static SyntaxNode RemoveUnusedUsings(SyntaxNode root, SemanticModel semanticModel)
        {
            var usingsToRemove = root.DescendantNodes()
                .OfType<UsingDirectiveSyntax>()
                .Where(u => !IsUsingUsed(u, root, semanticModel))
                .ToList();

            if (usingsToRemove.Any())
            {
                root = root.RemoveNodes(usingsToRemove, SyntaxRemoveOptions.KeepNoTrivia)!;
            }

            return root;
        }

        static bool IsUsingUsed(UsingDirectiveSyntax usingDirective, SyntaxNode root, SemanticModel semanticModel)
        {
            // Simple heuristic: check if namespace name appears in code
            var namespaceName = usingDirective.Name?.ToString();
            if (string.IsNullOrEmpty(namespaceName))
                return true; // Keep if we can't determine

            // This is a simplified check - a full implementation would use semantic analysis
            var codeText = root.ToString();
            var lastPart = namespaceName.Split('.').Last();

            return codeText.Contains(lastPart);
        }
    }
}
