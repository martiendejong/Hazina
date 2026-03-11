using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Hazina.Indexing.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hazina.Indexing.LocalSystem;

/// <summary>
/// High-performance structural indexer for local file systems.
/// Phase A implementation: Parallel directory traversal without content reading.
/// </summary>
public class StructuralIndexer : IStructuralIndexer
{
    private readonly ILogger<StructuralIndexer> _logger;

    // MIME type mapping (subset - extend as needed)
    private static readonly Dictionary<string, string> MimeTypes = new()
    {
        { ".txt", "text/plain" },
        { ".cs", "text/x-csharp" },
        { ".json", "application/json" },
        { ".xml", "application/xml" },
        { ".yaml", "text/yaml" },
        { ".yml", "text/yaml" },
        { ".md", "text/markdown" },
        { ".html", "text/html" },
        { ".css", "text/css" },
        { ".js", "application/javascript" },
        { ".ts", "application/typescript" },
        { ".jsx", "application/javascript" },
        { ".tsx", "application/typescript" },
        { ".py", "text/x-python" },
        { ".java", "text/x-java" },
        { ".sql", "application/sql" },
        { ".sh", "application/x-sh" },
        { ".ps1", "application/x-powershell" },
        { ".bat", "application/x-bat" },
        { ".cmd", "application/x-cmd" },
        { ".png", "image/png" },
        { ".jpg", "image/jpeg" },
        { ".jpeg", "image/jpeg" },
        { ".gif", "image/gif" },
        { ".svg", "image/svg+xml" },
        { ".pdf", "application/pdf" },
        { ".zip", "application/zip" },
        { ".dll", "application/x-msdownload" },
        { ".exe", "application/x-msdownload" }
    };

    public StructuralIndexer(ILogger<StructuralIndexer>? logger = null)
    {
        _logger = logger ?? NullLogger<StructuralIndexer>.Instance;
    }

    public async Task<StructuralIndex> IndexDirectoryAsync(string rootPath, IndexOptions options)
    {
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation("Starting structural indexation of {RootPath}", rootPath);

        if (!Directory.Exists(rootPath))
        {
            throw new DirectoryNotFoundException($"Root path not found: {rootPath}");
        }

        // Parallel directory traversal for maximum performance
        var files = await Task.Run(() =>
        {
            try
            {
                return Directory.EnumerateFiles(rootPath, "*.*", SearchOption.AllDirectories)
                    .AsParallel()
                    .WithDegreeOfParallelism(Environment.ProcessorCount)
                    .Where(f => !IsExcluded(f, options.ExcludePatterns))
                    .Where(f => new FileInfo(f).Length <= options.MaxFileSizeBytes)
                    .Select(f => BuildFileMetadata(f, rootPath))
                    .ToList();
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Access denied to some paths during indexation");
                return new List<FileMetadata>();
            }
        });

        // Count directories (separate traversal, fast)
        var directoryCount = await Task.Run(() =>
        {
            try
            {
                return Directory.EnumerateDirectories(rootPath, "*", SearchOption.AllDirectories)
                    .Count(d => !IsExcluded(d, options.ExcludePatterns));
            }
            catch (UnauthorizedAccessException)
            {
                return 0;
            }
        });

        // Calculate statistics
        var extensionCounts = files
            .GroupBy(f => f.Extension.ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.Count());

        var index = new StructuralIndex
        {
            RootPath = rootPath,
            Files = files,
            DirectoryCount = directoryCount,
            FileCount = files.Count,
            TotalSizeBytes = files.Sum(f => f.Size),
            IndexedAt = DateTime.UtcNow,
            Duration = stopwatch.Elapsed,
            ExtensionCounts = extensionCounts
        };

        stopwatch.Stop();

        _logger.LogInformation(
            "Indexed {FileCount} files and {DirectoryCount} directories in {Duration}ms",
            index.FileCount,
            index.DirectoryCount,
            stopwatch.ElapsedMilliseconds);

        return index;
    }

    public async Task<ProcessIndex> IndexRunningProcessesAsync()
    {
        _logger.LogInformation("Indexing running processes");

        var processes = await Task.Run(() =>
        {
            return Process.GetProcesses()
                .AsParallel()
                .Select(p =>
                {
                    try
                    {
                        return new ProcessMetadata
                        {
                            Pid = p.Id,
                            Name = p.ProcessName,
                            StartTime = p.StartTime,
                            CpuPercent = 0.0, // TODO: Implement CPU usage calculation
                            MemoryMB = p.WorkingSet64 / 1024 / 1024,
                            CommandLine = GetProcessCommandLine(p)
                        };
                    }
                    catch (Exception ex)
                    {
                        // Process may have exited, access denied, etc.
                        _logger.LogDebug(ex, "Failed to get metadata for process {ProcessId}", p.Id);
                        return null;
                    }
                })
                .Where(p => p != null)
                .Cast<ProcessMetadata>()
                .ToList();
        });

        return new ProcessIndex
        {
            Processes = processes,
            TotalProcesses = processes.Count,
            IndexedAt = DateTime.UtcNow
        };
    }

    public bool IsExcluded(string path, List<string> excludePatterns)
    {
        if (excludePatterns == null || excludePatterns.Count == 0)
        {
            return false;
        }

        // Normalize path separators for pattern matching
        var normalizedPath = path.Replace('\\', '/');

        foreach (var pattern in excludePatterns)
        {
            if (MatchesGlobPattern(normalizedPath, pattern))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Build file metadata without reading file content.
    /// </summary>
    private FileMetadata BuildFileMetadata(string filePath, string rootPath)
    {
        try
        {
            var fileInfo = new FileInfo(filePath);
            var extension = fileInfo.Extension.ToLowerInvariant();
            var relativePath = Path.GetRelativePath(rootPath, filePath).Replace('\\', '/');

            return new FileMetadata
            {
                Path = filePath,
                Name = fileInfo.Name,
                Extension = extension,
                Size = fileInfo.Length,
                Modified = fileInfo.LastWriteTimeUtc,
                MimeType = GetMimeType(extension),
                RelativePath = relativePath
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to build metadata for file: {FilePath}", filePath);
            throw;
        }
    }

    /// <summary>
    /// Get MIME type for file extension.
    /// </summary>
    private string? GetMimeType(string extension)
    {
        return MimeTypes.TryGetValue(extension, out var mimeType) ? mimeType : null;
    }

    /// <summary>
    /// Get command line for a process (platform-specific).
    /// </summary>
    private string? GetProcessCommandLine(Process process)
    {
        try
        {
            // This is Windows-specific; Linux/macOS requires different approach
            if (OperatingSystem.IsWindows())
            {
                // Use WMI query (expensive, disabled for now)
                return null;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Simple glob pattern matching.
    /// Supports: *, **, ?, and basic character classes.
    /// </summary>
    private bool MatchesGlobPattern(string path, string pattern)
    {
        // Normalize pattern
        var normalizedPattern = pattern.Replace('\\', '/');

        // Convert glob to regex
        var regexPattern = GlobToRegex(normalizedPattern);

        try
        {
            return Regex.IsMatch(path, regexPattern, RegexOptions.IgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Invalid glob pattern: {Pattern}", pattern);
            return false;
        }
    }

    /// <summary>
    /// Convert glob pattern to regex.
    /// </summary>
    private string GlobToRegex(string glob)
    {
        var regex = "^";
        var i = 0;

        while (i < glob.Length)
        {
            var c = glob[i];

            switch (c)
            {
                case '*':
                    // Check for ** (match any number of directories)
                    if (i + 1 < glob.Length && glob[i + 1] == '*')
                    {
                        regex += ".*";
                        i += 2;
                    }
                    else
                    {
                        // Single * matches anything except /
                        regex += "[^/]*";
                        i++;
                    }
                    break;

                case '?':
                    regex += "[^/]";
                    i++;
                    break;

                case '.':
                case '(':
                case ')':
                case '+':
                case '|':
                case '^':
                case '$':
                case '@':
                case '%':
                    // Escape regex special characters
                    regex += "\\" + c;
                    i++;
                    break;

                case '[':
                    // Character class - pass through
                    regex += c;
                    i++;
                    break;

                case ']':
                    // End character class
                    regex += c;
                    i++;
                    break;

                default:
                    regex += c;
                    i++;
                    break;
            }
        }

        regex += "$";
        return regex;
    }
}
