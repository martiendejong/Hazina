using Hazina.App.HazinaCoder.Core.Tools;

namespace Hazina.App.HazinaCoder.Core.Git;

/// <summary>
/// 🚀 IMPROVEMENT #2 (ROI 2.5): Git domain abstraction
/// Fixes worktree allocation disasters like "agent-feature-869c1w3d4" mistake
/// Provides type-safe Git operations with automatic path validation
/// </summary>
public class GitClient
{
    private readonly CommandExecutor _executor;
    private readonly string _repositoryPath;

    public GitClient(CommandExecutor executor, string repositoryPath)
    {
        _executor = executor;
        _repositoryPath = Path.GetFullPath(repositoryPath);

        if (!Directory.Exists(_repositoryPath))
            throw new ArgumentException($"Repository path does not exist: {_repositoryPath}");

        if (!Directory.Exists(Path.Combine(_repositoryPath, ".git")))
            throw new ArgumentException($"Not a git repository: {_repositoryPath}");
    }

    /// <summary>
    /// Worktree operations - prevents common mistakes
    /// </summary>
    public GitWorktreeOps Worktree => new(this);

    /// <summary>
    /// Branch operations
    /// </summary>
    public GitBranchOps Branch => new(this);

    /// <summary>
    /// Commit operations
    /// </summary>
    public GitCommitOps Commit => new(this);

    /// <summary>
    /// Remote operations
    /// </summary>
    public GitRemoteOps Remote => new(this);

    internal async Task<CommandResult> ExecuteGitAsync(
        string[] arguments,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default)
    {
        var args = new List<string> { "-C", workingDirectory ?? _repositoryPath };
        args.AddRange(arguments);

        return await _executor.ExecuteAsync("git", args, _repositoryPath, cancellationToken);
    }
}

/// <summary>
/// Worktree operations with validation
/// </summary>
public class GitWorktreeOps
{
    private readonly GitClient _git;

    public GitWorktreeOps(GitClient git) => _git = git;

    /// <summary>
    /// Add a new worktree with proper directory structure validation
    /// Prevents mistakes like "agent-feature-869c1w3d4" (wrong location)
    /// </summary>
    public async Task<WorktreeAddResult> AddAsync(
        string worktreePath,
        string branchName,
        bool createBranch = true,
        CancellationToken cancellationToken = default)
    {
        // Validate worktree path structure (must be in worker-agents directory)
        var fullPath = Path.GetFullPath(worktreePath);
        if (!fullPath.Contains("worker-agents"))
        {
            return WorktreeAddResult.Failed($"Invalid worktree path: must be under worker-agents/ directory\nGot: {fullPath}");
        }

        // Extract agent seat from path (e.g., agent-001)
        var pathParts = fullPath.Split(Path.DirectorySeparatorChar);
        var agentSeat = pathParts.FirstOrDefault(p => p.StartsWith("agent-"));
        if (agentSeat == null)
        {
            return WorktreeAddResult.Failed($"Invalid worktree path: missing agent seat (agent-XXX)\nGot: {fullPath}");
        }

        // Verify agent seat directory structure
        // Expected: C:\Projects\worker-agents\agent-XXX\<repo-name>
        var repoName = Path.GetFileName(worktreePath);
        var expectedPath = Path.Combine(Path.GetDirectoryName(fullPath)!, agentSeat, repoName);
        if (fullPath != expectedPath)
        {
            return WorktreeAddResult.Failed(
                $"Invalid worktree path structure\n" +
                $"Expected: ...\\worker-agents\\{agentSeat}\\{repoName}\n" +
                $"Got: {fullPath}");
        }

        // Create worktree
        var args = new List<string> { "worktree", "add", fullPath };
        if (createBranch)
        {
            args.Add("-b");
            args.Add(branchName);
        }
        else
        {
            args.Add(branchName);
        }

        var result = await _git.ExecuteGitAsync(args.ToArray(), cancellationToken: cancellationToken);

        if (!result.IsSuccess)
        {
            return WorktreeAddResult.Failed($"Git worktree add failed:\n{result.StandardError}");
        }

        return WorktreeAddResult.CreateSuccess(fullPath, branchName);
    }

    /// <summary>
    /// List all worktrees
    /// </summary>
    public async Task<List<WorktreeInfo>> ListAsync(CancellationToken cancellationToken = default)
    {
        var result = await _git.ExecuteGitAsync(new[] { "worktree", "list", "--porcelain" }, cancellationToken: cancellationToken);

        if (!result.IsSuccess)
            return new List<WorktreeInfo>();

        // Parse porcelain format
        var worktrees = new List<WorktreeInfo>();
        var lines = result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        WorktreeInfo? current = null;
        foreach (var line in lines)
        {
            if (line.StartsWith("worktree "))
            {
                if (current != null)
                    worktrees.Add(current);

                current = new WorktreeInfo { Path = line.Substring("worktree ".Length).Trim() };
            }
            else if (line.StartsWith("HEAD ") && current != null)
            {
                current.Head = line.Substring("HEAD ".Length).Trim();
            }
            else if (line.StartsWith("branch ") && current != null)
            {
                current.Branch = line.Substring("branch ".Length).Replace("refs/heads/", "").Trim();
            }
        }

        if (current != null)
            worktrees.Add(current);

        return worktrees;
    }

    /// <summary>
    /// Remove a worktree
    /// </summary>
    public async Task<CommandResult> RemoveAsync(string worktreePath, bool force = false, CancellationToken cancellationToken = default)
    {
        var args = new List<string> { "worktree", "remove", worktreePath };
        if (force)
            args.Add("--force");

        return await _git.ExecuteGitAsync(args.ToArray(), cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Prune stale worktrees
    /// </summary>
    public async Task<CommandResult> PruneAsync(CancellationToken cancellationToken = default)
    {
        return await _git.ExecuteGitAsync(new[] { "worktree", "prune" }, cancellationToken: cancellationToken);
    }
}

public class WorktreeAddResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public string? WorktreePath { get; init; }
    public string? BranchName { get; init; }

    public static WorktreeAddResult CreateSuccess(string path, string branch) =>
        new() { Success = true, WorktreePath = path, BranchName = branch };

    public static WorktreeAddResult Failed(string error) =>
        new() { Success = false, Error = error };
}

public class WorktreeInfo
{
    public string Path { get; set; } = "";
    public string? Head { get; set; }
    public string? Branch { get; set; }
}

/// <summary>
/// Branch operations
/// </summary>
public class GitBranchOps
{
    private readonly GitClient _git;

    public GitBranchOps(GitClient git) => _git = git;

    public async Task<CommandResult> CreateAsync(string branchName, string? startPoint = null, CancellationToken cancellationToken = default)
    {
        var args = new List<string> { "branch", branchName };
        if (startPoint != null)
            args.Add(startPoint);

        return await _git.ExecuteGitAsync(args.ToArray(), cancellationToken: cancellationToken);
    }

    public async Task<CommandResult> CheckoutAsync(string branchName, bool createIfNotExists = false, CancellationToken cancellationToken = default)
    {
        var args = new List<string> { "checkout" };
        if (createIfNotExists)
            args.Add("-b");
        args.Add(branchName);

        return await _git.ExecuteGitAsync(args.ToArray(), cancellationToken: cancellationToken);
    }

    public async Task<string?> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        var result = await _git.ExecuteGitAsync(new[] { "branch", "--show-current" }, cancellationToken: cancellationToken);
        return result.IsSuccess ? result.StandardOutput.Trim() : null;
    }

    public async Task<List<string>> ListAsync(bool remote = false, CancellationToken cancellationToken = default)
    {
        var args = remote ? new[] { "branch", "-r" } : new[] { "branch" };
        var result = await _git.ExecuteGitAsync(args, cancellationToken: cancellationToken);

        if (!result.IsSuccess)
            return new List<string>();

        return result.StandardOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(b => b.Trim().TrimStart('*').Trim())
            .ToList();
    }
}

/// <summary>
/// Commit operations with automatic quoting
/// </summary>
public class GitCommitOps
{
    private readonly GitClient _git;

    public GitCommitOps(GitClient git) => _git = git;

    /// <summary>
    /// Commit with automatic message quoting (fixes the commit failure cascade)
    /// </summary>
    public async Task<CommandResult> CreateAsync(
        string message,
        bool all = false,
        IEnumerable<string>? files = null,
        CancellationToken cancellationToken = default)
    {
        var args = new List<string> { "commit", "-m", message };
        if (all)
            args.Add("-a");
        if (files != null)
            args.AddRange(files);

        return await _git.ExecuteGitAsync(args.ToArray(), cancellationToken: cancellationToken);
    }

    public async Task<CommandResult> AddAsync(IEnumerable<string> paths, CancellationToken cancellationToken = default)
    {
        var args = new List<string> { "add" };
        args.AddRange(paths);

        return await _git.ExecuteGitAsync(args.ToArray(), cancellationToken: cancellationToken);
    }

    public async Task<string> GetStatusAsync(bool shortFormat = false, CancellationToken cancellationToken = default)
    {
        var args = shortFormat ? new[] { "status", "--short" } : new[] { "status" };
        var result = await _git.ExecuteGitAsync(args, cancellationToken: cancellationToken);
        return result.StandardOutput;
    }
}

/// <summary>
/// Remote operations with automatic quoting
/// </summary>
public class GitRemoteOps
{
    private readonly GitClient _git;

    public GitRemoteOps(GitClient git) => _git = git;

    public async Task<CommandResult> PushAsync(
        string? remote = null,
        string? branch = null,
        bool setUpstream = false,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        var args = new List<string> { "push" };
        if (setUpstream)
            args.Add("-u");
        if (force)
            args.Add("--force");
        if (remote != null)
            args.Add(remote);
        if (branch != null)
            args.Add(branch);

        return await _git.ExecuteGitAsync(args.ToArray(), cancellationToken: cancellationToken);
    }

    public async Task<CommandResult> PullAsync(string? remote = null, string? branch = null, CancellationToken cancellationToken = default)
    {
        var args = new List<string> { "pull" };
        if (remote != null)
            args.Add(remote);
        if (branch != null)
            args.Add(branch);

        return await _git.ExecuteGitAsync(args.ToArray(), cancellationToken: cancellationToken);
    }

    public async Task<List<string>> ListAsync(CancellationToken cancellationToken = default)
    {
        var result = await _git.ExecuteGitAsync(new[] { "remote" }, cancellationToken: cancellationToken);

        if (!result.IsSuccess)
            return new List<string>();

        return result.StandardOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(r => r.Trim())
            .ToList();
    }
}
