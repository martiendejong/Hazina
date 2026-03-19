# Hazina Configuration System

This document describes the improved configuration system for Hazina Agent Factory.

## Key Improvements

### 1. Separation of Parsing from Validation (869cabf3t)

The new configuration system separates parsing from object construction and returns typed validation diagnostics instead of throwing exceptions.

**Old approach:**
```csharp
// Throws exception on failure
var stores = HazinaStoreConfigParser.Parse(input);
```

**New approach:**
```csharp
var parser = new HazinaStoreConfigParserV2();
var result = parser.Parse(input);

if (result.IsValid)
{
    // Use result.Value safely
    foreach (var store in result.Value)
    {
        // ...
    }
}
else
{
    // Handle errors gracefully
    Console.WriteLine(result.FormatDiagnostics());
}
```

### 2. Dependency Injection (869cabf3p)

File system and time operations are now abstracted for testability and portability.

```csharp
// Production usage with physical file system
var parser = new HazinaStoreConfigParserV2(new PhysicalFileSystem());

// Test usage with mock file system
var mockFileSystem = new MockFileSystem();
var parser = new HazinaStoreConfigParserV2(mockFileSystem);
```

**Available abstractions:**
- `IFileSystem` - File operations (read, write, exists, etc.)
- `IClock` - Time operations (UtcNow, Now, Today)
- `PhysicalFileSystem` - Default implementation using System.IO
- `SystemClock` - Default implementation using DateTime
- `FixedClock` - Test implementation with fixed time

### 3. JSON Schema Generation (869cabf3f)

JSON schemas are generated for all configuration types to enable IDE autocomplete and validation.

```csharp
// Generate schemas
ConfigSchemaGenerator.SaveAllSchemas("./schemas");

// Files created:
// - store-config.schema.json
// - agent-config.schema.json
// - flow-config.schema.json
```

**IDE integration:**
Add to your JSON config files:
```json
{
  "$schema": "./schemas/store-config.schema.json",
  "Name": "MyStore",
  "Path": "./data"
}
```

### 4. Safety Defaults (869cabf3d)

Stores now default to read-only mode and require explicit write enablement.

```csharp
var store = new StoreConfig
{
    Name = "MyStore",
    Path = "./data",
    // IsReadOnly = true by default
    // ExplicitWriteEnabled = false by default
};

// To enable writes, you must explicitly set both:
var writeableStore = new StoreConfig
{
    Name = "MyStore",
    Path = "./data",
    IsReadOnly = false,
    ExplicitWriteEnabled = true  // Required!
};
```

**Additional safety features:**
- `MaxFileSizeBytes` - Default 10MB limit
- `AllowedExtensions` - Whitelist file types
- `FollowSymlinks` - Default false (security)
- `MaxDirectoryDepth` - Default 10 (prevents infinite loops)

## Configuration Validation

### StoreConfig Validation

```csharp
var validator = new StoreConfigValidator(storeConfig);
var result = validator.Validate();

if (!result.IsValid)
{
    foreach (var error in result.Errors)
    {
        Console.WriteLine($"[{error.Code}] {error.Field}: {error.Message}");
    }
}
```

**Validation checks:**
- Required fields (Name, Path)
- Path format and existence
- File filter validity
- Safety settings consistency
- Extension format
- Size limits

### AgentConfig Validation

```csharp
var validator = new AgentConfigValidator(
    agentConfig,
    availableStores: storeNames,
    availableFunctions: functionNames
);
var result = validator.Validate();
```

**Validation checks:**
- Required fields (Name, Prompt)
- Store references exist
- Function references exist
- No circular dependencies
- No self-references
- Prompt quality checks

### Batch Validation

```csharp
// Validate all stores at once
var storeResult = StoreConfigValidator.ValidateAll(stores);

// Validate all agents with store/function context
var agentResult = AgentConfigValidator.ValidateAll(
    agents,
    availableStores: stores.Select(s => s.Name).ToList(),
    availableFunctions: toolRegistry.GetAllTools()
);
```

## Diagnostic Severity Levels

- **Error** - Prevents configuration from being used
- **Warning** - Indicates potential issues but doesn't block usage
- **Info** - Informational messages about the configuration

## Diagnostic Codes

All diagnostics include a code for programmatic handling:

- `STORE001-STORE021` - Store configuration issues
- `AGENT001-AGENT018` - Agent configuration issues
- `PARSE001-PARSE008` - Parsing issues
- `FILE001-FILE003` - File I/O issues

## Migration Guide

### From HazinaStoreConfigParser to HazinaStoreConfigParserV2

**Before:**
```csharp
try
{
    var stores = HazinaStoreConfigParser.Parse(input);
    // Use stores
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
    // Use stores
}
else
{
    Console.WriteLine(result.FormatDiagnostics());
}
```

### Adding Safety Settings to Existing Stores

If you have existing store configs, they will automatically get safety defaults:

```
Name: MyStore
Path: ./data
FileFilters: *.cs,*.txt
# New fields with defaults:
# IsReadOnly: true
# ExplicitWriteEnabled: false
# MaxFileSizeBytes: 10485760
# FollowSymlinks: false
# MaxDirectoryDepth: 10
```

To enable writes, add:
```
IsReadOnly: false
ExplicitWriteEnabled: true
```

## Examples

### Safe Read-Only Store

```csharp
var store = new StoreConfig
{
    Name = "Documentation",
    Description = "Project documentation files",
    Path = "./docs",
    FileFilters = new[] { "*.md", "*.txt" },
    ExcludePattern = new[] { "node_modules", ".git" },
    IsReadOnly = true,  // Default
    MaxFileSizeBytes = 5 * 1024 * 1024,  // 5MB
    AllowedExtensions = new[] { ".md", ".txt" }
};
```

### Writeable Store with Restrictions

```csharp
var store = new StoreConfig
{
    Name = "CodeGeneration",
    Description = "Generated code output",
    Path = "./output",
    FileFilters = new[] { "*.cs", "*.ts" },
    IsReadOnly = false,
    ExplicitWriteEnabled = true,  // Required!
    MaxFileSizeBytes = 1 * 1024 * 1024,  // 1MB per file
    AllowedExtensions = new[] { ".cs", ".ts", ".json" },
    ExcludePattern = new[] { "bin", "obj" }
};
```

### Complete Validation Workflow

```csharp
// Parse configuration
var parser = new HazinaStoreConfigParserV2();
var parseResult = parser.LoadFromFile("./config/stores.hazina");

if (!parseResult.IsValid)
{
    Console.WriteLine("Configuration errors:");
    Console.WriteLine(parseResult.FormatDiagnostics());
    return;
}

// Use validated configuration
var stores = parseResult.Value;

// Check warnings
if (parseResult.Warnings.Any())
{
    Console.WriteLine("Configuration warnings:");
    foreach (var warning in parseResult.Warnings)
    {
        Console.WriteLine($"  {warning}");
    }
}

// Proceed with validated stores
foreach (var store in stores)
{
    // Safe to use
}
```

## Best Practices

1. **Always validate configurations** before using them
2. **Use read-only stores by default** - only enable writes when necessary
3. **Set appropriate file size limits** to prevent memory issues
4. **Use AllowedExtensions** for additional safety
5. **Exclude common directories** (node_modules, .git, bin, obj)
6. **Review warnings** - they often indicate configuration issues
7. **Use JSON schemas** in your IDE for autocomplete
8. **Test with mock abstractions** (IFileSystem, IClock)

## Testing

```csharp
[Fact]
public void TestStoreConfigValidation()
{
    var mockFileSystem = new MockFileSystem();
    var parser = new HazinaStoreConfigParserV2(mockFileSystem);

    // Setup test files
    mockFileSystem.AddFile("/test/config.hazina", "Name: TestStore\nPath: /data");

    // Parse and validate
    var result = parser.LoadFromFile("/test/config.hazina");

    Assert.True(result.IsValid);
    Assert.Single(result.Value);
    Assert.Equal("TestStore", result.Value[0].Name);
}
```

## See Also

- [StoreConfig API Documentation](./StoreConfig.cs)
- [AgentConfig API Documentation](./AgentConfig.cs)
- [Validation System](./Validation/README.md)
- [Schema Generation](./Schema/README.md)
