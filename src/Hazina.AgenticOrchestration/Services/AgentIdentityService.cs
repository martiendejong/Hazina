using Hazina.AgenticOrchestration.Abstractions;
using Hazina.AgenticOrchestration.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Hazina.AgenticOrchestration.Services;

/// <summary>
/// Service for managing persistent agent identities (CV system)
/// Inspired by Overstory's agent identity tracking
/// </summary>
public interface IAgentIdentityService
{
    Task<AgentIdentity> CreateIdentityAsync(AgentIdentity identity);
    Task<AgentIdentity?> LoadIdentityAsync(string agentName);
    Task UpdateIdentityAsync(AgentIdentity identity);
    Task RecordTaskCompletionAsync(string agentName, AgentTaskRecord task);
}

public class AgentIdentityService : IAgentIdentityService
{
    private readonly string _identitiesPath;
    private readonly ILogger<AgentIdentityService> _logger;
    private readonly IFileSystem _fileSystem;
    private readonly ISystemClock _clock;

    public AgentIdentityService(
        string identitiesPath,
        ILogger<AgentIdentityService> logger,
        IFileSystem fileSystem,
        ISystemClock clock)
    {
        _identitiesPath = identitiesPath;
        _logger = logger;
        _fileSystem = fileSystem;
        _clock = clock;

        // Ensure identities directory exists
        _fileSystem.CreateDirectory(_identitiesPath);
    }

    public async Task<AgentIdentity> CreateIdentityAsync(AgentIdentity identity)
    {
        var filePath = GetIdentityPath(identity.Name);

        if (_fileSystem.FileExists(filePath))
        {
            throw new InvalidOperationException($"Agent identity {identity.Name} already exists");
        }

        await SaveIdentityAsync(filePath, identity);
        _logger.LogInformation("Created agent identity: {AgentName} ({Capability})", identity.Name, identity.Capability);

        return identity;
    }

    public async Task<AgentIdentity?> LoadIdentityAsync(string agentName)
    {
        var filePath = GetIdentityPath(agentName);

        if (!_fileSystem.FileExists(filePath))
        {
            return null;
        }

        try
        {
            var json = await _fileSystem.ReadAllTextAsync(filePath);
            var identity = JsonSerializer.Deserialize<AgentIdentity>(json);
            _logger.LogInformation("Loaded agent identity: {AgentName} ({Sessions} sessions)", agentName, identity?.SessionsCompleted ?? 0);
            return identity;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load agent identity: {AgentName}", agentName);
            return null;
        }
    }

    public async Task UpdateIdentityAsync(AgentIdentity identity)
    {
        var filePath = GetIdentityPath(identity.Name);
        await SaveIdentityAsync(filePath, identity);
        _logger.LogInformation("Updated agent identity: {AgentName}", identity.Name);
    }

    public async Task RecordTaskCompletionAsync(string agentName, AgentTaskRecord task)
    {
        var identity = await LoadIdentityAsync(agentName);

        if (identity == null)
        {
            _logger.LogWarning("Cannot record task completion - agent identity not found: {AgentName}", agentName);
            return;
        }

        identity.SessionsCompleted++;
        identity.LastSession = _clock.UtcNow;

        // Add to recent tasks (keep last 10)
        identity.RecentTasks.Insert(0, task);
        if (identity.RecentTasks.Count > 10)
        {
            identity.RecentTasks = identity.RecentTasks.Take(10).ToList();
        }

        await UpdateIdentityAsync(identity);
        _logger.LogInformation("Recorded task completion for {AgentName}: {TaskId}", agentName, task.TaskId);
    }

    private string GetIdentityPath(string agentName)
    {
        return Path.Combine(_identitiesPath, $"{agentName}.json");
    }

    private async Task SaveIdentityAsync(string filePath, AgentIdentity identity)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var json = JsonSerializer.Serialize(identity, options);
        await _fileSystem.WriteAllTextAsync(filePath, json);
    }
}
