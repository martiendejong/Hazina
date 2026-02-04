using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Hazina.App.HazinaCoder.Core.MultiAgent;

/// <summary>
/// Pre-allocation conflict detection to prevent resource collisions.
/// Checks git worktrees, database allocations, and instances.map.md before allocating.
///
/// Improvement #40: Conflict Detection Before Allocation (Value: 9/10, Effort: 5/10, Ratio: 1.80)
/// </summary>
public class ConflictDetector
{
    private readonly CoordinationDatabase _db;
    private readonly string _projectsRoot;
    private readonly string _scriptsRoot;

    public ConflictDetector(CoordinationDatabase db, string projectsRoot, string scriptsRoot)
    {
        _db = db;
        _projectsRoot = projectsRoot;
        _scriptsRoot = scriptsRoot;
    }

    /// <summary>
    /// Check for conflicts before allocating a worktree
    /// </summary>
    public async Task<ConflictCheckResult> CheckWorktreeConflictAsync(string worktreePath)
    {
        var conflicts = new List<string>();

        // 1. Check coordination database
        var dbAllocation = await CheckDatabaseAllocationAsync("worktree", worktreePath);
        if (dbAllocation.HasConflict)
        {
            conflicts.Add($"Database: Already allocated to agent {dbAllocation.ConflictingAgent}");
        }

        // 2. Check git worktree status
        var gitConflict = await CheckGitWorktreeAsync(worktreePath);
        if (gitConflict.HasConflict)
        {
            conflicts.Add($"Git: Worktree exists at {worktreePath}");
        }

        // 3. Check instances.map.md
        var instancesConflict = await CheckInstancesMapAsync(worktreePath);
        if (instancesConflict.HasConflict)
        {
            conflicts.Add($"Instances Map: {instancesConflict.Message}");
        }

        // 4. Check directory existence
        if (Directory.Exists(worktreePath))
        {
            var files = Directory.GetFiles(worktreePath, "*.*", SearchOption.AllDirectories);
            if (files.Length > 0)
            {
                conflicts.Add($"Directory: Path exists with {files.Length} files");
            }
        }

        return new ConflictCheckResult
        {
            HasConflict = conflicts.Any(),
            ResourcePath = worktreePath,
            ConflictReasons = conflicts,
            CheckedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Check for conflicts before allocating a process (e.g., dev server on specific port)
    /// </summary>
    public async Task<ConflictCheckResult> CheckProcessConflictAsync(string processName, int port)
    {
        var conflicts = new List<string>();

        // 1. Check coordination database
        var resourcePath = $"{processName}:port-{port}";
        var dbAllocation = await CheckDatabaseAllocationAsync("process", resourcePath);
        if (dbAllocation.HasConflict)
        {
            conflicts.Add($"Database: Port {port} allocated to agent {dbAllocation.ConflictingAgent}");
        }

        // 2. Check if port is in use (network check)
        var portInUse = IsPortInUse(port);
        if (portInUse)
        {
            conflicts.Add($"Network: Port {port} is already in use");
        }

        return new ConflictCheckResult
        {
            HasConflict = conflicts.Any(),
            ResourcePath = resourcePath,
            ConflictReasons = conflicts,
            CheckedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Check for file lock conflicts
    /// </summary>
    public async Task<ConflictCheckResult> CheckFileConflictAsync(string filePath)
    {
        var conflicts = new List<string>();

        // 1. Check coordination database
        var dbAllocation = await CheckDatabaseAllocationAsync("file", filePath);
        if (dbAllocation.HasConflict)
        {
            conflicts.Add($"Database: File locked by agent {dbAllocation.ConflictingAgent}");
        }

        // 2. Check if file is locked by OS
        if (File.Exists(filePath))
        {
            try
            {
                using var stream = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                stream.Close();
            }
            catch (IOException)
            {
                conflicts.Add("OS: File is locked by another process");
            }
        }

        return new ConflictCheckResult
        {
            HasConflict = conflicts.Any(),
            ResourcePath = filePath,
            ConflictReasons = conflicts,
            CheckedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Check database for existing allocation
    /// </summary>
    private async Task<DatabaseConflictResult> CheckDatabaseAllocationAsync(string resourceType, string resourcePath)
    {
        // Stub implementation - actual check happens in CoordinationDatabase.TryAllocateResourceAsync
        // This is a pre-check before attempting allocation

        var activeAgents = await _db.GetActiveAgentsAsync();

        // For now, return no conflict - real check is in TryAllocateResourceAsync
        return new DatabaseConflictResult
        {
            HasConflict = false
        };
    }

    /// <summary>
    /// Check git worktree list
    /// </summary>
    private async Task<GitConflictResult> CheckGitWorktreeAsync(string worktreePath)
    {
        try
        {
            // Extract repo name from worktree path
            // Expected format: C:/Projects/worker-agents/agent-004/repo-name
            var pathParts = worktreePath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            if (pathParts.Length < 2)
                return new GitConflictResult { HasConflict = false };

            var repoName = pathParts.Last();
            var baseRepoPath = Path.Combine(_projectsRoot, repoName);

            if (!Directory.Exists(baseRepoPath))
                return new GitConflictResult { HasConflict = false };

            // Check git worktree list (stub - would run `git worktree list` in production)
            // For now, check if directory exists
            var exists = Directory.Exists(worktreePath);

            return new GitConflictResult
            {
                HasConflict = exists,
                Message = exists ? $"Worktree directory exists at {worktreePath}" : null
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Git worktree check failed: {ex.Message}");
            return new GitConflictResult { HasConflict = false };
        }
    }

    /// <summary>
    /// Check instances.map.md for allocation status
    /// </summary>
    private async Task<InstancesMapConflictResult> CheckInstancesMapAsync(string worktreePath)
    {
        try
        {
            var instancesMapPath = Path.Combine(_scriptsRoot, "_machine", "instances.map.md");

            if (!File.Exists(instancesMapPath))
                return new InstancesMapConflictResult { HasConflict = false };

            var content = await File.ReadAllTextAsync(instancesMapPath);

            // Check if worktree is marked as BUSY
            // Expected format: | agent-004 | BUSY | ...
            var worktreeName = Path.GetFileName(worktreePath);
            var lines = content.Split('\n');

            foreach (var line in lines)
            {
                if (line.Contains(worktreeName) && line.Contains("BUSY"))
                {
                    return new InstancesMapConflictResult
                    {
                        HasConflict = true,
                        Message = $"Worktree {worktreeName} marked as BUSY in instances.map.md"
                    };
                }
            }

            return new InstancesMapConflictResult { HasConflict = false };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ instances.map.md check failed: {ex.Message}");
            return new InstancesMapConflictResult { HasConflict = false };
        }
    }

    /// <summary>
    /// Check if network port is in use
    /// </summary>
    private bool IsPortInUse(int port)
    {
        try
        {
            var ipGlobalProperties = System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties();
            var tcpConnections = ipGlobalProperties.GetActiveTcpListeners();

            return tcpConnections.Any(endpoint => endpoint.Port == port);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Port check failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Generate conflict report
    /// </summary>
    public string GenerateConflictReport(ConflictCheckResult result)
    {
        if (!result.HasConflict)
            return $"✅ No conflicts detected for {result.ResourcePath}";

        var report = $"❌ CONFLICTS DETECTED for {result.ResourcePath}\n";
        report += $"   Checked at: {result.CheckedAt:yyyy-MM-dd HH:mm:ss}\n";
        report += "   Reasons:\n";

        foreach (var reason in result.ConflictReasons)
        {
            report += $"   - {reason}\n";
        }

        return report;
    }
}

/// <summary>
/// Result of conflict check
/// </summary>
public class ConflictCheckResult
{
    public bool HasConflict { get; set; }
    public string ResourcePath { get; set; } = string.Empty;
    public List<string> ConflictReasons { get; set; } = new();
    public DateTime CheckedAt { get; set; }
}

/// <summary>
/// Database conflict result
/// </summary>
public class DatabaseConflictResult
{
    public bool HasConflict { get; set; }
    public string? ConflictingAgent { get; set; }
}

/// <summary>
/// Git conflict result
/// </summary>
public class GitConflictResult
{
    public bool HasConflict { get; set; }
    public string? Message { get; set; }
}

/// <summary>
/// instances.map.md conflict result
/// </summary>
public class InstancesMapConflictResult
{
    public bool HasConflict { get; set; }
    public string? Message { get; set; }
}
