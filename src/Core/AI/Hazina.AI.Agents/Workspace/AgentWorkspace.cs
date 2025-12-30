namespace Hazina.AI.Agents.Workspace;

/// <summary>
/// Deterministic workspace for agent runs with isolated file storage.
/// Each agent run gets its own directory with predictable paths.
/// </summary>
public class AgentWorkspace
{
    private readonly string _workspaceRoot;
    private readonly string _runId;
    private readonly string _runDirectory;

    public string RunId => _runId;
    public string RunDirectory => _runDirectory;

    /// <summary>
    /// Create workspace for a specific agent run
    /// </summary>
    /// <param name="workspaceRoot">Base directory for all workspaces</param>
    /// <param name="runId">Unique run identifier</param>
    public AgentWorkspace(string workspaceRoot, string runId)
    {
        _workspaceRoot = workspaceRoot ?? throw new ArgumentNullException(nameof(workspaceRoot));
        _runId = runId ?? throw new ArgumentNullException(nameof(runId));

        _runDirectory = Path.Combine(_workspaceRoot, _runId);
    }

    /// <summary>
    /// Initialize the workspace (create directories)
    /// </summary>
    public void Initialize()
    {
        if (!Directory.Exists(_runDirectory))
        {
            Directory.CreateDirectory(_runDirectory);
        }

        Directory.CreateDirectory(Path.Combine(_runDirectory, "files"));
        Directory.CreateDirectory(Path.Combine(_runDirectory, "traces"));
        Directory.CreateDirectory(Path.Combine(_runDirectory, "plans"));
        Directory.CreateDirectory(Path.Combine(_runDirectory, "outputs"));
    }

    /// <summary>
    /// Write a file to the workspace
    /// </summary>
    public async Task WriteFileAsync(string relativePath, string content)
    {
        var fullPath = GetFilePath(relativePath);
        var directory = Path.GetDirectoryName(fullPath);

        if (directory != null && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(fullPath, content);
    }

    /// <summary>
    /// Read a file from the workspace
    /// </summary>
    public async Task<string> ReadFileAsync(string relativePath)
    {
        var fullPath = GetFilePath(relativePath);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"File not found: {relativePath}");
        }

        return await File.ReadAllTextAsync(fullPath);
    }

    /// <summary>
    /// Check if a file exists in the workspace
    /// </summary>
    public bool FileExists(string relativePath)
    {
        var fullPath = GetFilePath(relativePath);
        return File.Exists(fullPath);
    }

    /// <summary>
    /// List all files in the workspace
    /// </summary>
    public List<string> ListFiles(string? subdirectory = null)
    {
        var searchPath = subdirectory != null
            ? Path.Combine(_runDirectory, subdirectory)
            : _runDirectory;

        if (!Directory.Exists(searchPath))
        {
            return new List<string>();
        }

        var files = Directory.GetFiles(searchPath, "*", SearchOption.AllDirectories);

        return files.Select(f => Path.GetRelativePath(_runDirectory, f)).ToList();
    }

    /// <summary>
    /// Delete a file from the workspace
    /// </summary>
    public void DeleteFile(string relativePath)
    {
        var fullPath = GetFilePath(relativePath);

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
    }

    /// <summary>
    /// Get full path for a relative file path
    /// </summary>
    public string GetFilePath(string relativePath)
    {
        return Path.Combine(_runDirectory, "files", relativePath);
    }

    /// <summary>
    /// Get path for trace file
    /// </summary>
    public string GetTracePath(string traceId) =>
        Path.Combine(_runDirectory, "traces", $"{traceId}.json");

    /// <summary>
    /// Get path for plan file
    /// </summary>
    public string GetPlanPath(string planId) =>
        Path.Combine(_runDirectory, "plans", $"{planId}.json");

    /// <summary>
    /// Get path for output file
    /// </summary>
    public string GetOutputPath(string filename) =>
        Path.Combine(_runDirectory, "outputs", filename);

    /// <summary>
    /// Clean up workspace (delete all files)
    /// </summary>
    public void Cleanup()
    {
        if (Directory.Exists(_runDirectory))
        {
            Directory.Delete(_runDirectory, recursive: true);
        }
    }

    /// <summary>
    /// Get workspace size in bytes
    /// </summary>
    public long GetSize()
    {
        if (!Directory.Exists(_runDirectory))
            return 0;

        var files = Directory.GetFiles(_runDirectory, "*", SearchOption.AllDirectories);
        return files.Sum(f => new FileInfo(f).Length);
    }
}

/// <summary>
/// Factory for creating agent workspaces
/// </summary>
public class AgentWorkspaceFactory
{
    private readonly string _workspaceRoot;

    public AgentWorkspaceFactory(string workspaceRoot)
    {
        _workspaceRoot = workspaceRoot ?? throw new ArgumentNullException(nameof(workspaceRoot));

        if (!Directory.Exists(_workspaceRoot))
        {
            Directory.CreateDirectory(_workspaceRoot);
        }
    }

    /// <summary>
    /// Create a new workspace with a generated run ID
    /// </summary>
    public AgentWorkspace CreateWorkspace(string? prefix = null)
    {
        var runId = prefix != null
            ? $"{prefix}_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N").Substring(0, 8)}"
            : $"run_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N").Substring(0, 8)}";

        var workspace = new AgentWorkspace(_workspaceRoot, runId);
        workspace.Initialize();

        return workspace;
    }

    /// <summary>
    /// Get an existing workspace by run ID
    /// </summary>
    public AgentWorkspace GetWorkspace(string runId)
    {
        return new AgentWorkspace(_workspaceRoot, runId);
    }

    /// <summary>
    /// List all workspaces
    /// </summary>
    public List<string> ListWorkspaces()
    {
        if (!Directory.Exists(_workspaceRoot))
            return new List<string>();

        return Directory.GetDirectories(_workspaceRoot)
            .Select(Path.GetFileName)
            .Where(name => name != null)
            .Cast<string>()
            .ToList();
    }

    /// <summary>
    /// Clean up old workspaces (older than specified days)
    /// </summary>
    public int CleanupOldWorkspaces(int olderThanDays)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-olderThanDays);
        var deleted = 0;

        if (!Directory.Exists(_workspaceRoot))
            return deleted;

        foreach (var dir in Directory.GetDirectories(_workspaceRoot))
        {
            var dirInfo = new DirectoryInfo(dir);
            if (dirInfo.CreationTimeUtc < cutoffDate)
            {
                Directory.Delete(dir, recursive: true);
                deleted++;
            }
        }

        return deleted;
    }
}
