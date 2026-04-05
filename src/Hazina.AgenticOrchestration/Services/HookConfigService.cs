using Hazina.AgenticOrchestration.Abstractions;
using Hazina.AgenticOrchestration.Extensions;
using Hazina.AgenticOrchestration.Validation;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hazina.AgenticOrchestration.Services;

/// <summary>
/// Service for generating Claude Code hook configurations
/// Inspired by Overstory's hooks-deployer system
/// </summary>
public interface IHookConfigService
{
    Task<string> GenerateHooksConfigAsync(string agentName, int debounceMs = 5000);
    Task InstallHooksAsync(string worktreePath, string agentName, int debounceMs = 5000);
    Task UninstallHooksAsync(string worktreePath);
    Task<bool> AreHooksInstalledAsync(string worktreePath);
}

public class HookConfigService : IHookConfigService
{
    private readonly ILogger<HookConfigService> _logger;
    private readonly IFileSystem _fileSystem;
    private readonly string _allowedWorktreeRoot;

    public HookConfigService(
        ILogger<HookConfigService> logger,
        IFileSystem fileSystem,
        AgenticOrchestrationOptions options)
    {
        _logger = logger;
        _fileSystem = fileSystem;
        _allowedWorktreeRoot = options.WorktreeRoot;
    }

    public Task<string> GenerateHooksConfigAsync(string agentName, int debounceMs = 5000)
    {
        // Validate agent name to prevent command injection
        if (!InputValidator.IsValidAgentName(agentName))
        {
            throw new ArgumentException($"Invalid agent name: {agentName}. Must match ^[a-zA-Z0-9_-]{{1,50}}$");
        }

        // Escape agent name for shell safety (defense in depth)
        var safeAgentName = InputValidator.EscapeShellArgument(agentName);

        var config = new
        {
            hooks = new
            {
                SessionStart = new[]
                {
                    new
                    {
                        command = $"hazina-orchestration prime --agent \"{safeAgentName}\""
                    }
                },
                UserPromptSubmit = new[]
                {
                    new
                    {
                        command = $"hazina-orchestration mail-check --agent \"{safeAgentName}\" --inject --debounce {debounceMs}",
                        outputMode = "prepend"
                    }
                }
            }
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var json = JsonSerializer.Serialize(config, options);
        return Task.FromResult(json);
    }

    public async Task InstallHooksAsync(string worktreePath, string agentName, int debounceMs = 5000)
    {
        // Validate path safety (prevent path traversal)
        if (!InputValidator.IsPathSafe(worktreePath, _allowedWorktreeRoot))
        {
            throw new ArgumentException($"Worktree path must be within {_allowedWorktreeRoot}");
        }

        var claudeDir = Path.Combine(worktreePath, ".claude");
        _fileSystem.CreateDirectory(claudeDir);

        var settingsPath = Path.Combine(claudeDir, "settings.local.json");

        JsonNode rootNode;

        // Load existing settings or create new
        if (_fileSystem.FileExists(settingsPath))
        {
            var existingJson = await _fileSystem.ReadAllTextAsync(settingsPath);
            rootNode = JsonNode.Parse(existingJson) ?? new JsonObject();
        }
        else
        {
            rootNode = new JsonObject();
        }

        // Generate hooks config
        var hooksJson = await GenerateHooksConfigAsync(agentName, debounceMs);
        var hooksNode = JsonNode.Parse(hooksJson);

        // Merge hooks (don't overwrite existing)
        if (rootNode is JsonObject rootObj)
        {
            if (hooksNode?["hooks"] is JsonObject newHooks)
            {
                if (rootObj["hooks"] is not JsonObject existingHooks)
                {
                    // No existing hooks - add new ones
                    rootObj["hooks"] = newHooks;
                }
                else
                {
                    // Merge hooks
                    foreach (var hookType in newHooks)
                    {
                        existingHooks[hookType.Key] = hookType.Value?.DeepClone();
                    }
                }
            }
        }

        // Write back
        var writeOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        var finalJson = rootNode.ToJsonString(writeOptions);
        await _fileSystem.WriteAllTextAsync(settingsPath, finalJson);

        _logger.LogInformation("Installed hooks for agent {AgentName} at {WorktreePath}", agentName, worktreePath);
    }

    public async Task UninstallHooksAsync(string worktreePath)
    {
        var settingsPath = Path.Combine(worktreePath, ".claude", "settings.local.json");

        if (!_fileSystem.FileExists(settingsPath))
        {
            return;
        }

        var json = await _fileSystem.ReadAllTextAsync(settingsPath);
        var rootNode = JsonNode.Parse(json);

        if (rootNode is JsonObject rootObj && rootObj.ContainsKey("hooks"))
        {
            rootObj.Remove("hooks");

            var options = new JsonSerializerOptions { WriteIndented = true };
            var finalJson = rootNode.ToJsonString(options);
            await _fileSystem.WriteAllTextAsync(settingsPath, finalJson);

            _logger.LogInformation("Uninstalled hooks from {WorktreePath}", worktreePath);
        }
    }

    public async Task<bool> AreHooksInstalledAsync(string worktreePath)
    {
        var settingsPath = Path.Combine(worktreePath, ".claude", "settings.local.json");

        if (!_fileSystem.FileExists(settingsPath))
        {
            return false;
        }

        try
        {
            var json = await _fileSystem.ReadAllTextAsync(settingsPath);
            var rootNode = JsonNode.Parse(json);

            if (rootNode is JsonObject rootObj && rootObj["hooks"] is JsonObject hooks)
            {
                return hooks.ContainsKey("SessionStart") && hooks.ContainsKey("UserPromptSubmit");
            }

            return false;
        }
        catch
        {
            return false;
        }
    }
}
