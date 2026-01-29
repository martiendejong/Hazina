# Hazina Dynamic Plugin System

**🚀 AI-Powered Runtime Plugin Compilation using Roslyn**

This project enables dynamic creation and execution of C# plugins at runtime, allowing AI or users to generate new capabilities on-demand without redeployment.

## 🎯 Overview

The Hazina Plugin System allows you to:
- **Generate plugins from C# code** at runtime using Roslyn
- **Execute plugins in a sandboxed environment** with timeout and security controls
- **Manage plugin lifecycle** (register, compile, cache, execute, disable/enable)
- **Create tools dynamically** via AI, enabling truly extensible behavior
- **Update plugins on-demand** with automatic versioning and hot-reload (no downtime)
- **Rollback to previous versions** if updates cause issues

This is a **production-ready pattern** used by:
- Azure Functions (C# scripting)
- Unity Game Engine (runtime C# scripting)
- RoslynPad / LINQPad (C# REPL)
- Orchard CMS (dynamic modules)

---

## 📁 Architecture

### Directory Structure

```
Hazina.Core.Plugins/
├── Abstractions/          # Interfaces and contracts
│   ├── IHazinaPlugin.cs          # Base plugin interface
│   ├── PluginContext.cs          # Execution context
│   ├── PluginResult.cs           # Execution result
│   ├── PluginMetadata.cs         # Plugin metadata
│   └── PluginVersion.cs          # Version tracking
├── Compilation/           # Roslyn-based compilation
│   ├── HazinaPluginCompiler.cs   # Compiles C# → Assembly
│   ├── CompiledPlugin.cs         # Compiled plugin wrapper
│   └── PluginCompilationException.cs
├── Execution/             # Sandboxed execution
│   ├── PluginSandbox.cs          # Timeout & security
│   ├── PluginSecuritySettings.cs # Security configuration
│   └── PluginTimeoutException.cs
├── Management/            # Plugin lifecycle
│   ├── PluginManager.cs          # Registration, caching, execution
│   └── IPluginRepository.cs      # Storage interface
└── Tools/                 # Agent tools for plugin system
    ├── CreateDynamicToolTool.cs  # Create new plugins
    ├── ListDynamicToolsTool.cs   # List registered plugins
    ├── ExecuteDynamicToolTool.cs # Execute plugins
    ├── UpdateDynamicToolTool.cs  # Update existing plugins
    ├── RollbackDynamicToolTool.cs # Rollback to previous versions
    └── GetPluginVersionHistoryTool.cs # View version history
```

---

## 🔧 How It Works

### 1. Plugin Creation (AI or User)

AI generates C# code:

```csharp
// User request: "Send a welcome email when a new customer registers"

// AI generates this plugin code:
var customer = context.GetParameter<Customer>("customer");

var emailService = context.GetService<IEmailService>();

await emailService.SendTemplateAsync(
    to: customer.Email,
    template: "welcome",
    data: new { CustomerName = customer.Name }
);

context.Logger.LogInformation("Welcome email sent to {Email}", customer.Email);

return PluginResult.Successful(new { EmailSent = true });
```

### 2. Compilation (Roslyn)

```csharp
var metadata = new PluginMetadata
{
    Id = Guid.NewGuid().ToString(),
    Name = "SendWelcomeEmail",
    SourceCode = aiGeneratedCode,
    Description = "Sends welcome email to new customers",
    CreatedBy = "AI"
};

var pluginId = await pluginManager.RegisterPluginAsync(metadata);
// Plugin is compiled to in-memory assembly and cached
```

### 3. Execution (Sandboxed)

```csharp
var context = new PluginContext
{
    Services = serviceProvider,
    Logger = logger,
    Parameters = new Dictionary<string, object>
    {
        ["customer"] = newCustomer
    }
};

var result = await pluginManager.ExecutePluginAsync(pluginId, context);

if (result.Success)
{
    Console.WriteLine("Plugin executed successfully!");
}
```

---

## 🛠️ Usage Examples

### Example 1: Create Plugin via Tool

```csharp
var createTool = serviceProvider.GetRequiredService<CreateDynamicToolTool>();

var result = await createTool.ExecuteAsync(new Dictionary<string, object>
{
    ["name"] = "HighValueOrderAlert",
    ["source_code"] = @"
        var order = context.GetParameter<Order>(""order"");

        if (order.TotalAmount > 1000)
        {
            var notificationService = context.GetService<INotificationService>();
            await notificationService.SendAsync(
                ""manager"",
                $""High value order: {order.Id} - ${order.TotalAmount}""
            );

            return PluginResult.Successful(new { AlertSent = true });
        }

        return PluginResult.Successful(new { AlertSent = false });
    ",
    ["description"] = "Alert manager for orders over $1000",
    ["tags"] = new[] { "orders", "notifications" }
});
```

### Example 2: List Plugins

```csharp
var listTool = serviceProvider.GetRequiredService<ListDynamicToolsTool>();

var result = await listTool.ExecuteAsync(new Dictionary<string, object>
{
    ["enabled_only"] = true,
    ["format"] = "detailed"
});

Console.WriteLine(result.Output);
```

Output:
```
Found 3 dynamic plugin(s):

Plugin: SendWelcomeEmail
  ID: 123e4567-e89b-12d3-a456-426614174000
  Version: 1.0.0
  Enabled: True
  Created: 2026-01-27 14:30:00 UTC
  Created By: AI
  Description: Sends welcome email to new customers
  Tags: email, customers
  Source Code Length: 456 characters
```

### Example 3: Execute Plugin

```csharp
var executeTool = serviceProvider.GetRequiredService<ExecuteDynamicToolTool>();

var result = await executeTool.ExecuteAsync(new Dictionary<string, object>
{
    ["plugin_identifier"] = "SendWelcomeEmail",
    ["parameters"] = new Dictionary<string, object>
    {
        ["customer"] = new Customer { Email = "john@example.com", Name = "John Doe" }
    }
});

if (result.Success)
{
    Console.WriteLine("Email sent successfully!");
}
```

---

## 🔄 Version Management & Updates

### Update Existing Plugins On-Demand

One of the most powerful features: **modify existing plugin behavior without downtime**.

#### How It Works

1. **Update** - AI or user provides new source code
2. **Version Created** - System creates new version entry (v2, v3, etc.)
3. **Recompilation** - New code compiled with Roslyn
4. **Hot-Reload** - Cache updated atomically (zero downtime)
5. **Previous Versions Kept** - Can rollback if needed

#### Example: Updating a Plugin

```csharp
var updateTool = serviceProvider.GetRequiredService<UpdateDynamicToolTool>();

// User: "Add error handling to the welcome email plugin"
// AI generates improved version:
var improvedCode = @"
    var customer = context.GetParameter<Customer>(""customer"");
    var emailService = context.GetService<IEmailService>();

    try
    {
        await emailService.SendTemplateAsync(
            to: customer.Email,
            template: ""welcome"",
            data: new { CustomerName = customer.Name }
        );

        context.Logger.LogInformation(""Welcome email sent to {Email}"", customer.Email);
        return PluginResult.Successful(new { EmailSent = true });
    }
    catch (Exception ex)
    {
        context.Logger.LogError(ex, ""Failed to send welcome email to {Email}"", customer.Email);
        return PluginResult.Failed($""Email send failed: {ex.Message}"", ex);
    }
";

var result = await updateTool.ExecuteAsync(new Dictionary<string, object>
{
    ["plugin_identifier"] = "SendWelcomeEmail",
    ["new_source_code"] = improvedCode,
    ["change_description"] = "Added try-catch error handling and detailed logging"
});

// Output: "Plugin 'SendWelcomeEmail' updated successfully. New version: 2. Plugin has been hot-reloaded."
```

#### View Version History

```csharp
var historyTool = serviceProvider.GetRequiredService<GetPluginVersionHistoryTool>();

var result = await historyTool.ExecuteAsync(new Dictionary<string, object>
{
    ["plugin_identifier"] = "SendWelcomeEmail"
});

Console.WriteLine(result.Output);
```

Output:
```
Version history for plugin: SendWelcomeEmail
Total versions: 2

★ ACTIVE Version 2
         Created: 2026-01-27 16:15:00 UTC
         Created By: AI
         Changes: Added try-catch error handling and detailed logging
         Source Code: 678 characters

   Version 1
         Created: 2026-01-27 14:30:00 UTC
         Created By: AI
         Changes: Initial version
         Source Code: 456 characters
```

#### Rollback to Previous Version

```csharp
var rollbackTool = serviceProvider.GetRequiredService<RollbackDynamicToolTool>();

// If version 2 has issues, rollback to version 1:
var result = await rollbackTool.ExecuteAsync(new Dictionary<string, object>
{
    ["plugin_identifier"] = "SendWelcomeEmail",
    ["target_version"] = 1
});

// Output: "Plugin 'SendWelcomeEmail' rolled back to version 1. Plugin has been hot-reloaded with previous code."
```

### Versioning Features

✅ **Automatic Version Numbering** - v1, v2, v3...
✅ **Change Descriptions** - Document what changed
✅ **Hot-Reload** - Zero downtime updates
✅ **Complete History** - All versions preserved
✅ **Active Version Tracking** - Only one version active at a time
✅ **Atomic Updates** - Cache invalidation + recompilation in single operation
✅ **Rollback Support** - Revert to any previous version
✅ **Audit Trail** - Who updated, when, and why

### Version Lifecycle

```
Create Plugin (v1)
    ↓
Execute (uses v1)
    ↓
Update Plugin (v2 created)
    ↓
Hot-Reload (v2 now active)
    ↓
Execute (uses v2)
    ↓
Issue Detected!
    ↓
Rollback to v1
    ↓
Hot-Reload (v1 active again)
    ↓
Execute (uses v1)
```

### Use Cases for Updates

1. **Bug Fixes** - Fix logic errors in production plugins
2. **Feature Enhancement** - Add new capabilities to existing plugins
3. **Performance Optimization** - Improve slow-running plugins
4. **Security Patches** - Address security vulnerabilities
5. **Behavioral Changes** - Modify business rules as requirements change
6. **A/B Testing** - Deploy v2, compare to v1, rollback if needed

### Storage Considerations

**Repository must implement version methods:**

```csharp
public interface IPluginRepository
{
    // ... existing methods ...

    // Version management
    Task<string> SaveVersionAsync(PluginVersion version, CancellationToken ct = default);
    Task<List<PluginVersion>> GetVersionsAsync(string pluginId, CancellationToken ct = default);
    Task<PluginVersion?> GetVersionAsync(string pluginId, int versionNumber, CancellationToken ct = default);
    Task<PluginVersion?> GetActiveVersionAsync(string pluginId, CancellationToken ct = default);
    Task<bool> SetActiveVersionAsync(string pluginId, int versionNumber, CancellationToken ct = default);
}
```

**Example (In-Memory):**

```csharp
private readonly ConcurrentDictionary<string, List<PluginVersion>> _versions = new();

public Task<string> SaveVersionAsync(PluginVersion version, CancellationToken ct = default)
{
    if (!_versions.ContainsKey(version.PluginId))
    {
        _versions[version.PluginId] = new List<PluginVersion>();
    }

    _versions[version.PluginId].Add(version);
    return Task.FromResult(version.VersionId);
}

public Task<List<PluginVersion>> GetVersionsAsync(string pluginId, CancellationToken ct = default)
{
    if (_versions.TryGetValue(pluginId, out var versions))
    {
        return Task.FromResult(versions.OrderByDescending(v => v.Version).ToList());
    }

    return Task.FromResult(new List<PluginVersion>());
}
```

---

## 🔒 Security Features

### Timeout Protection
```csharp
var securitySettings = new PluginSecuritySettings
{
    TimeoutSeconds = 30,  // Max 30 seconds per execution
    MaxMemoryBytes = 100 * 1024 * 1024,  // 100 MB limit (not enforced yet)
    StopOnFirstFailure = false
};
```

### Sandboxed Execution
- **Timeout enforcement**: Kills plugin if exceeds time limit
- **Compile-time safety**: No `unsafe` code, overflow checking enabled
- **Assembly isolation**: Each plugin in separate `AssemblyLoadContext`
- **Reference restrictions**: Only allow specific assemblies

### Future Security Enhancements
- Memory limit enforcement
- File system access controls
- Network access controls
- CPU usage limits
- Maximum concurrent execution limits

---

## 🎨 What Plugins Can Access

### Available via `context.GetService<T>()`

Plugins can request any service registered in DI:
- `IEmailService`
- `INotificationService`
- `ILogger<T>`
- `DbContext` (Entity Framework)
- Custom services you register

### Available via `context.Parameters`

Data passed at execution time:
```csharp
var orderId = context.GetParameter<int>("orderId");
var customer = context.GetParameter<Customer>("customer");

// Or try get
if (context.TryGetParameter<string>("email", out var email))
{
    // Use email
}
```

### Available via `context.Logger`

```csharp
context.Logger.LogInformation("Processing order {OrderId}", orderId);
context.Logger.LogError(ex, "Failed to send email");
```

---

## 📦 Storage (IPluginRepository)

You must implement `IPluginRepository` to persist plugins. Example implementations:

### In-Memory (Testing)
```csharp
public class InMemoryPluginRepository : IPluginRepository
{
    private readonly ConcurrentDictionary<string, PluginMetadata> _plugins = new();

    public Task<string> SaveAsync(PluginMetadata metadata, CancellationToken ct = default)
    {
        _plugins[metadata.Id] = metadata;
        return Task.FromResult(metadata.Id);
    }

    // ... implement other methods
}
```

### Entity Framework (Production)
```csharp
public class EFPluginRepository : IPluginRepository
{
    private readonly DbContext _context;

    public async Task<string> SaveAsync(PluginMetadata metadata, CancellationToken ct = default)
    {
        var entity = new PluginEntity
        {
            Id = metadata.Id,
            Name = metadata.Name,
            SourceCode = metadata.SourceCode,
            // ... map all properties
        };

        await _context.Plugins.AddAsync(entity, ct);
        await _context.SaveChangesAsync(ct);
        return entity.Id;
    }

    // ... implement other methods
}
```

---

## ⚙️ Dependency Injection Setup

```csharp
services.AddSingleton<PluginSecuritySettings>(new PluginSecuritySettings
{
    TimeoutSeconds = 30,
    MaxConcurrentExecutions = 10
});

services.AddSingleton<HazinaPluginCompiler>();
services.AddSingleton<PluginSandbox>();
services.AddSingleton<PluginManager>();

// Choose your repository implementation
services.AddSingleton<IPluginRepository, InMemoryPluginRepository>();
// OR: services.AddScoped<IPluginRepository, EFPluginRepository>();

// Register the three tools
services.AddSingleton<CreateDynamicToolTool>();
services.AddSingleton<ListDynamicToolsTool>();
services.AddSingleton<ExecuteDynamicToolTool>();
```

---

## 🎯 Use Cases

### 1. AI-Generated Business Logic
> "When a customer places 3 orders in one day, upgrade them to Gold tier"

AI generates plugin → compiles → executes automatically.

### 2. User-Extensible Platform
Power users can write custom plugins without deploying code.

### 3. A/B Testing
Create multiple plugin versions, route traffic, compare results.

### 4. Rapid Prototyping
Test new features as plugins before promoting to core codebase.

### 5. Multi-Tenant Customization
Each tenant gets custom plugins for their unique workflow.

---

## ⚠️ Limitations & Future Work

### Current Limitations
1. **No persistent storage included** - Must implement `IPluginRepository`
2. **Memory limits not enforced** - Only timeout protection active
3. **No file system/network restrictions** - Trust the code being executed
4. **Single-threaded compilation** - Concurrent requests queue up

### Future Enhancements
- [ ] Advanced security: AppDomain isolation, resource quotas
- [ ] Plugin versioning: Side-by-side execution of multiple versions
- [ ] Plugin dependencies: Plugins can reference other plugins
- [ ] UI for plugin management: Web-based editor
- [ ] Plugin testing framework: Unit test generated plugins
- [ ] Performance monitoring: Execution metrics, slow plugin alerts

---

## 📚 References

### Inspiration
- **Azure Functions** - C# scripting model
- **Unity** - Runtime C# compilation for game mods
- **Roslyn Scripting APIs** - Foundation for this system
- **CS-Script** - Mature C# scripting engine

### Documentation
- [Roslyn Scripting Documentation](https://github.com/dotnet/roslyn/wiki/Scripting-API-Samples)
- [AssemblyLoadContext](https://docs.microsoft.com/en-us/dotnet/core/dependency-loading/understanding-assemblyloadcontext)

---

## 🚀 Getting Started

1. **Implement `IPluginRepository`** for your storage backend
2. **Register services** in DI container
3. **Use the tools** to create/list/execute plugins
4. **Let AI generate** plugins for your users!

---

## 📝 License

Part of the Hazina framework. See main repository LICENSE.

---

**Built with ❤️ using Roslyn and .NET 9**
