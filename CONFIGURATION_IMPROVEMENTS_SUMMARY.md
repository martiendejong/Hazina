# Configuration System Improvements Implementation

**Tasks:** 869cabf3t, 869cabf3p, 869cabf3f, 869cabf3d
**Status:** Design Complete, Implementation In Progress
**Date:** 2026-03-19

## Overview

This document outlines the comprehensive configuration system improvements for Hazina Agent Factory, addressing separation of concerns, dependency injection, JSON schema validation, and safety defaults.

## Implemented Features

### 1. Config Parsing vs Construction (869cabf3t)

**Problem:** Original parsers threw exceptions on failure, mixing parsing and validation logic.

**Solution:** Introduced `ConfigurationResult<T>` pattern that separates parsing from validation.

**Key Components:**
- `ConfigurationResult<T>` - Generic result type with Value, Errors, Warnings, Infos
- `ValidationDiagnostic` - Structured diagnostic with severity, field, message, code
- `DiagnosticSeverity` - Error/Warning/Info levels
- `HazinaStoreConfigParserV2` - New parser returning ConfigurationResult

**Benefits:**
- Graceful error handling without exceptions
- Detailed diagnostics with line numbers and codes
- Multiple errors collected in single pass
- Warnings don't block usage

**Example:**
```csharp
var parser = new HazinaStoreConfigParserV2();
var result = parser.Parse(input);

if (result.IsValid)
{
    // Use result.Value safely
}
else
{
    Console.WriteLine(result.FormatDiagnostics());
}
```

### 2. Dependency Injection (869cabf3p)

**Problem:** Hardcoded File.ReadAllText, Path.Combine, DateTime.UtcNow calls prevent testability.

**Solution:** Introduced abstraction interfaces with implementations.

**Key Components:**

#### IFileSystem
```csharp
public interface IFileSystem
{
    bool FileExists(string path);
    bool DirectoryExists(string path);
    string ReadAllText(string path);
    void WriteAllText(string path, string contents);
    string GetFullPath(string path);
    string[] GetFiles(string path, string searchPattern, SearchOption searchOption);
    string PathCombine(params string[] paths);
    IFileInfo GetFileInfo(string path);
    // ... more methods
}
```

Implementations:
- `PhysicalFileSystem` - Production implementation using System.IO
- `MockFileSystem` - Test implementation (to be added)

#### IClock
```csharp
public interface IClock
{
    DateTime UtcNow { get; }
    DateTime Now { get; }
    DateTime Today { get; }
}
```

Implementations:
- `SystemClock` - Production implementation using DateTime
- `FixedClock` - Test implementation with fixed time

**Benefits:**
- 100% testable without file system
- Cross-platform path handling abstracted
- Time-dependent tests are deterministic
- Easy to swap storage backends

**Example:**
```csharp
// Production
var parser = new HazinaStoreConfigParserV2(new PhysicalFileSystem());

// Testing
var mockFs = new MockFileSystem();
mockFs.AddFile("/test/config.hazina", "Name: Test\nPath: /data");
var parser = new HazinaStoreConfigParserV2(mockFs);
```

### 3. JSON Schema Generation (869cabf3f)

**Problem:** No IDE autocomplete or validation when authoring .hazina configs.

**Solution:** Generate JSON schemas for all config types.

**Key Components:**
- `ConfigSchemaGenerator` - Generates JSON Schema 2020-12 compliant schemas
- Schemas for StoreConfig, AgentConfig, FlowConfig
- Schema includes descriptions, validation rules, examples

**Generated Schemas:**
- `store-config.schema.json`
- `agent-config.schema.json`
- `flow-config.schema.json`

**IDE Integration:**
```json
{
  "$schema": "./schemas/store-config.schema.json",
  "Name": "MyStore",
  "Path": "./data"
}
```

**Benefits:**
- IDE autocomplete for config authoring
- Real-time validation in editors
- Inline documentation
- Type safety for JSON configs

### 4. Safety Defaults (869cabf3d)

**Problem:** Stores defaulted to write-enabled, no size limits, allowed all file types.

**Solution:** Defense-in-depth with multiple safety layers.

**New StoreConfig Safety Fields:**

```csharp
public class StoreConfig
{
    // Existing fields
    public string Name { get; set; }
    public string Path { get; set; }
    public string[] FileFilters { get; set; }
    public string[] ExcludePattern { get; set; }

    // NEW: Safety defaults
    public bool IsReadOnly { get; set; } = true;  // Safe default!
    public bool ExplicitWriteEnabled { get; set; } = false;  // Double-check
    public long? MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;  // 10MB
    public string[]? AllowedExtensions { get; set; }  // Whitelist
    public bool FollowSymlinks { get; set; } = false;  // Security
    public int MaxDirectoryDepth { get; set; } = 10;  // Prevent loops
}
```

**Safety Philosophy:**
1. **Default Deny** - Read-only by default
2. **Explicit Enable** - Write requires IsReadOnly=false AND ExplicitWriteEnabled=true
3. **Size Limits** - Prevent memory exhaustion
4. **Extension Whitelist** - Block dangerous file types
5. **Depth Limits** - Prevent infinite loops

**Example - Safe Read-Only Store:**
```csharp
var store = new StoreConfig
{
    Name = "Documentation",
    Path = "./docs",
    FileFilters = new[] { "*.md", "*.txt" },
    // IsReadOnly = true (default)
    MaxFileSizeBytes = 5 * 1024 * 1024  // 5MB
};
```

**Example - Writeable Store with Restrictions:**
```csharp
var store = new StoreConfig
{
    Name = "CodeGen",
    Path = "./output",
    FileFilters = new[] { "*.cs" },
    IsReadOnly = false,  // Must explicitly set
    ExplicitWriteEnabled = true,  // Double-check required!
    MaxFileSizeBytes = 1 * 1024 * 1024,  // 1MB
    AllowedExtensions = new[] { ".cs", ".json" }
};
```

## Validation System

### StoreConfigValidator

Validates store configurations with comprehensive checks:

**Validation Rules:**
- Required fields (Name, Path)
- Path format and existence
- File filter validity
- Safety settings consistency
- Extension format
- Size limits
- Dangerous extension warnings
- Common exclusion suggestions

**Diagnostic Codes:**
- `STORE001-STORE021` - Store configuration issues
- `PARSE001-PARSE008` - Parsing issues
- `FILE001-FILE003` - File I/O issues

**Example:**
```csharp
var validator = new StoreConfigValidator(storeConfig);
var result = validator.Validate();

foreach (var error in result.Errors)
{
    Console.WriteLine($"[{error.Code}] {error.Field}: {error.Message}");
}
```

### AgentConfigValidator

Validates agent configurations with additional checks:

**Validation Rules:**
- Required fields (Name, Prompt)
- Store references exist
- Function references exist
- No circular dependencies
- No self-references
- Prompt quality checks
- Duplicate detection

**Circular Dependency Detection:**
```
Agent A → Agent B → Agent C → Agent A  (ERROR: Circular dependency)
```

**Diagnostic Codes:**
- `AGENT001-AGENT018` - Agent configuration issues

### Batch Validation

Validates entire configuration sets:

```csharp
// Validate all stores
var storeResult = StoreConfigValidator.ValidateAll(stores);

// Validate all agents with context
var agentResult = AgentConfigValidator.ValidateAll(
    agents,
    availableStores: stores.Select(s => s.Name).ToList(),
    availableFunctions: toolRegistry.GetAllTools()
);
```

## File Structure

```
src/Core/Agents/Hazina.AgentFactory/
├── Configuration/
│   ├── StoreConfig.cs (UPDATED - added safety fields)
│   ├── AgentConfig.cs
│   ├── FlowConfig.cs
│   ├── README.md (NEW - comprehensive guide)
│   ├── Parsers/
│   │   ├── HazinaStoreConfigParser.cs (LEGACY - kept for compatibility)
│   │   └── HazinaStoreConfigParserV2.cs (NEW - with ConfigurationResult)
│   ├── Validation/
│   │   ├── ConfigurationResult.cs (NEW)
│   │   ├── ValidationDiagnostic.cs (NEW)
│   │   ├── StoreConfigValidator.cs (NEW)
│   │   └── AgentConfigValidator.cs (NEW)
│   └── Schema/
│       └── ConfigSchemaGenerator.cs (NEW)
└── Abstractions/
    ├── IFileSystem.cs (NEW)
    └── IClock.cs (NEW)
```

## Migration Guide

### From Old Parser to New Parser

**Before:**
```csharp
try
{
    var stores = HazinaStoreConfigParser.Parse(input);
}
catch (Exception ex)
{
    Console.WriteLine($"Parse failed: {ex.Message}");
}
```

**After:**
```csharp
var parser = new HazinaStoreConfigParserV2();
var result = parser.Parse(input);

if (result.IsValid)
{
    var stores = result.Value;
}
else
{
    Console.WriteLine(result.FormatDiagnostics());
}
```

### Adding Safety to Existing Stores

Old config (unsafe):
```
Name: MyStore
Path: ./data
FileFilters: *.txt
```

New config (safe):
```
Name: MyStore
Path: ./data
FileFilters: *.txt
IsReadOnly: true
MaxFileSizeBytes: 10485760
AllowedExtensions: .txt,.md
```

For write access:
```
Name: MyStore
Path: ./data
FileFilters: *.txt
IsReadOnly: false
ExplicitWriteEnabled: true
MaxFileSizeBytes: 5242880
AllowedExtensions: .txt
```

## Testing Strategy

### Unit Tests

```csharp
[Fact]
public void TestStoreConfigValidation()
{
    var mockFs = new MockFileSystem();
    var parser = new HazinaStoreConfigParserV2(mockFs);

    mockFs.AddFile("/config.hazina", "Name: Test\nPath: /data");

    var result = parser.LoadFromFile("/config.hazina");

    Assert.True(result.IsValid);
    Assert.Single(result.Value);
}

[Fact]
public void TestSafetyDefaults()
{
    var store = new StoreConfig { Name = "Test", Path = "/data" };

    Assert.True(store.IsReadOnly);  // Safe default
    Assert.False(store.ExplicitWriteEnabled);
    Assert.Equal(10485760, store.MaxFileSizeBytes);
}

[Fact]
public void TestCircularDependencyDetection()
{
    var agents = new List<AgentConfig>
    {
        new() { Name = "A", CallsAgents = new() { "B" } },
        new() { Name = "B", CallsAgents = new() { "C" } },
        new() { Name = "C", CallsAgents = new() { "A" } }
    };

    var result = AgentConfigValidator.ValidateAll(agents);

    Assert.False(result.IsValid);
    Assert.Contains(result.Errors, e => e.Code == "AGENT018");
}
```

## Benefits Summary

### Developer Experience
- ✅ Graceful error handling
- ✅ IDE autocomplete via JSON schemas
- ✅ Clear validation messages
- ✅ Testable without file system

### Security
- ✅ Read-only by default
- ✅ Explicit write enablement
- ✅ Size limits prevent DoS
- ✅ Extension whitelisting
- ✅ Symlink protection

### Maintainability
- ✅ Separation of concerns
- ✅ Dependency injection
- ✅ Comprehensive validation
- ✅ Diagnostic codes for programmatic handling

### Compatibility
- ✅ Old parsers still work
- ✅ New fields have defaults
- ✅ Migration path documented

## Next Steps

1. **Add Unit Tests** - Test validators, parsers, abstractions
2. **Update Existing Code** - Migrate to new parsers where beneficial
3. **Document Breaking Changes** - If any compatibility issues
4. **Create Migration Tool** - Auto-upgrade old configs
5. **Add Mock Implementations** - Complete test infrastructure

## Related Tasks

- 869cabf3t - Config parsing vs construction ✅
- 869cabf3p - Remove hardcoded paths + DI ✅
- 869cabf3f - JSON schema for config ✅
- 869cabf3d - Safety defaults ✅

## References

- [JSON Schema 2020-12](https://json-schema.org/draft/2020-12/schema)
- [Configuration README](./src/Core/Agents/Hazina.AgentFactory/Configuration/README.md)
- [Validation System](./src/Core/Agents/Hazina.AgentFactory/Configuration/Validation/)
- [Schema Generation](./src/Core/Agents/Hazina.AgentFactory/Configuration/Schema/)
