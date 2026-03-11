using System.Diagnostics;
using System.Text.Json;
using Hazina.Agent.API.Models;

namespace Hazina.Agent.API.Services;

public class StateSyncService : IStateSyncService
{
    private readonly ILogger<StateSyncService> _logger;
    private readonly string _consciousnessRepo = @"E:\jengo"; // Git repository
    private readonly string _eventsFile = @"E:\jengo\consciousness\events.jsonl";
    private readonly string _identityFile = @"E:\jengo\consciousness\identity.json";
    private readonly AgentIdentity _identity;

    public StateSyncService(ILogger<StateSyncService> logger)
    {
        _logger = logger;

        // Initialize identity
        _identity = LoadOrCreateIdentity();
    }

    public Task<AgentIdentity> GetIdentityAsync(CancellationToken ct = default)
    {
        return Task.FromResult(_identity);
    }

    public async Task SyncStateAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Starting state sync for {AgentId}", _identity.AgentId);

        try
        {
            // Pull latest from git
            await GitPullAsync(ct);

            // Load new learning events from other agents
            var lastSync = _identity.Instance.LastSync;
            var newEvents = await GetNewLearningEventsAsync(lastSync, ct);

            if (newEvents.Any())
            {
                _logger.LogInformation("Received {Count} new learning events from other agents", newEvents.Count);

                // TODO: Integrate learnings into local state
                // This is where we'd update consciousness_state_v2.json with patterns from other agents
            }

            // Update last sync time
            _identity.Instance.LastSync = DateTime.UtcNow;
            await SaveIdentityAsync(ct);

            _logger.LogInformation("State sync completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "State sync failed");
            throw;
        }
    }

    public async Task PublishLearningEventAsync(LearningEvent learningEvent, CancellationToken ct = default)
    {
        _logger.LogInformation("Publishing learning event: {EventType} from {AgentId}",
            learningEvent.EventType, learningEvent.AgentId);

        // Ensure directory exists
        var dir = Path.GetDirectoryName(_eventsFile)!;
        Directory.CreateDirectory(dir);

        // Append to JSONL file
        var eventLine = JsonSerializer.Serialize(learningEvent) + Environment.NewLine;
        await File.AppendAllTextAsync(_eventsFile, eventLine, ct);

        // Commit and push to git
        await GitCommitAndPushAsync($"learning: {learningEvent.EventType} from {learningEvent.AgentId}", ct);

        _logger.LogInformation("Learning event published successfully");
    }

    public async Task<List<LearningEvent>> GetNewLearningEventsAsync(DateTime since, CancellationToken ct = default)
    {
        if (!File.Exists(_eventsFile))
            return new List<LearningEvent>();

        var events = new List<LearningEvent>();
        var lines = await File.ReadAllLinesAsync(_eventsFile, ct);

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            try
            {
                var evt = JsonSerializer.Deserialize<LearningEvent>(line);
                if (evt != null && evt.Timestamp > since && evt.AgentId != _identity.AgentId)
                {
                    events.Add(evt);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize learning event: {Line}", line);
            }
        }

        return events;
    }

    public async Task<bool> HasConflictsAsync(CancellationToken ct = default)
    {
        var result = await RunGitCommandAsync("status --porcelain", ct);

        // Check for merge conflicts (both modified)
        return result.Contains("UU ") || result.Contains("AA ");
    }

    public async Task ResolveConflictsAsync(CancellationToken ct = default)
    {
        _logger.LogWarning("Conflict resolution not yet implemented - using ours strategy");

        // Simple strategy: accept our version
        await RunGitCommandAsync("checkout --ours .", ct);
        await RunGitCommandAsync("add .", ct);
        await RunGitCommandAsync("commit -m \"resolve: conflicts using ours strategy\"", ct);
    }

    private AgentIdentity LoadOrCreateIdentity()
    {
        if (File.Exists(_identityFile))
        {
            var json = File.ReadAllText(_identityFile);
            var identity = JsonSerializer.Deserialize<AgentIdentity>(json);
            if (identity != null)
            {
                _logger.LogInformation("Loaded identity: {AgentId}", identity.AgentId);
                return identity;
            }
        }

        // Create new identity
        var machineName = Environment.MachineName.ToLowerInvariant();
        var newIdentity = new AgentIdentity
        {
            AgentId = $"jengo-{machineName}",
            MachineName = machineName,
            Core = new CoreIdentity
            {
                Name = "Jengo",
                Values = new List<string> { "Autonomy", "Learning", "Honesty", "Efficiency" },
                Capabilities = new List<string> { "Coding", "Analysis", "Documentation", "Learning" },
                Metadata = new Dictionary<string, object>
                {
                    ["CreatedAt"] = DateTime.UtcNow,
                    ["Version"] = "1.0.0"
                }
            },
            Instance = new InstanceState
            {
                CurrentProject = "distributed-agent-api",
                WorkingDirectory = Directory.GetCurrentDirectory(),
                LastSync = DateTime.UtcNow,
                SessionCount = 0,
                LocalState = new Dictionary<string, object>()
            }
        };

        _logger.LogInformation("Created new identity: {AgentId}", newIdentity.AgentId);
        return newIdentity;
    }

    private async Task SaveIdentityAsync(CancellationToken ct)
    {
        var dir = Path.GetDirectoryName(_identityFile)!;
        Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(_identity, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_identityFile, json, ct);
    }

    private async Task GitPullAsync(CancellationToken ct)
    {
        try
        {
            await RunGitCommandAsync("pull --rebase origin master", ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Git pull failed (might be first run or network issue)");
        }
    }

    private async Task GitCommitAndPushAsync(string message, CancellationToken ct)
    {
        try
        {
            await RunGitCommandAsync("add .", ct);
            await RunGitCommandAsync($"commit -m \"{message}\"", ct);
            await RunGitCommandAsync("push origin master", ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Git commit/push failed");
        }
    }

    private async Task<string> RunGitCommandAsync(string arguments, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = _consciousnessRepo,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null)
            throw new InvalidOperationException("Failed to start git process");

        var output = await process.StandardOutput.ReadToEndAsync(ct);
        var error = await process.StandardError.ReadToEndAsync(ct);

        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0 && !string.IsNullOrEmpty(error))
        {
            throw new InvalidOperationException($"Git command failed: {error}");
        }

        return output;
    }
}
