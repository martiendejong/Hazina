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

    public HookConfigService(ILogger<HookConfigService> logger)
    {
        _logger = logger;
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
        const string allowedRoot = @"C:\Projects\worker-agents";
        if (!InputValidator.IsPathSafe(worktreePath, allowedRoot))
        {
            throw new ArgumentException($"Worktree path must be within {allowedRoot}");
        }

        var claudeDir = Path.Combine(worktreePath, ".claude");
        Directory.CreateDirectory(claudeDir);

        var settingsPath = Path.Combine(claudeDir, "settings.local.json");

        JsonNode rootNode;

        // Load existing settings or create new
        if (File.Exists(settingsPath))
        {
            var existingJson = await File.ReadAllTextAsync(settingsPath);
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
        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        var finalJson = rootNode.ToJsonString(options);
        await File.WriteAllTextAsync(settingsPath, finalJson);

        _logger.LogInformation("Installed hooks for agent {AgentName} at {WorktreePath}", agentName, worktreePath);
    }

    public async Task UninstallHooksAsync(string worktreePath)
    {
        var settingsPath = Path.Combine(worktreePath, ".claude", "settings.local.json");

        if (!File.Exists(settingsPath))
        {
            return;
        }

        var json = await File.ReadAllTextAsync(settingsPath);
        var rootNode = JsonNode.Parse(json);

        if (rootNode is JsonObject rootObj && rootObj.ContainsKey("hooks"))
        {
            rootObj.Remove("hooks");

            var options = new JsonSerializerOptions { WriteIndented = true };
            var finalJson = rootNode.ToJsonString(options);
            await File.WriteAllTextAsync(settingsPath, finalJson);

            _logger.LogInformation("Uninstalled hooks from {WorktreePath}", worktreePath);
        }
    }

    public async Task<bool> AreHooksInstalledAsync(string worktreePath)
    {
        var settingsPath = Path.Combine(worktreePath, ".claude", "settings.local.json");

        if (!File.Exists(settingsPath))
        {
            return false;
        }

        try
        {
            var json = await File.ReadAllTextAsync(settingsPath);
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
