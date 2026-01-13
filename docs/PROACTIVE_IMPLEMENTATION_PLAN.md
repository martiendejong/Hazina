# Proactive Agent Implementation Plan
**Date**: 2026-01-13
**Companion to**: PROACTIVE_AGENT_ANALYSIS.md
**Purpose**: Concrete implementation guidance for each proactive function

---

## Quick Reference

| Module | Priority | Files to Create | Est. Hours | Prerequisites |
|--------|----------|----------------|------------|---------------|
| PermissionGuard | P1-Critical | 12 files | 80h (2 weeks) | None |
| TaskScheduler | P1-High | 10 files | 80h (2 weeks) | None |
| TriggerSystem | P2-Medium | 6 files | 40h (1 week) | EvaluationPipeline |
| SuggestionBus | P2-Medium | 8 files | 40h (1 week) | None |
| Context Prediction | P2-Medium | 4 files | 40h (1 week) | SmartContextBuilder |
| RAG Prefetching | P2-Medium | 5 files | 40h (1 week) | RAGEngine |
| ObserverEngine | P3-Low | 10 files | 80h (2 weeks) | None |
| PredictiveModel | P3-Low | 8 files | 120h (3 weeks) | ObserverEngine |
| Task Generation | P3-Low | 6 files | 80h (2 weeks) | CodeGeneration |

---

## Phase 1: Safety & Infrastructure

### 1. PermissionGuard (P1-CRITICAL)

#### File Structure
```
src/Core/Security/Hazina.Security.Permissions/
├── Core/
│   ├── IPermissionGuard.cs
│   ├── PermissionGuard.cs
│   └── PermissionLevel.cs
├── Models/
│   ├── PermissionRequest.cs
│   ├── PermissionResult.cs
│   ├── ActionContext.cs
│   └── SafetyRules.cs
├── Storage/
│   ├── IPermissionStore.cs
│   └── SQLitePermissionStore.cs
├── Policies/
│   ├── IPermissionPolicy.cs
│   ├── DefaultPermissionPolicy.cs
│   └── FileSystemPolicy.cs
├── Audit/
│   ├── IPermissionAuditor.cs
│   └── PermissionAuditor.cs
└── Hazina.Security.Permissions.csproj
```

#### Implementation Steps

**Step 1: Create Core Interface** (`IPermissionGuard.cs`)
```csharp
namespace Hazina.Security.Permissions.Core;

public interface IPermissionGuard
{
    /// <summary>
    /// Check if an action is permitted for the given user/agent
    /// </summary>
    Task<PermissionResult> CheckPermissionAsync(
        string userId,
        PermissionRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Validate action safety against configured rules
    /// </summary>
    Task<bool> IsActionSafeAsync(
        ActionContext action,
        SafetyRules rules,
        CancellationToken ct = default);

    /// <summary>
    /// Register a custom permission policy
    /// </summary>
    void RegisterPolicy(IPermissionPolicy policy);
}

public enum PermissionLevel
{
    None = 0,
    Read = 1,        // Read files, query data
    Write = 2,       // Write files, modify data
    Execute = 3,     // Run commands, call APIs
    Admin = 4        // Delete, system changes
}
```

**Step 2: Create Models** (`PermissionRequest.cs`)
```csharp
namespace Hazina.Security.Permissions.Models;

public record PermissionRequest
{
    public required string Action { get; init; }
    public required PermissionLevel RequiredLevel { get; init; }
    public Dictionary<string, object> Parameters { get; init; } = new();
    public string? ResourcePath { get; init; }
}

public record PermissionResult
{
    public bool Allowed { get; init; }
    public string? Reason { get; init; }
    public PermissionLevel GrantedLevel { get; init; }
    public Dictionary<string, object> Metadata { get; init; } = new();
}

public record ActionContext
{
    public required string ActionName { get; init; }
    public required string ToolName { get; init; }
    public Dictionary<string, object> Parameters { get; init; } = new();
    public string? UserId { get; init; }
    public string? AgentId { get; init; }
}

public record SafetyRules
{
    public List<string> BlockedActions { get; init; } = new();
    public List<string> AllowedPaths { get; init; } = new();
    public bool RequireApprovalForDelete { get; init; } = true;
    public bool AllowNetworkCalls { get; init; } = false;
    public int MaxFileSize { get; init; } = 10_000_000; // 10MB
}
```

**Step 3: Implement Permission Guard** (`PermissionGuard.cs`)
```csharp
namespace Hazina.Security.Permissions.Core;

using Microsoft.Extensions.Logging;
using Hazina.Security.Permissions.Models;
using Hazina.Security.Permissions.Policies;
using Hazina.Security.Permissions.Storage;
using Hazina.Security.Permissions.Audit;

public class PermissionGuard : IPermissionGuard
{
    private readonly IPermissionStore _store;
    private readonly IPermissionAuditor _auditor;
    private readonly ILogger<PermissionGuard> _logger;
    private readonly List<IPermissionPolicy> _policies = new();

    public PermissionGuard(
        IPermissionStore store,
        IPermissionAuditor auditor,
        ILogger<PermissionGuard> logger)
    {
        _store = store;
        _auditor = auditor;
        _logger = logger;

        // Register default policies
        RegisterPolicy(new DefaultPermissionPolicy());
        RegisterPolicy(new FileSystemPolicy());
    }

    public async Task<PermissionResult> CheckPermissionAsync(
        string userId,
        PermissionRequest request,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Checking permission for user {UserId}, action {Action}",
            userId, request.Action);

        // 1. Check user's permission level
        var userPermissions = await _store.GetUserPermissionsAsync(userId, ct);
        if (userPermissions.Level < request.RequiredLevel)
        {
            var result = new PermissionResult
            {
                Allowed = false,
                Reason = $"User level {userPermissions.Level} < required {request.RequiredLevel}",
                GrantedLevel = userPermissions.Level
            };

            await _auditor.LogDenialAsync(userId, request, result.Reason, ct);
            return result;
        }

        // 2. Check policies
        foreach (var policy in _policies)
        {
            if (!await policy.CheckAsync(userId, request, ct))
            {
                var result = new PermissionResult
                {
                    Allowed = false,
                    Reason = $"Blocked by policy: {policy.GetType().Name}",
                    GrantedLevel = userPermissions.Level
                };

                await _auditor.LogDenialAsync(userId, request, result.Reason, ct);
                return result;
            }
        }

        // 3. Permission granted
        await _auditor.LogApprovalAsync(userId, request, ct);
        return new PermissionResult
        {
            Allowed = true,
            GrantedLevel = userPermissions.Level
        };
    }

    public async Task<bool> IsActionSafeAsync(
        ActionContext action,
        SafetyRules rules,
        CancellationToken ct = default)
    {
        // Check if action is explicitly blocked
        if (rules.BlockedActions.Contains(action.ActionName))
        {
            _logger.LogWarning("Action {Action} is blocked by safety rules", action.ActionName);
            return false;
        }

        // Check file system access
        if (action.Parameters.TryGetValue("path", out var pathObj) && pathObj is string path)
        {
            if (!IsPathAllowed(path, rules.AllowedPaths))
            {
                _logger.LogWarning("Path {Path} is not in allowed paths", path);
                return false;
            }
        }

        // Check for delete operations
        if (action.ActionName.Contains("delete", StringComparison.OrdinalIgnoreCase))
        {
            if (rules.RequireApprovalForDelete)
            {
                _logger.LogWarning("Delete action requires explicit approval");
                return false;
            }
        }

        // All checks passed
        return true;
    }

    public void RegisterPolicy(IPermissionPolicy policy)
    {
        _policies.Add(policy);
        _logger.LogInformation("Registered permission policy: {Policy}", policy.GetType().Name);
    }

    private bool IsPathAllowed(string path, List<string> allowedPaths)
    {
        if (allowedPaths.Count == 0)
            return true; // No restrictions

        var normalizedPath = Path.GetFullPath(path);
        return allowedPaths.Any(allowed =>
            normalizedPath.StartsWith(Path.GetFullPath(allowed), StringComparison.OrdinalIgnoreCase));
    }
}
```

**Step 4: Create Storage** (`SQLitePermissionStore.cs`)
```csharp
namespace Hazina.Security.Permissions.Storage;

using Microsoft.Data.Sqlite;
using Hazina.Security.Permissions.Models;

public interface IPermissionStore
{
    Task<UserPermissions> GetUserPermissionsAsync(string userId, CancellationToken ct = default);
    Task SetUserPermissionsAsync(string userId, PermissionLevel level, CancellationToken ct = default);
}

public record UserPermissions(string UserId, PermissionLevel Level);

public class SQLitePermissionStore : IPermissionStore
{
    private readonly string _connectionString;

    public SQLitePermissionStore(string dbPath)
    {
        _connectionString = $"Data Source={dbPath}";
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS user_permissions (
                user_id TEXT PRIMARY KEY,
                permission_level INTEGER NOT NULL,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );
        ";
        cmd.ExecuteNonQuery();
    }

    public async Task<UserPermissions> GetUserPermissionsAsync(string userId, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT permission_level FROM user_permissions WHERE user_id = @userId";
        cmd.Parameters.AddWithValue("@userId", userId);

        var level = await cmd.ExecuteScalarAsync(ct);
        if (level == null)
        {
            // Default: Read-only for new users
            return new UserPermissions(userId, PermissionLevel.Read);
        }

        return new UserPermissions(userId, (PermissionLevel)(long)level);
    }

    public async Task SetUserPermissionsAsync(string userId, PermissionLevel level, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT OR REPLACE INTO user_permissions (user_id, permission_level, created_at, updated_at)
            VALUES (@userId, @level, @now, @now)
        ";
        cmd.Parameters.AddWithValue("@userId", userId);
        cmd.Parameters.AddWithValue("@level", (int)level);
        cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("O"));

        await cmd.ExecuteNonQueryAsync(ct);
    }
}
```

**Step 5: Integration with Tools**

Update all existing tool contexts to check permissions:

```csharp
// Example: In StoreToolsContext.cs or similar
public abstract class SecureToolsContext : IToolsContext
{
    private readonly IPermissionGuard _permissionGuard;
    private readonly string _userId;

    protected SecureToolsContext(IPermissionGuard permissionGuard, string userId)
    {
        _permissionGuard = permissionGuard;
        _userId = userId;
    }

    protected async Task<bool> CheckPermissionAsync(
        string action,
        PermissionLevel requiredLevel,
        Dictionary<string, object>? parameters = null)
    {
        var request = new PermissionRequest
        {
            Action = action,
            RequiredLevel = requiredLevel,
            Parameters = parameters ?? new()
        };

        var result = await _permissionGuard.CheckPermissionAsync(_userId, request);

        if (!result.Allowed)
        {
            throw new UnauthorizedAccessException($"Permission denied: {result.Reason}");
        }

        return true;
    }

    // Example tool with permission check
    public async Task<string> WriteFileAsync(string path, string content)
    {
        await CheckPermissionAsync("write_file", PermissionLevel.Write, new()
        {
            ["path"] = path
        });

        // Actual file write logic
        await File.WriteAllTextAsync(path, content);
        return $"File written: {path}";
    }
}
```

#### Testing Strategy

```csharp
// tests/Hazina.Security.Permissions.Tests/PermissionGuardTests.cs
public class PermissionGuardTests
{
    [Fact]
    public async Task CheckPermission_UserHasRequiredLevel_ReturnsAllowed()
    {
        // Arrange
        var store = new InMemoryPermissionStore();
        await store.SetUserPermissionsAsync("user1", PermissionLevel.Write);

        var guard = new PermissionGuard(store, new NullAuditor(), NullLogger<PermissionGuard>.Instance);

        // Act
        var result = await guard.CheckPermissionAsync("user1", new PermissionRequest
        {
            Action = "write_file",
            RequiredLevel = PermissionLevel.Write
        });

        // Assert
        Assert.True(result.Allowed);
    }

    [Fact]
    public async Task CheckPermission_UserLacksRequiredLevel_ReturnsDenied()
    {
        // Arrange
        var store = new InMemoryPermissionStore();
        await store.SetUserPermissionsAsync("user1", PermissionLevel.Read);

        var guard = new PermissionGuard(store, new NullAuditor(), NullLogger<PermissionGuard>.Instance);

        // Act
        var result = await guard.CheckPermissionAsync("user1", new PermissionRequest
        {
            Action = "execute_command",
            RequiredLevel = PermissionLevel.Execute
        });

        // Assert
        Assert.False(result.Allowed);
        Assert.Contains("required Execute", result.Reason);
    }

    [Fact]
    public async Task IsActionSafe_BlockedAction_ReturnsFalse()
    {
        // Arrange
        var guard = new PermissionGuard(...);
        var rules = new SafetyRules
        {
            BlockedActions = new() { "delete_database" }
        };

        // Act
        var result = await guard.IsActionSafeAsync(new ActionContext
        {
            ActionName = "delete_database",
            ToolName = "DatabaseTool"
        }, rules);

        // Assert
        Assert.False(result);
    }
}
```

---

### 2. TaskScheduler (P1-High)

#### File Structure
```
src/Core/Scheduling/Hazina.TaskScheduling/
├── Core/
│   ├── ITaskScheduler.cs
│   ├── TaskScheduler.cs
│   └── TaskStatus.cs
├── Models/
│   ├── ScheduledTask.cs
│   ├── TaskResult.cs
│   └── TaskOptions.cs
├── Storage/
│   ├── ITaskStore.cs
│   └── SQLiteTaskStore.cs
├── Workers/
│   ├── BackgroundTaskWorker.cs
│   └── TaskExecutor.cs
└── Hazina.TaskScheduling.csproj
```

#### Implementation Steps

**Step 1: Create Interface** (`ITaskScheduler.cs`)
```csharp
namespace Hazina.TaskScheduling.Core;

public interface ITaskScheduler
{
    /// <summary>
    /// Schedule a task for execution
    /// </summary>
    Task<Guid> ScheduleAsync(ScheduledTask task, CancellationToken ct = default);

    /// <summary>
    /// Get task status
    /// </summary>
    Task<TaskStatus> GetStatusAsync(Guid taskId, CancellationToken ct = default);

    /// <summary>
    /// Cancel a pending task
    /// </summary>
    Task CancelAsync(Guid taskId, CancellationToken ct = default);

    /// <summary>
    /// Get task result (waits if not complete)
    /// </summary>
    Task<TaskResult> GetResultAsync(Guid taskId, TimeSpan? timeout = null, CancellationToken ct = default);
}

public enum TaskStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled
}
```

**Step 2: Create Models** (`ScheduledTask.cs`)
```csharp
namespace Hazina.TaskScheduling.Models;

public record ScheduledTask
{
    public required string Name { get; init; }
    public required Func<CancellationToken, Task<object>> ExecuteAsync { get; init; }
    public TaskOptions Options { get; init; } = new();
}

public record TaskOptions
{
    public TaskPriority Priority { get; init; } = TaskPriority.Normal;
    public DateTime? ScheduledTime { get; init; }  // Null = immediate
    public TimeSpan? Timeout { get; init; }
    public int MaxRetries { get; init; } = 0;
    public TimeSpan? RetryDelay { get; init; }
}

public enum TaskPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Critical = 3
}

public record TaskResult
{
    public Guid TaskId { get; init; }
    public TaskStatus Status { get; init; }
    public object? Result { get; init; }
    public Exception? Error { get; init; }
    public DateTime StartTime { get; init; }
    public DateTime? EndTime { get; init; }
}
```

**Step 3: Implement Scheduler** (`TaskScheduler.cs`)
```csharp
namespace Hazina.TaskScheduling.Core;

using System.Collections.Concurrent;
using Hazina.TaskScheduling.Models;
using Hazina.TaskScheduling.Storage;

public class TaskScheduler : ITaskScheduler
{
    private readonly ITaskStore _store;
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<TaskResult>> _pendingTasks = new();

    public TaskScheduler(ITaskStore store)
    {
        _store = store;
    }

    public async Task<Guid> ScheduleAsync(ScheduledTask task, CancellationToken ct = default)
    {
        var taskId = Guid.NewGuid();
        var tcs = new TaskCompletionSource<TaskResult>();
        _pendingTasks[taskId] = tcs;

        // Store task metadata
        await _store.CreateTaskAsync(taskId, task.Name, task.Options, ct);

        // Queue for execution (BackgroundTaskWorker will pick it up)
        _ = Task.Run(async () =>
        {
            try
            {
                // Wait if scheduled for future
                if (task.Options.ScheduledTime.HasValue)
                {
                    var delay = task.Options.ScheduledTime.Value - DateTime.UtcNow;
                    if (delay > TimeSpan.Zero)
                    {
                        await Task.Delay(delay, ct);
                    }
                }

                await _store.UpdateStatusAsync(taskId, TaskStatus.Running, ct);

                // Execute task
                var result = await ExecuteWithRetryAsync(taskId, task, ct);

                await _store.UpdateStatusAsync(taskId, TaskStatus.Completed, ct);
                await _store.StoreResultAsync(taskId, result, ct);

                tcs.SetResult(new TaskResult
                {
                    TaskId = taskId,
                    Status = TaskStatus.Completed,
                    Result = result,
                    EndTime = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                await _store.UpdateStatusAsync(taskId, TaskStatus.Failed, ct);
                await _store.StoreErrorAsync(taskId, ex, ct);

                tcs.SetResult(new TaskResult
                {
                    TaskId = taskId,
                    Status = TaskStatus.Failed,
                    Error = ex,
                    EndTime = DateTime.UtcNow
                });
            }
            finally
            {
                _pendingTasks.TryRemove(taskId, out _);
            }
        }, ct);

        return taskId;
    }

    private async Task<object> ExecuteWithRetryAsync(
        Guid taskId,
        ScheduledTask task,
        CancellationToken ct)
    {
        var attempts = 0;
        Exception? lastError = null;

        while (attempts <= task.Options.MaxRetries)
        {
            try
            {
                using var timeoutCts = task.Options.Timeout.HasValue
                    ? new CancellationTokenSource(task.Options.Timeout.Value)
                    : new CancellationTokenSource();

                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

                return await task.ExecuteAsync(linkedCts.Token);
            }
            catch (Exception ex)
            {
                lastError = ex;
                attempts++;

                if (attempts <= task.Options.MaxRetries && task.Options.RetryDelay.HasValue)
                {
                    await Task.Delay(task.Options.RetryDelay.Value, ct);
                }
            }
        }

        throw lastError!;
    }

    public async Task<TaskStatus> GetStatusAsync(Guid taskId, CancellationToken ct = default)
    {
        return await _store.GetStatusAsync(taskId, ct);
    }

    public async Task CancelAsync(Guid taskId, CancellationToken ct = default)
    {
        await _store.UpdateStatusAsync(taskId, TaskStatus.Cancelled, ct);

        if (_pendingTasks.TryRemove(taskId, out var tcs))
        {
            tcs.SetCanceled(ct);
        }
    }

    public async Task<TaskResult> GetResultAsync(Guid taskId, TimeSpan? timeout = null, CancellationToken ct = default)
    {
        if (_pendingTasks.TryGetValue(taskId, out var tcs))
        {
            // Task is running, wait for completion
            if (timeout.HasValue)
            {
                using var cts = new CancellationTokenSource(timeout.Value);
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, cts.Token);

                return await tcs.Task.WaitAsync(linkedCts.Token);
            }

            return await tcs.Task;
        }

        // Task already complete, fetch from store
        return await _store.GetResultAsync(taskId, ct);
    }
}
```

**Step 4: Integration Example - Brain Module**

```csharp
// Update Brain/Services/MemoryModule.cs

public class MemoryModule : IMemoryModule
{
    private readonly ITaskScheduler _taskScheduler;
    // ... other fields

    public async Task ObserveAsync(ObservationContext context, CancellationToken ct = default)
    {
        // ... store episode ...

        // Schedule fact distillation as background task
        if (_options.DistillOnObserve)
        {
            await _taskScheduler.ScheduleAsync(new ScheduledTask
            {
                Name = $"DistillFacts_{episode.Id}",
                ExecuteAsync = async (ct) =>
                {
                    await _distiller.TryDistillFactsAsync(episode, ct);
                    return episode.Id;
                },
                Options = new TaskOptions
                {
                    Priority = TaskPriority.Low,
                    MaxRetries = 2,
                    RetryDelay = TimeSpan.FromSeconds(5)
                }
            }, ct);
        }
    }
}
```

---

## Phase 2: Proactive Engagement

### 3. TriggerSystem Enhancement (P2-Medium)

#### Implementation

**Extend existing:** `src/Core/AI/Hazina.AI.PromptManagement/Triggers/`

```csharp
// New file: ITriggerSystem.cs
namespace Hazina.AI.Triggers;

public interface ITriggerSystem
{
    Task RegisterTriggerAsync(Trigger trigger);
    Task RemoveTriggerAsync(string triggerId);
    Task<List<TriggeredAction>> EvaluateAsync(TriggerContext context);
}

public record Trigger
{
    public required string Id { get; init; }
    public required ITriggerPattern Pattern { get; init; }
    public required ITriggeredAction Action { get; init; }
    public TriggerConditions Conditions { get; init; } = new();
}

public interface ITriggerPattern
{
    Task<bool> MatchesAsync(TriggerContext context);
}

public interface ITriggeredAction
{
    Task<ActionResult> ExecuteAsync(TriggerContext context);
}

// Example patterns:
public class KeywordPattern : ITriggerPattern
{
    private readonly string[] _keywords;

    public KeywordPattern(params string[] keywords)
    {
        _keywords = keywords;
    }

    public Task<bool> MatchesAsync(TriggerContext context)
    {
        return Task.FromResult(_keywords.Any(k =>
            context.UserInput.Contains(k, StringComparison.OrdinalIgnoreCase)));
    }
}

// Example actions:
public class SuggestToolAction : ITriggeredAction
{
    private readonly string _toolName;
    private readonly ISuggestionBus _suggestionBus;

    public async Task<ActionResult> ExecuteAsync(TriggerContext context)
    {
        await _suggestionBus.PublishAsync(new Suggestion
        {
            Type = SuggestionType.Action,
            Title = $"Use {_toolName}?",
            Content = $"I noticed you mentioned ... Would you like me to use {_toolName}?",
            Actions = new()
            {
                new() { Label = "Yes", ActionId = $"invoke_{_toolName}" },
                new() { Label = "No thanks", ActionId = "dismiss" }
            }
        });

        return new ActionResult { Success = true };
    }
}

// Usage example:
var triggerSystem = new TriggerSystem();

await triggerSystem.RegisterTriggerAsync(new Trigger
{
    Id = "suggest-report-generation",
    Pattern = new KeywordPattern("report", "generate report", "create report"),
    Action = new SuggestToolAction("ReportGenerationTool", suggestionBus)
});
```

---

## Next Steps

1. **Review this plan** with the development team
2. **Set up project** tracking (GitHub Projects or similar)
3. **Begin Phase 1** with PermissionGuard (highest priority)
4. **Create test environments** for each module
5. **Follow phased approach** - don't jump ahead

**Estimated Total Effort:** 600 hours (~4 months with 1 developer, ~2 months with 2 developers)

**Critical Path:** PermissionGuard → TaskScheduler → (Other modules in parallel)

---

**Document Version:** 1.0
**Last Updated:** 2026-01-13
**Status:** Ready for Implementation
**Next Review:** After Phase 1 completion
