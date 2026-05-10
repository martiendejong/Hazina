using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Hazina.AgenticOrchestration.Services.Monitoring;

/// <summary>
/// Monitors GitHub for events that need autonomous action:
/// - New pull requests
/// - PR comments requesting changes
/// - New issues
/// - Issue comments mentioning @jengo
/// </summary>
public class GitHubEventMonitor
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _token;
    private readonly string _owner;
    private readonly string _repo;
    private DateTime _lastCheck = DateTime.UtcNow;

    public GitHubEventMonitor(IHttpClientFactory httpClientFactory, string token, string owner, string repo)
    {
        _httpClientFactory = httpClientFactory;
        _token = token;
        _owner = owner;
        _repo = repo;
    }

    private HttpClient CreateClient()
    {
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Jengo", "1.0"));
        return client;
    }

    /// <summary>
    /// Poll GitHub for actionable events
    /// </summary>
    public async Task<List<GitHubEvent>> PollForNewEvents(CancellationToken cancellationToken)
    {
        var events = new List<GitHubEvent>();
        using var httpClient = CreateClient();

        // Check for new pull requests
        var newPRs = await GetNewPullRequests(httpClient, cancellationToken);
        events.AddRange(newPRs);

        // Check for PR review comments
        var prComments = await GetPRComments(httpClient, cancellationToken);
        events.AddRange(prComments);

        // Check for new issues
        var newIssues = await GetNewIssues(httpClient, cancellationToken);
        events.AddRange(newIssues);

        _lastCheck = DateTime.UtcNow;
        return events;
    }

    private async Task<List<GitHubEvent>> GetNewPullRequests(HttpClient httpClient, CancellationToken cancellationToken)
    {
        try
        {
            var url = $"https://api.github.com/repos/{_owner}/{_repo}/pulls?state=open&sort=updated&direction=desc";
            var prs = await httpClient.GetFromJsonAsync<List<GitHubPullRequest>>(url, cancellationToken);

            return prs?
                .Where(pr => pr.UpdatedAt > _lastCheck)
                .Select(pr => new GitHubEvent
                {
                    Type = GitHubEventType.NewPullRequest,
                    Id = pr.Number.ToString(),
                    Title = pr.Title,
                    Url = pr.HtmlUrl,
                    CreatedAt = pr.CreatedAt,
                    Priority = 2 // High priority
                })
                .ToList() ?? new List<GitHubEvent>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching PRs: {ex.Message}");
            return new List<GitHubEvent>();
        }
    }

    private async Task<List<GitHubEvent>> GetPRComments(HttpClient httpClient, CancellationToken cancellationToken)
    {
        try
        {
            // Get recent PR review comments across all PRs
            var url = $"https://api.github.com/repos/{_owner}/{_repo}/pulls/comments?sort=updated&direction=desc&per_page=50";
            var comments = await httpClient.GetFromJsonAsync<List<GitHubComment>>(url, cancellationToken);

            return comments?
                .Where(c => c.UpdatedAt > _lastCheck)
                .Where(c => c.Body.Contains("request changes", StringComparison.OrdinalIgnoreCase) ||
                           c.Body.Contains("please fix", StringComparison.OrdinalIgnoreCase))
                .Select(c => new GitHubEvent
                {
                    Type = GitHubEventType.PRComment,
                    Id = c.Id.ToString(),
                    Title = $"Comment on PR: {c.Body.Substring(0, Math.Min(50, c.Body.Length))}...",
                    Url = c.HtmlUrl,
                    CreatedAt = c.CreatedAt,
                    Priority = 1 // Urgent - blocking PR
                })
                .ToList() ?? new List<GitHubEvent>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching PR comments: {ex.Message}");
            return new List<GitHubEvent>();
        }
    }

    private async Task<List<GitHubEvent>> GetNewIssues(HttpClient httpClient, CancellationToken cancellationToken)
    {
        try
        {
            var url = $"https://api.github.com/repos/{_owner}/{_repo}/issues?state=open&sort=updated&direction=desc&per_page=50";
            var issues = await httpClient.GetFromJsonAsync<List<GitHubIssue>>(url, cancellationToken);

            return issues?
                .Where(i => i.UpdatedAt > _lastCheck)
                .Where(i => !i.PullRequest.HasValue) // Exclude PRs (they appear as issues too)
                .Select(i => new GitHubEvent
                {
                    Type = GitHubEventType.NewIssue,
                    Id = i.Number.ToString(),
                    Title = i.Title,
                    Url = i.HtmlUrl,
                    CreatedAt = i.CreatedAt,
                    Priority = 3 // Normal priority
                })
                .ToList() ?? new List<GitHubEvent>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching issues: {ex.Message}");
            return new List<GitHubEvent>();
        }
    }
}

public class GitHubEvent
{
    public GitHubEventType Type { get; set; }
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int Priority { get; set; } // 1=urgent, 2=high, 3=normal
}

public enum GitHubEventType
{
    NewPullRequest,
    PRComment,
    NewIssue
}

public class GitHubPullRequest
{
    public int Number { get; set; }
    public string Title { get; set; } = string.Empty;
    public string HtmlUrl { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class GitHubComment
{
    public int Id { get; set; }
    public string Body { get; set; } = string.Empty;
    public string HtmlUrl { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class GitHubIssue
{
    public int Number { get; set; }
    public string Title { get; set; } = string.Empty;
    public string HtmlUrl { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public object? PullRequest { get; set; } // null if it's an issue, non-null if it's a PR
}
