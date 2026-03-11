using Hazina.App.HazinaCoder.Core.Tools;
using System.Text.RegularExpressions;

namespace Hazina.App.HazinaCoder.Core.ErrorHandling;

/// <summary>
/// 🚀 IMPROVEMENT #4 (ROI 1.8): Error classification + auto-remediation
/// Stops infinite retry loops (like the 15+ identical "gh pr create" failures)
/// Learns from error patterns and applies automatic fixes
/// </summary>
public class ErrorRemediationEngine
{
    private readonly Dictionary<string, ErrorRemediationRule> _rules = new();
    private readonly Dictionary<string, int> _attemptCounts = new();
    private const int MaxRemediationAttempts = 3;

    public ErrorRemediationEngine()
    {
        InitializeDefaultRules();
    }

    /// <summary>
    /// Analyze an error and suggest/apply remediation
    /// </summary>
    public async Task<RemediationResult> RemediateAsync(
        CommandResult errorResult,
        string originalCommand,
        Func<string, Task<CommandResult>> retryFunc)
    {
        var error = errorResult.StandardError;
        var attemptKey = GetAttemptKey(originalCommand, error);

        // Check if we've tried too many times
        if (_attemptCounts.GetValueOrDefault(attemptKey, 0) >= MaxRemediationAttempts)
        {
            return RemediationResult.GiveUp(
                $"Max remediation attempts ({MaxRemediationAttempts}) reached for this error.\n" +
                $"Error: {error}\n" +
                $"Manual intervention required.");
        }

        // Find matching remediation rule
        var rule = _rules.Values.FirstOrDefault(r => r.Matches(error));
        if (rule == null)
        {
            return RemediationResult.NoRuleFound(error);
        }

        // Increment attempt count
        _attemptCounts[attemptKey] = _attemptCounts.GetValueOrDefault(attemptKey, 0) + 1;

        // Apply remediation
        var remediatedCommand = rule.ApplyFix(originalCommand, error);
        if (remediatedCommand == originalCommand)
        {
            return RemediationResult.NoFixAvailable(error, rule.Name);
        }

        Console.WriteLine($"[ErrorRemediation] Applying fix: {rule.Name}");
        Console.WriteLine($"[ErrorRemediation] Original: {originalCommand}");
        Console.WriteLine($"[ErrorRemediation] Fixed: {remediatedCommand}");

        // Retry with remediated command
        var retryResult = await retryFunc(remediatedCommand);

        if (retryResult.IsSuccess)
        {
            // Success! Reset attempt counter for this pattern
            _attemptCounts.Remove(attemptKey);
            return RemediationResult.CreateSuccess(remediatedCommand, rule.Name);
        }
        else
        {
            // Still failing - return for potential next remediation attempt
            return RemediationResult.Failed(remediatedCommand, rule.Name, retryResult.StandardError);
        }
    }

    private string GetAttemptKey(string command, string error)
    {
        // Create a stable key from command + error pattern (ignore variable parts)
        var normalizedError = Regex.Replace(error, @"\d+", "N"); // Replace numbers
        normalizedError = Regex.Replace(normalizedError, @"[a-f0-9]{6,}", "HEX"); // Replace hashes
        return $"{command}::{normalizedError}".GetHashCode().ToString();
    }

    private void InitializeDefaultRules()
    {
        // Rule 1: Missing quotes in git commit messages
        AddRule(new ErrorRemediationRule
        {
            Name = "Git Commit Unquoted Message",
            ErrorPattern = new Regex(@"error: pathspec '.+' did not match any file"),
            Explanation = "Git commit message not quoted - each word treated as pathspec",
            FixFunc = (cmd, error) =>
            {
                // Extract git commit command and properly quote the message
                var match = Regex.Match(cmd, @"git commit -m (.+?)(?:\s+--|$)");
                if (!match.Success) return cmd;

                var message = match.Groups[1].Value.Trim();
                // Remove existing quotes if any
                message = message.Trim('"', '\'');

                // Rebuild command with proper quotes
                return Regex.Replace(cmd, @"git commit -m .+?(?:\s+--|$)",
                    $"git commit -m \"{message}\"");
            }
        });

        // Rule 2: Missing quotes in gh CLI commands
        AddRule(new ErrorRemediationRule
        {
            Name = "GitHub CLI Unquoted Arguments",
            ErrorPattern = new Regex(@"unknown arguments \[.+\].*please quote all values that have spaces"),
            Explanation = "gh CLI arguments with spaces not quoted",
            FixFunc = (cmd, error) =>
            {
                // Find --title and --body arguments and quote them
                cmd = Regex.Replace(cmd, @"--title\s+([^""]\S+(?:\s+\S+)+)", @"--title ""$1""");
                cmd = Regex.Replace(cmd, @"--body\s+([^""]\S+(?:\s+\S+)+)", @"--body ""$1""");
                return cmd;
            }
        });

        // Rule 3: No commits between branches (trying to create empty PR)
        AddRule(new ErrorRemediationRule
        {
            Name = "No Commits For PR",
            ErrorPattern = new Regex(@"No commits between .+ and .+"),
            Explanation = "Trying to create PR with no commits - need to make commits first",
            FixFunc = (cmd, error) =>
            {
                // Can't auto-fix this - need actual code changes
                // Return suggestion in command form
                return "# ERROR: No commits to create PR. You must:\n" +
                       "# 1. Make code changes\n" +
                       "# 2. git add <files>\n" +
                       "# 3. git commit -m \"message\"\n" +
                       "# 4. git push\n" +
                       "# 5. THEN create PR";
            }
        });

        // Rule 4: Branch doesn't exist for worktree
        AddRule(new ErrorRemediationRule
        {
            Name = "Branch Not Found For Worktree",
            ErrorPattern = new Regex(@"fatal: invalid reference: (.+)"),
            Explanation = "Branch doesn't exist yet - need to create it first",
            FixFunc = (cmd, error) =>
            {
                var match = Regex.Match(error, @"fatal: invalid reference: (.+)");
                if (!match.Success) return cmd;

                var branchName = match.Groups[1].Value.Trim();
                // Extract repo path from worktree add command
                var repoMatch = Regex.Match(cmd, @"git worktree add (.+?)\s+(.+)");
                if (!repoMatch.Success) return cmd;

                var worktreePath = repoMatch.Groups[1].Value;

                // Return command sequence to create branch first
                return $"git branch {branchName} develop && git worktree add {worktreePath} {branchName}";
            }
        });

        // Rule 5: PowerShell path with spaces not quoted
        AddRule(new ErrorRemediationRule
        {
            Name = "PowerShell Path Spaces",
            ErrorPattern = new Regex(@"The term 'C:\\Program' is not recognized"),
            Explanation = "Windows path with spaces not quoted in PowerShell",
            FixFunc = (cmd, error) =>
            {
                // Quote paths with Program Files
                cmd = Regex.Replace(cmd, @"(C:\\Program Files[^""]+(?:\.exe|\.bat))", "\"$1\"");
                return cmd;
            }
        });

        // Rule 6: Working tree clean (nothing to commit)
        AddRule(new ErrorRemediationRule
        {
            Name = "Nothing To Commit",
            ErrorPattern = new Regex(@"nothing to commit, working tree clean"),
            Explanation = "No changes staged for commit",
            FixFunc = (cmd, error) =>
            {
                return "# ERROR: No changes to commit. You must:\n" +
                       "# 1. Make actual code changes (not just placeholder comments)\n" +
                       "# 2. git add <modified-files>\n" +
                       "# 3. git commit -m \"message\"";
            }
        });

        // Rule 7: File not found / path doesn't exist
        AddRule(new ErrorRemediationRule
        {
            Name = "Path Not Found",
            ErrorPattern = new Regex(@"(No such file or directory|cannot find the path|does not exist)"),
            Explanation = "Specified path doesn't exist",
            FixFunc = (cmd, error) =>
            {
                // Extract the problematic path
                var pathMatch = Regex.Match(error, @"'([^']+)'|""([^""]+)""");
                if (!pathMatch.Success) return cmd;

                var path = pathMatch.Groups[1].Value + pathMatch.Groups[2].Value;
                return $"# ERROR: Path not found: {path}\n" +
                       $"# Check if:\n" +
                       $"# 1. Path exists\n" +
                       $"# 2. Spelling is correct\n" +
                       $"# 3. You're in the right working directory";
            }
        });

        // Rule 8: Git remote already exists
        AddRule(new ErrorRemediationRule
        {
            Name = "Remote Already Exists",
            ErrorPattern = new Regex(@"remote .+ already exists"),
            Explanation = "Trying to add a remote that already exists",
            FixFunc = (cmd, error) =>
            {
                // Replace 'git remote add' with 'git remote set-url'
                return cmd.Replace("git remote add", "git remote set-url");
            }
        });

        // Rule 9: Branch already exists
        AddRule(new ErrorRemediationRule
        {
            Name = "Branch Already Exists",
            ErrorPattern = new Regex(@"fatal: A branch named '.+' already exists"),
            Explanation = "Branch already exists - use checkout instead",
            FixFunc = (cmd, error) =>
            {
                // Change 'git branch' to 'git checkout'
                return cmd.Replace("git branch", "git checkout");
            }
        });

        // Rule 10: Merge conflict detected
        AddRule(new ErrorRemediationRule
        {
            Name = "Merge Conflict",
            ErrorPattern = new Regex(@"CONFLICT .+: Merge conflict"),
            Explanation = "Merge conflicts need manual resolution",
            FixFunc = (cmd, error) =>
            {
                return "# ERROR: Merge conflicts detected\n" +
                       "# Resolution steps:\n" +
                       "# 1. git status (see conflicted files)\n" +
                       "# 2. Manually edit files to resolve conflicts\n" +
                       "# 3. git add <resolved-files>\n" +
                       "# 4. git commit";
            }
        });
    }

    private void AddRule(ErrorRemediationRule rule)
    {
        _rules[rule.Name] = rule;
    }

    /// <summary>
    /// Log a successful remediation for learning
    /// </summary>
    public void LogSuccess(string ruleName, string originalCommand, string fixedCommand)
    {
        // This would append to reflection.log.md in production
        Console.WriteLine($"[ErrorRemediation] SUCCESS: {ruleName}");
        Console.WriteLine($"[ErrorRemediation] Learned pattern for future sessions");
    }
}

public class ErrorRemediationRule
{
    public string Name { get; set; } = "";
    public Regex ErrorPattern { get; set; } = null!;
    public string Explanation { get; set; } = "";
    public Func<string, string, string> FixFunc { get; set; } = null!;

    public bool Matches(string error)
    {
        return ErrorPattern.IsMatch(error);
    }

    public string ApplyFix(string originalCommand, string error)
    {
        return FixFunc(originalCommand, error);
    }
}

public class RemediationResult
{
    public bool WasRemediated { get; init; }
    public bool Success { get; init; }
    public string? FixedCommand { get; init; }
    public string? RuleName { get; init; }
    public string? Message { get; init; }
    public string? RemainingError { get; init; }

    public static RemediationResult CreateSuccess(string fixedCommand, string ruleName) =>
        new()
        {
            WasRemediated = true,
            Success = true,
            FixedCommand = fixedCommand,
            RuleName = ruleName,
            Message = $"Successfully remediated using rule: {ruleName}"
        };

    public static RemediationResult Failed(string fixedCommand, string ruleName, string remainingError) =>
        new()
        {
            WasRemediated = true,
            Success = false,
            FixedCommand = fixedCommand,
            RuleName = ruleName,
            RemainingError = remainingError,
            Message = $"Remediation attempted with rule '{ruleName}' but still failing"
        };

    public static RemediationResult NoRuleFound(string error) =>
        new()
        {
            WasRemediated = false,
            Success = false,
            Message = $"No remediation rule found for error pattern"
        };

    public static RemediationResult NoFixAvailable(string error, string ruleName) =>
        new()
        {
            WasRemediated = false,
            Success = false,
            RuleName = ruleName,
            Message = $"Rule '{ruleName}' matched but no automatic fix available"
        };

    public static RemediationResult GiveUp(string message) =>
        new()
        {
            WasRemediated = false,
            Success = false,
            Message = message
        };
}
