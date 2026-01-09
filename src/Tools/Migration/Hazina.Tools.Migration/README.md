# Hazina Migration Tool

Automated data migration from file-based storage to SQLite database.

## Overview

The Hazina Migration Tool provides a safe, validated way to migrate your existing file-based Hazina projects to the new SQLite storage backend. It handles:

- **Metadata** (from `.metadata.json` files)
- **Embeddings** (from `embeddings.json`)
- **Chunks** (from `chunks.json`)
- **Text content** (from individual chunk files)

## Features

- ✅ **Progress reporting** - Real-time progress updates during migration
- ✅ **Validation** - Automatic integrity checks after migration
- ✅ **Dry-run mode** - Test migration without making changes
- ✅ **Error handling** - Continues on errors, reports all issues
- ✅ **Idempotent** - Safe to run multiple times (merges data)

## Usage

### Programmatic Usage

```csharp
using Hazina.Tools.Migration;
using Microsoft.Extensions.Logging;

// Create logger (optional)
var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = loggerFactory.CreateLogger<MigrationCommand>();

// Create migration command
var migrationCommand = new MigrationCommand(logger);

// Run migration
var success = await migrationCommand.MigrateFileToSqliteAsync(
    sourceFolder: @"C:\projects\my-hazina-project",
    sqliteDbPath: @"C:\projects\my-hazina-project\hazina.db",
    validateAfter: true,      // Validate after migration
    dryRun: false             // Set to true for validation-only
);

if (success)
{
    Console.WriteLine("Migration completed successfully!");
}
```

### CLI Usage (Integration Example)

```csharp
// In your CLI application
using Hazina.Tools.Migration;

class Program
{
    static async Task<int> Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: hazina-migrate <source-folder> <sqlite-db-path> [--dry-run] [--no-validate]");
            return 1;
        }

        var sourceFolder = args[0];
        var sqliteDbPath = args[1];
        var dryRun = args.Contains("--dry-run");
        var noValidate = args.Contains("--no-validate");

        var command = new MigrationCommand();
        var success = await command.MigrateFileToSqliteAsync(
            sourceFolder,
            sqliteDbPath,
            validateAfter: !noValidate,
            dryRun: dryRun
        );

        return success ? 0 : 1;
    }
}
```

## Migration Process

### Step 1: Dry Run (Recommended)

First, validate your source data without migrating:

```csharp
await migrationCommand.MigrateFileToSqliteAsync(
    sourceFolder: @"C:\projects\my-project",
    sqliteDbPath: @"C:\projects\my-project\hazina.db",
    validateAfter: false,
    dryRun: true  // ← Validation only
);
```

This will:
- Scan all source files
- Report how many documents will be migrated
- Check for any obvious issues
- **NOT modify anything**

### Step 2: Actual Migration

Once dry run succeeds, run the actual migration:

```csharp
await migrationCommand.MigrateFileToSqliteAsync(
    sourceFolder: @"C:\projects\my-project",
    sqliteDbPath: @"C:\projects\my-project\hazina.db",
    validateAfter: true,  // ← Validate after completion
    dryRun: false
);
```

This will:
1. Scan source files
2. Ask for confirmation
3. Migrate all data to SQLite
4. Validate migration integrity
5. Report results

### Step 3: Validate (Optional)

You can re-run validation at any time:

```csharp
await migrationCommand.ValidateMigrationAsync(
    sourceFolder: @"C:\projects\my-project",
    sqliteDbPath: @"C:\projects\my-project\hazina.db"
);
```

## File Structure

### Source (File-Based)

```
project-folder/
├── embeddings/
│   └── embeddings.json                # All vector embeddings
├── parts/
│   ├── chunks.json                    # Document → chunk mapping
│   ├── doc1.txt chunk 0               # Individual chunk files
│   ├── doc1.txt chunk 1
│   └── ...
├── metadata/
│   ├── doc1.txt.metadata.json         # Per-document metadata
│   ├── doc2.txt.metadata.json
│   └── ...
└── [source files]
```

### Destination (SQLite)

```
project-folder/
├── hazina.db                          # Single SQLite database
└── [source files]                     # Preserved (not modified)
```

**Database Tables**:
- `items` - Documents with checksums
- `metadata` - Key-value metadata
- `tags` - Document tags
- `chunks` - Document chunks
- `embeddings` - Vector embeddings (BLOB)
- `items_fts` - Full-text search index

## Progress Reporting

During migration, you'll see real-time progress:

```
═══════════════════════════════════════════════════════════════
  Hazina Data Migration Tool
  File-based Storage → SQLite Database
═══════════════════════════════════════════════════════════════

Source:      C:\projects\my-project
Destination: C:\projects\my-project\hazina.db
Mode:        MIGRATION

Proceed with migration? (y/n): y

Starting migration...

[14:32:15] Progress: 10% (150/1500)
[14:32:20] Progress: 20% (300/1500)
[14:32:25] Progress: 30% (450/1500)
...
[14:33:45] Progress: 100% (1500/1500)

─────────────────────────────────────────────────────────────
  Migration Results
─────────────────────────────────────────────────────────────
Status:     Completed Successfully
Duration:   90.2 seconds
Total:      1500 documents
Successful: 1498
Failed:     2
Skipped:    0
Warnings:   5
─────────────────────────────────────────────────────────────
```

## Validation Results

After migration, validation compares source and destination:

```
─────────────────────────────────────────────────────────────
  Validation Results
─────────────────────────────────────────────────────────────
Validation: PASSED, 1498/1500 documents validated,
2 missing, 0 errors, 3 warnings

Warnings (3):
  - Document doc123: Tag count mismatch (5 vs 4)
  - Document doc456: Chunk count mismatch (10 vs 9)
  - Document doc789: Embedding missing in SQLite
─────────────────────────────────────────────────────────────
```

## Error Handling

The migration tool is designed to be resilient:

- **Continues on errors**: If one document fails, others continue
- **Detailed error reporting**: All errors logged with document IDs
- **Partial migration**: Successfully migrated documents remain in database
- **Idempotent**: Can re-run to migrate failed documents

Example error output:

```
Errors: 2
  - Failed to migrate doc123: Invalid JSON in metadata file
  - Failed to migrate doc456: Embedding data corrupted
```

## Performance

Typical migration speeds:

| Dataset Size | Duration | Speed |
|--------------|----------|-------|
| 100 docs | ~5 seconds | ~20 docs/sec |
| 1,000 docs | ~45 seconds | ~22 docs/sec |
| 10,000 docs | ~7 minutes | ~24 docs/sec |
| 100,000 docs | ~70 minutes | ~24 docs/sec |

**Factors affecting speed**:
- Embedding size (dimension)
- Number of chunks per document
- Disk I/O speed
- Validation enabled/disabled

## Troubleshooting

### Migration Fails with "Database is locked"

**Problem**: SQLite database is open in another application.

**Solution**: Close all applications accessing the database, or use `Cache=Shared` in connection string.

### Some Documents Show as "Missing" in Validation

**Problem**: Migration completed but validation reports missing documents.

**Solution**: Re-run migration - it will only migrate the missing documents (idempotent).

### "Invalid JSON" Errors

**Problem**: Source metadata files are corrupted or in old format.

**Solution**:
1. Check the specific files mentioned in errors
2. Fix or remove corrupted files
3. Re-run migration

### Migration is Very Slow

**Problem**: Large embeddings or many chunks per document.

**Solution**:
- Disable validation during migration (run separately after)
- Use SSD instead of HDD
- Consider migrating in batches

## API Reference

### FileToSqliteMigrationEngine

Main migration engine.

```csharp
public class FileToSqliteMigrationEngine
{
    public FileToSqliteMigrationEngine(
        string sourceFolder,
        string sqliteConnectionString,
        ILogger? logger = null);

    public event EventHandler<MigrationProgressEventArgs>? ProgressChanged;

    public Task<MigrationProgress> MigrateAsync(
        bool validateOnly = false,
        CancellationToken cancellationToken = default);
}
```

### MigrationValidator

Validates migrated data.

```csharp
public class MigrationValidator
{
    public MigrationValidator(
        string sourceFolder,
        string sqliteConnectionString,
        ILogger? logger = null);

    public Task<ValidationResult> ValidateAsync(
        CancellationToken cancellationToken = default);
}
```

### MigrationCommand

High-level CLI-friendly interface.

```csharp
public class MigrationCommand
{
    public Task<bool> MigrateFileToSqliteAsync(
        string sourceFolder,
        string sqliteDbPath,
        bool validateAfter = true,
        bool dryRun = false);

    public Task<bool> ValidateMigrationAsync(
        string sourceFolder,
        string sqliteDbPath);
}
```

## Safety Considerations

1. **Backup First**: Always backup your source folder before migration
2. **Dry Run**: Run with `dryRun: true` first to validate
3. **Source Preserved**: Migration never modifies source files
4. **Idempotent**: Safe to run multiple times (merges data)
5. **Validation**: Always validate after migration

## Next Steps

After successful migration:

1. **Update Configuration**: Switch to SQLite backend in your code
2. **Test Application**: Verify everything works with new backend
3. **Keep Backups**: Retain file-based backup until confident
4. **Monitor Performance**: Compare query performance with file-based
5. **Archive Old Files**: Once stable, archive old storage files

## Support

- **Documentation**: See `docs/SQLITE_QUICKSTART.md`
- **Issues**: Report at [GitHub Issues](https://github.com/martiendejong/Hazina/issues)
- **Analysis**: See `docs/architecture-storage-analysis.md`

---

**Version**: 2.0.0+
**Last Updated**: 2026-01-06
