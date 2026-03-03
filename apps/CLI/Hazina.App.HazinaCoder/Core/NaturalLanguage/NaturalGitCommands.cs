using System;
using System.Text.RegularExpressions;

namespace Hazina.App.HazinaCoder.Core.NaturalLanguage;

/// <summary>
/// Natural language interface for Git operations.
/// Translates conversational commands into Git actions.
/// </summary>
/// <remarks>
/// Improvement #37: Natural Language Git
///
/// Examples:
/// - "Commit this with a good message" → analyze changes, generate commit
/// - "Create PR to develop" → create pull request
/// - "Show me what changed" → git diff
/// - "Undo that commit" → git reset
/// </remarks>
public class NaturalGitCommands
{
    /// <summary>
    /// Parse natural language command into Git action
    /// </summary>
    public GitCommand? ParseCommand(string naturalLanguage)
    {
        var input = naturalLanguage.ToLower().Trim();

        // Commit patterns
        if (Regex.IsMatch(input, @"commit (this|changes|everything)"))
        {
            return new GitCommand
            {
                Action = GitAction.Commit,
                GenerateMessage = input.Contains("good message") || input.Contains("smart message"),
                Description = "Commit current changes"
            };
        }

        // PR patterns
        if (Regex.IsMatch(input, @"(create|make|open) (a )?pr"))
        {
            var targetBranch = ExtractBranch(input) ?? "develop";
            return new GitCommand
            {
                Action = GitAction.CreatePR,
                TargetBranch = targetBranch,
                Description = $"Create pull request to {targetBranch}"
            };
        }

        // Diff patterns
        if (Regex.IsMatch(input, @"(show|what|see) .* (changed|diff|modified)"))
        {
            return new GitCommand
            {
                Action = GitAction.ShowDiff,
                Description = "Show changes"
            };
        }

        // Status patterns
        if (Regex.IsMatch(input, @"(git )?status|what('?s| is) the state"))
        {
            return new GitCommand
            {
                Action = GitAction.Status,
                Description = "Show repository status"
            };
        }

        // Undo patterns
        if (Regex.IsMatch(input, @"undo (that |the )?(commit|change)"))
        {
            return new GitCommand
            {
                Action = GitAction.UndoCommit,
                Description = "Undo last commit (keeping changes)"
            };
        }

        // Branch patterns
        if (Regex.IsMatch(input, @"(create|make|new) (a )?branch"))
        {
            return new GitCommand
            {
                Action = GitAction.CreateBranch,
                Description = "Create new branch"
            };
        }

        // Merge patterns
        if (Regex.IsMatch(input, @"merge .* (into|to)"))
        {
            return new GitCommand
            {
                Action = GitAction.Merge,
                Description = "Merge branches"
            };
        }

        // No match found
        return null;
    }

    /// <summary>
    /// Extract branch name from natural language
    /// </summary>
    private string? ExtractBranch(string input)
    {
        var match = Regex.Match(input, @"(to|into) ([a-z0-9\-_/]+)");
        return match.Success ? match.Groups[2].Value : null;
    }

    /// <summary>
    /// Generate a commit message from changes
    /// </summary>
    public string GenerateCommitMessage(string diffOutput)
    {
        // TODO: Use LLM to generate intelligent commit message from diff
        // For now, placeholder
        return "chore: Update files\n\n- Made changes to codebase\n\nCo-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>";
    }
}

/// <summary>
/// Git command parsed from natural language
/// </summary>
public class GitCommand
{
    public GitAction Action { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? TargetBranch { get; set; }
    public bool GenerateMessage { get; set; }
}

/// <summary>
/// Git actions
/// </summary>
public enum GitAction
{
    Commit,
    CreatePR,
    ShowDiff,
    Status,
    UndoCommit,
    CreateBranch,
    Merge,
    Push,
    Pull,
    Stash
}
