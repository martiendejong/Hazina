# UpdateStore Safety Policies

**Critical Documentation for AI-Driven Document Modification**

This document establishes comprehensive safety policies for the `UpdateStore` functionality in Hazina.Generator, which allows AI agents to autonomously create, modify, move, and delete documents in document stores.

---

## Table of Contents

1. [Overview](#overview)
2. [Risk Assessment](#risk-assessment)
3. [Safety Architecture](#safety-architecture)
4. [Implementation Guidelines](#implementation-guidelines)
5. [Dangerous Patterns](#dangerous-patterns)
6. [Safe Patterns](#safe-patterns)
7. [Validation Requirements](#validation-requirements)
8. [Rollback Strategies](#rollback-strategies)
9. [Security Considerations](#security-considerations)
10. [Production Checklist](#production-checklist)

---

## Overview

### What is UpdateStore?

`UpdateStore` is a method in `DocumentGenerator` that allows AI models to autonomously modify document stores through structured responses:

```csharp
var response = await generator.UpdateStore(
    "Create a new file called 'notes.txt' with my meeting notes",
    CancellationToken.None,
    history: history,
    addRelevantDocuments: true
);
```

**Location:** `C:\Projects\hazina\src\Core\Agents\Hazina.Generator\Core\DocumentGenerator.cs:101`

### Capabilities

The AI can execute three types of operations:

1. **Modifications** - Create or update files (`Store.Store`)
2. **Deletions** - Remove files (`Store.Remove`)
3. **Moves** - Relocate files (`Store.Move`)

### Risk Level: HIGH

**Why High Risk:**
- AI has direct write access to storage
- Mistakes can cause data loss
- No built-in undo mechanism in UpdateStore
- Potential for cascading failures
- Security vulnerabilities if misused

---

## Risk Assessment

### Critical Risks

| Risk | Severity | Likelihood | Impact |
|------|----------|------------|--------|
| **Unintended Data Loss** | CRITICAL | Medium | Complete file deletion without backup |
| **Path Traversal Attack** | CRITICAL | Low | AI writes outside intended directory |
| **Overwrite Critical Files** | HIGH | Medium | Configuration/system files overwritten |
| **Infinite Loop Modifications** | HIGH | Low | AI repeatedly modifies same files |
| **Malformed Content** | MEDIUM | High | Corrupted files from AI hallucination |
| **Permission Escalation** | HIGH | Low | AI modifies access control files |

### Threat Model

**Adversarial Scenarios:**
1. **Malicious User Input:** User crafts prompt to delete all files
2. **AI Hallucination:** Model generates nonsense file modifications
3. **Path Injection:** User tricks AI into writing to `/etc/passwd`
4. **Resource Exhaustion:** AI creates millions of small files
5. **Data Exfiltration:** AI copies sensitive data to public locations

---

## Safety Architecture

### Multi-Layer Defense

```
┌─────────────────────────────────────────────────────────┐
│              Layer 1: Input Validation                  │
│  • Prompt safety checks                                 │
│  • User authorization verification                      │
│  • Rate limiting                                        │
└─────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────┐
│           Layer 2: Pre-Execution Validation             │
│  • Path whitelist/blacklist                             │
│  • File size limits                                     │
│  • Operation quota enforcement                          │
└─────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────┐
│             Layer 3: Atomic Transaction                 │
│  • Create backup snapshot                               │
│  • Execute all operations                               │
│  • Validate results                                     │
│  • Rollback on any failure                              │
└─────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────┐
│           Layer 4: Post-Execution Validation            │
│  • File integrity checks                                │
│  • Storage quota verification                           │
│  • Audit logging                                        │
└─────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────┐
│               Layer 5: Monitoring & Alerts              │
│  • Anomaly detection                                    │
│  • Real-time alerts                                     │
│  • Recovery procedures                                  │
└─────────────────────────────────────────────────────────┘
```

### Core Principles

1. **Principle of Least Privilege** - Grant minimum permissions needed
2. **Fail-Safe Defaults** - Default to read-only unless explicitly enabled
3. **Defense in Depth** - Multiple independent security layers
4. **Audit Everything** - Log all operations for forensics
5. **Graceful Degradation** - System remains functional if safety features fail

---

## Implementation Guidelines

### Step 1: Enable UpdateStore (Opt-In)

UpdateStore should be **disabled by default**. Require explicit opt-in:

```csharp
public class SafeDocumentGenerator : DocumentGenerator
{
    public bool UpdateStoreEnabled { get; set; } = false;  // DEFAULT: DISABLED

    public async Task<LLMResponse<string>> UpdateStore(
        string message,
        CancellationToken cancel,
        IEnumerable<HazinaChatMessage>? history = null,
        bool addRelevantDocuments = true,
        bool addFilesList = true)
    {
        // SAFETY GATE #1: Check if enabled
        if (!UpdateStoreEnabled)
        {
            throw new InvalidOperationException(
                "UpdateStore is disabled. Set UpdateStoreEnabled = true to allow AI modifications."
            );
        }

        // Continue with implementation...
    }
}
```

### Step 2: Implement Path Validation

**CRITICAL:** Validate all paths before execution:

```csharp
public class UpdateStoreValidator
{
    private readonly HashSet<string> _allowedDirectories;
    private readonly HashSet<string> _forbiddenPaths;
    private readonly long _maxFileSize;

    public UpdateStoreValidator(
        IEnumerable<string> allowedDirectories,
        IEnumerable<string> forbiddenPaths,
        long maxFileSize = 10 * 1024 * 1024)  // 10MB default
    {
        _allowedDirectories = new HashSet<string>(allowedDirectories);
        _forbiddenPaths = new HashSet<string>(forbiddenPaths);
        _maxFileSize = maxFileSize;
    }

    public ValidationResult ValidateOperation(UpdateStoreResponse response)
    {
        var errors = new List<string>();

        // Validate modifications
        if (response.Modifications != null)
        {
            foreach (var mod in response.Modifications)
            {
                // Check path safety
                if (!IsPathSafe(mod.Path))
                    errors.Add($"Unsafe path: {mod.Path}");

                // Check file size
                if (mod.Contents.Length > _maxFileSize)
                    errors.Add($"File too large: {mod.Path} ({mod.Contents.Length} bytes)");

                // Check for path traversal
                if (mod.Path.Contains(".."))
                    errors.Add($"Path traversal detected: {mod.Path}");
            }
        }

        // Validate deletions
        if (response.Deletions != null)
        {
            foreach (var del in response.Deletions)
            {
                if (!IsPathSafe(del.Path))
                    errors.Add($"Unsafe deletion path: {del.Path}");

                if (_forbiddenPaths.Contains(Path.GetFullPath(del.Path)))
                    errors.Add($"Cannot delete protected file: {del.Path}");
            }
        }

        // Validate moves
        if (response.Moves != null)
        {
            foreach (var move in response.Moves)
            {
                if (!IsPathSafe(move.Path) || !IsPathSafe(move.NewPath))
                    errors.Add($"Unsafe move: {move.Path} → {move.NewPath}");
            }
        }

        return new ValidationResult(errors.Count == 0, errors);
    }

    private bool IsPathSafe(string path)
    {
        var fullPath = Path.GetFullPath(path);

        // Check if within allowed directories
        var inAllowed = _allowedDirectories.Any(dir =>
            fullPath.StartsWith(Path.GetFullPath(dir)));

        // Check if in forbidden paths
        var inForbidden = _forbiddenPaths.Any(forbidden =>
            fullPath.StartsWith(Path.GetFullPath(forbidden)));

        return inAllowed && !inForbidden;
    }
}

public record ValidationResult(bool IsValid, List<string> Errors);
```

### Step 3: Implement Transactional Updates

**CRITICAL:** All operations must be atomic (all-or-nothing):

```csharp
public class TransactionalUpdateStore
{
    private readonly IDocumentStore _store;
    private readonly UpdateStoreValidator _validator;

    public async Task<LLMResponse<string>> UpdateStoreWithTransaction(
        UpdateStoreResponse response,
        CancellationToken cancel)
    {
        // SAFETY GATE #2: Validate before execution
        var validation = _validator.ValidateOperation(response);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(
                $"Validation failed: {string.Join(", ", validation.Errors)}"
            );
        }

        // Create backup snapshot
        var snapshot = await CreateSnapshot();

        try
        {
            // Execute all operations
            await ExecuteOperations(response, cancel);

            // Validate results
            var postValidation = await ValidatePostExecution(response);
            if (!postValidation.IsValid)
            {
                throw new InvalidOperationException(
                    $"Post-execution validation failed: {string.Join(", ", postValidation.Errors)}"
                );
            }

            return new LLMResponse<string>(
                response.ResponseMessage ?? "Operations completed successfully",
                new TokenUsage()
            );
        }
        catch (Exception ex)
        {
            // ROLLBACK on any failure
            await RestoreSnapshot(snapshot);
            throw new InvalidOperationException(
                $"UpdateStore transaction failed and was rolled back: {ex.Message}",
                ex
            );
        }
    }

    private async Task<Snapshot> CreateSnapshot()
    {
        // Implementation: Save current state of all affected files
        return new Snapshot();
    }

    private async Task RestoreSnapshot(Snapshot snapshot)
    {
        // Implementation: Restore files to snapshot state
    }
}
```

### Step 4: Add Operation Quotas

Prevent resource exhaustion:

```csharp
public class OperationQuotaEnforcer
{
    private readonly int _maxModificationsPerRequest = 10;
    private readonly int _maxDeletionsPerRequest = 5;
    private readonly int _maxMovesPerRequest = 5;
    private readonly long _maxTotalBytesPerRequest = 50 * 1024 * 1024;  // 50MB

    public void EnforceQuotas(UpdateStoreResponse response)
    {
        // Check modification quota
        if (response.Modifications?.Count > _maxModificationsPerRequest)
        {
            throw new InvalidOperationException(
                $"Too many modifications: {response.Modifications.Count} (max: {_maxModificationsPerRequest})"
            );
        }

        // Check deletion quota
        if (response.Deletions?.Count > _maxDeletionsPerRequest)
        {
            throw new InvalidOperationException(
                $"Too many deletions: {response.Deletions.Count} (max: {_maxDeletionsPerRequest})"
            );
        }

        // Check total bytes
        var totalBytes = response.Modifications?.Sum(m => m.Contents?.Length ?? 0) ?? 0;
        if (totalBytes > _maxTotalBytesPerRequest)
        {
            throw new InvalidOperationException(
                $"Total data too large: {totalBytes} bytes (max: {_maxTotalBytesPerRequest})"
            );
        }
    }
}
```

---

## Dangerous Patterns

### ❌ NEVER DO THIS

#### Pattern 1: No Validation

```csharp
// DANGEROUS: Direct execution without validation
var response = await generator.UpdateStore(userInput, cancel);
// ❌ AI can delete anything, write anywhere
```

**Risk:** Complete data loss, security breach
**Fix:** Always validate paths and operations before execution

---

#### Pattern 2: User-Controlled Paths

```csharp
// DANGEROUS: User provides path directly
var response = await generator.UpdateStore(
    $"Create file at {userProvidedPath}",
    cancel
);
// ❌ Path traversal attack: userProvidedPath = "/etc/passwd"
```

**Risk:** Path traversal, privilege escalation
**Fix:** Validate paths against whitelist, normalize paths, reject `..`

---

#### Pattern 3: No Rollback

```csharp
// DANGEROUS: No transaction or rollback
await store.Store(path1, content1, null, false);
await store.Store(path2, content2, null, false);
// ❌ If second operation fails, first is committed (inconsistent state)
```

**Risk:** Inconsistent data, partial updates
**Fix:** Use transactional updates with rollback on failure

---

#### Pattern 4: Unlimited Operations

```csharp
// DANGEROUS: No quota enforcement
var response = await generator.UpdateStore(
    "Create 1 million files",
    cancel
);
// ❌ Resource exhaustion, DoS
```

**Risk:** Disk full, system crash
**Fix:** Enforce quotas on operation count and data size

---

#### Pattern 5: No Audit Logging

```csharp
// DANGEROUS: Silent execution
await generator.UpdateStore(message, cancel);
// ❌ No record of what was modified, by whom, when
```

**Risk:** Impossible to debug, no forensics
**Fix:** Log all operations to audit trail

---

## Safe Patterns

### ✅ ALWAYS DO THIS

#### Pattern 1: Full Validation Pipeline

```csharp
// SAFE: Multi-layer validation
public async Task<LLMResponse<string>> SafeUpdateStore(
    string message,
    string userId,
    CancellationToken cancel)
{
    // Layer 1: Input validation
    if (string.IsNullOrWhiteSpace(message))
        throw new ArgumentException("Message cannot be empty");

    // Layer 2: Rate limiting
    await _rateLimiter.CheckAndIncrementAsync(userId);

    // Layer 3: Get AI response
    var response = await _generator.UpdateStore(message, cancel);

    // Layer 4: Parse and validate operations
    var operations = ParseOperations(response.Result);
    var validation = _validator.ValidateOperation(operations);

    if (!validation.IsValid)
    {
        _logger.LogWarning(
            "UpdateStore validation failed for user {UserId}: {Errors}",
            userId,
            string.Join(", ", validation.Errors)
        );
        throw new InvalidOperationException(
            $"Operation not allowed: {string.Join(", ", validation.Errors)}"
        );
    }

    // Layer 5: Execute with transaction
    return await _transactional.UpdateStoreWithTransaction(operations, cancel);
}
```

---

#### Pattern 2: Path Whitelisting

```csharp
// SAFE: Only allow operations in specific directories
var allowedDirs = new[] {
    "/app/data/documents",
    "/app/data/notes"
};

var forbiddenPaths = new[] {
    "/app/config",
    "/app/secrets",
    "/etc",
    "/sys"
};

var validator = new UpdateStoreValidator(allowedDirs, forbiddenPaths);
```

---

#### Pattern 3: Dry-Run Mode

```csharp
// SAFE: Preview changes before applying
public async Task<PreviewResult> PreviewUpdateStore(
    string message,
    CancellationToken cancel)
{
    var response = await _generator.UpdateStore(message, cancel);

    return new PreviewResult
    {
        ModificationsCount = response.Modifications?.Count ?? 0,
        DeletionsCount = response.Deletions?.Count ?? 0,
        MovesCount = response.Moves?.Count ?? 0,
        EstimatedSize = CalculateSize(response),
        Operations = response  // Return for user approval
    };
}

// User approves, then execute
if (await ConfirmWithUser(preview))
{
    await ExecuteOperations(preview.Operations);
}
```

---

#### Pattern 4: Immutable Audit Trail

```csharp
// SAFE: Log every operation
public class UpdateStoreAuditor
{
    private readonly IAuditLog _auditLog;

    public async Task LogOperation(
        string userId,
        UpdateStoreResponse response,
        bool success,
        string? errorMessage = null)
    {
        await _auditLog.WriteAsync(new AuditEntry
        {
            Timestamp = DateTime.UtcNow,
            UserId = userId,
            OperationType = "UpdateStore",
            ModificationsCount = response.Modifications?.Count ?? 0,
            DeletionsCount = response.Deletions?.Count ?? 0,
            MovesCount = response.Moves?.Count ?? 0,
            Success = success,
            ErrorMessage = errorMessage,
            AffectedPaths = GetAffectedPaths(response)
        });
    }
}
```

---

## Validation Requirements

### Mandatory Checks

Every UpdateStore operation MUST pass these checks:

#### 1. Path Validation

```csharp
✅ Path is within allowed directories
✅ Path does not contain ".."
✅ Path does not contain null bytes
✅ Path is normalized (Path.GetFullPath)
✅ Path is not in forbidden list
✅ Path does not point to system files
```

#### 2. Content Validation

```csharp
✅ File size < maximum (default 10MB)
✅ Content is valid UTF-8 (for text files)
✅ Content does not contain malicious patterns
✅ Total data size < quota (default 50MB per request)
```

#### 3. Operation Validation

```csharp
✅ Modification count < limit (default 10)
✅ Deletion count < limit (default 5)
✅ Move count < limit (default 5)
✅ No circular dependencies in moves
✅ No operations on non-existent files (for moves/deletes)
```

#### 4. Permission Validation

```csharp
✅ User has write permission to target directory
✅ User is authenticated
✅ User is authorized for UpdateStore
✅ Rate limit not exceeded
```

---

## Rollback Strategies

### Strategy 1: Snapshot-Based Rollback

```csharp
public class SnapshotRollback
{
    public async Task<Snapshot> CreateSnapshot(IEnumerable<string> paths)
    {
        var snapshot = new Snapshot
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            Files = new Dictionary<string, FileSnapshot>()
        };

        foreach (var path in paths)
        {
            if (File.Exists(path))
            {
                snapshot.Files[path] = new FileSnapshot
                {
                    Content = await File.ReadAllBytesAsync(path),
                    Metadata = new FileInfo(path)
                };
            }
        }

        return snapshot;
    }

    public async Task RestoreSnapshot(Snapshot snapshot)
    {
        foreach (var (path, file) in snapshot.Files)
        {
            // Restore original file
            await File.WriteAllBytesAsync(path, file.Content);

            // Restore metadata
            File.SetLastWriteTime(path, file.Metadata.LastWriteTime);
        }
    }
}
```

### Strategy 2: Transaction Log Rollback

```csharp
public class TransactionLog
{
    private readonly List<IReversibleOperation> _operations = new();

    public void RecordModification(string path, byte[] originalContent)
    {
        _operations.Add(new ModificationOperation(path, originalContent));
    }

    public void RecordDeletion(string path, byte[] content)
    {
        _operations.Add(new DeletionOperation(path, content));
    }

    public void RecordMove(string oldPath, string newPath)
    {
        _operations.Add(new MoveOperation(oldPath, newPath));
    }

    public async Task Rollback()
    {
        // Execute operations in reverse order
        foreach (var operation in _operations.AsEnumerable().Reverse())
        {
            await operation.Undo();
        }
    }
}
```

---

## Security Considerations

### Access Control

```csharp
public class UpdateStoreAccessControl
{
    private readonly IAuthorizationService _authz;

    public async Task<bool> CanExecuteUpdateStore(
        string userId,
        UpdateStoreResponse response)
    {
        // Check user has UpdateStore permission
        if (!await _authz.HasPermissionAsync(userId, "updatestore"))
            return false;

        // Check user has write access to all affected paths
        var allPaths = GetAffectedPaths(response);
        foreach (var path in allPaths)
        {
            if (!await _authz.CanWriteAsync(userId, path))
                return false;
        }

        return true;
    }
}
```

### Sandboxing

```csharp
// Isolate UpdateStore operations in separate process
public class SandboxedUpdateStore
{
    public async Task<LLMResponse<string>> ExecuteInSandbox(
        UpdateStoreResponse response,
        CancellationToken cancel)
    {
        // Create isolated environment
        var sandbox = await _containerService.CreateSandboxAsync();

        try
        {
            // Copy allowed directories to sandbox
            await sandbox.MountDirectoryAsync("/app/data");

            // Execute operations in sandbox
            var result = await sandbox.ExecuteAsync(() =>
                ApplyOperations(response));

            // Validate results
            if (result.Success)
            {
                // Copy modified files back to host
                await sandbox.SyncBackAsync();
            }

            return result;
        }
        finally
        {
            await sandbox.DisposeAsync();
        }
    }
}
```

### Encryption

```csharp
// Encrypt sensitive content before storage
public class EncryptedUpdateStore
{
    private readonly IEncryptionService _encryption;

    public async Task StoreEncrypted(
        string path,
        string content,
        string encryptionKey)
    {
        var encrypted = await _encryption.EncryptAsync(
            content,
            encryptionKey
        );

        await _store.Store(path, encrypted, null, false);
    }
}
```

---

## Production Checklist

Before deploying UpdateStore to production, verify:

### Security

- [ ] UpdateStore is disabled by default (opt-in required)
- [ ] Path whitelist configured (only allowed directories)
- [ ] Path blacklist configured (system files forbidden)
- [ ] File size limits enforced (default 10MB per file)
- [ ] Total data size limits enforced (default 50MB per request)
- [ ] Operation quotas enforced (max 10 mods, 5 deletes, 5 moves)
- [ ] User authentication required
- [ ] User authorization checked (updatestore permission)
- [ ] Rate limiting enabled (default 10 requests per hour per user)

### Reliability

- [ ] Transactional updates implemented (all-or-nothing)
- [ ] Rollback strategy implemented (snapshot or transaction log)
- [ ] Pre-execution validation enabled
- [ ] Post-execution validation enabled
- [ ] Graceful degradation on validation failure
- [ ] Timeout configured (default 30 seconds per operation)

### Observability

- [ ] Audit logging enabled (all operations logged)
- [ ] Metrics collection enabled (success/failure rates)
- [ ] Alerting configured (anomaly detection)
- [ ] Error reporting configured (Sentry, Application Insights)
- [ ] Performance monitoring enabled (latency tracking)

### Testing

- [ ] Unit tests for all validators
- [ ] Integration tests for UpdateStore
- [ ] Security tests (path traversal, injection attacks)
- [ ] Load tests (resource exhaustion scenarios)
- [ ] Chaos tests (failure injection, rollback verification)

### Documentation

- [ ] Safety policies reviewed by security team
- [ ] User documentation updated (UpdateStore limitations)
- [ ] Incident response playbook created
- [ ] Rollback procedures documented
- [ ] Security review completed

### Recovery

- [ ] Backup strategy documented (how often, retention)
- [ ] Restore procedure tested (recovery time objective)
- [ ] Disaster recovery plan created
- [ ] Incident escalation path defined
- [ ] Contact information updated (security team, on-call)

---

## Example: Production-Ready UpdateStore

Complete implementation with all safety features:

```csharp
public class ProductionUpdateStore
{
    private readonly DocumentGenerator _generator;
    private readonly UpdateStoreValidator _validator;
    private readonly OperationQuotaEnforcer _quotaEnforcer;
    private readonly UpdateStoreAuditor _auditor;
    private readonly TransactionalUpdateStore _transactional;
    private readonly IAuthorizationService _authz;
    private readonly IRateLimiter _rateLimiter;
    private readonly ILogger<ProductionUpdateStore> _logger;

    public async Task<LLMResponse<string>> SafeUpdateStore(
        string message,
        string userId,
        CancellationToken cancel)
    {
        // STEP 1: Authentication & Authorization
        if (!await _authz.HasPermissionAsync(userId, "updatestore"))
        {
            _logger.LogWarning("User {UserId} denied UpdateStore access", userId);
            throw new UnauthorizedAccessException("UpdateStore permission required");
        }

        // STEP 2: Rate Limiting
        if (!await _rateLimiter.TryAcquireAsync(userId))
        {
            _logger.LogWarning("User {UserId} rate limited", userId);
            throw new InvalidOperationException("Rate limit exceeded");
        }

        try
        {
            // STEP 3: Get AI Response
            _logger.LogInformation(
                "UpdateStore request from user {UserId}: {Message}",
                userId,
                message
            );

            var aiResponse = await _generator.UpdateStore(
                message,
                cancel,
                addRelevantDocuments: true,
                addFilesList: true
            );

            // Parse response
            var operations = ParseOperations(aiResponse.Result);

            // STEP 4: Quota Enforcement
            _quotaEnforcer.EnforceQuotas(operations);

            // STEP 5: Validation
            var validation = _validator.ValidateOperation(operations);
            if (!validation.IsValid)
            {
                _logger.LogWarning(
                    "Validation failed for user {UserId}: {Errors}",
                    userId,
                    string.Join(", ", validation.Errors)
                );

                await _auditor.LogOperation(userId, operations, false,
                    $"Validation failed: {string.Join(", ", validation.Errors)}");

                throw new InvalidOperationException(
                    $"Operation not allowed: {string.Join(", ", validation.Errors)}"
                );
            }

            // STEP 6: Execute with Transaction
            var result = await _transactional.UpdateStoreWithTransaction(
                operations,
                cancel
            );

            // STEP 7: Audit Log
            await _auditor.LogOperation(userId, operations, true);

            _logger.LogInformation(
                "UpdateStore completed successfully for user {UserId}",
                userId
            );

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "UpdateStore failed for user {UserId}",
                userId
            );

            // Log failure
            await _auditor.LogOperation(userId, null, false, ex.Message);

            throw;
        }
    }
}
```

---

## Conclusion

UpdateStore is a powerful but dangerous feature. **NEVER use it in production without implementing all safety layers** described in this document.

### Key Takeaways

1. **Disabled by Default** - Require explicit opt-in
2. **Validate Everything** - Paths, sizes, quotas, permissions
3. **Use Transactions** - All-or-nothing with rollback
4. **Audit Everything** - Immutable logs for forensics
5. **Limit Exposure** - Rate limits, quotas, sandboxing

### Further Reading

- [Hazina.Generator README](../src/Core/Agents/Hazina.Generator/README.md)
- [UpdateStoreResponse.cs](../src/Core/Agents/Hazina.Generator/Models/UpdateStoreResponse.cs)
- [DocumentGenerator.cs](../src/Core/Agents/Hazina.Generator/Core/DocumentGenerator.cs)

---

**Last Updated:** 2026-03-19
**Hazina Version:** 1.0.1
**Review Required:** Before every production deployment
