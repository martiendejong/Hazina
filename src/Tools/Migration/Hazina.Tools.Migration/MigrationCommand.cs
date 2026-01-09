using Microsoft.Extensions.Logging;
using HazinaStore.Models;

namespace Hazina.Tools.Migration;

/// <summary>
/// Command-line interface for running migrations.
/// Provides methods to migrate, validate, and manage data migration operations.
/// </summary>
public class MigrationCommand
{
    private readonly ILogger? _logger;

    public MigrationCommand(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Migrates data from file-based storage to SQLite.
    /// </summary>
    /// <param name="sourceFolder">Path to file-based storage folder</param>
    /// <param name="sqliteDbPath">Path to SQLite database file (will be created if doesn't exist)</param>
    /// <param name="validateAfter">Validate migration after completion</param>
    /// <param name="dryRun">Run validation only without actual migration</param>
    /// <returns>True if migration succeeded</returns>
    public async Task<bool> MigrateFileToSqliteAsync(
        string sourceFolder,
        string sqliteDbPath,
        bool validateAfter = true,
        bool dryRun = false)
    {
        try
        {
            Console.WriteLine("═══════════════════════════════════════════════════════════════");
            Console.WriteLine("  Hazina Data Migration Tool");
            Console.WriteLine("  File-based Storage → SQLite Database");
            Console.WriteLine("═══════════════════════════════════════════════════════════════");
            Console.WriteLine();
            Console.WriteLine($"Source:      {Path.GetFullPath(sourceFolder)}");
            Console.WriteLine($"Destination: {Path.GetFullPath(sqliteDbPath)}");
            Console.WriteLine($"Mode:        {(dryRun ? "DRY RUN (validation only)" : "MIGRATION")}");
            Console.WriteLine();

            if (!Directory.Exists(sourceFolder))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ Error: Source folder not found: {sourceFolder}");
                Console.ResetColor();
                return false;
            }

            // Confirm before proceeding (unless dry run)
            if (!dryRun)
            {
                if (File.Exists(sqliteDbPath))
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("⚠ Warning: SQLite database already exists. Data will be merged.");
                    Console.ResetColor();
                }

                Console.Write("Proceed with migration? (y/n): ");
                var response = Console.ReadLine()?.Trim().ToLower();
                if (response != "y" && response != "yes")
                {
                    Console.WriteLine("Migration cancelled.");
                    return false;
                }
                Console.WriteLine();
            }

            // Build connection string
            var connectionString = new SqliteSettings
            {
                DatabasePath = sqliteDbPath
            }.GetConnectionString();

            // Run migration
            var engine = new FileToSqliteMigrationEngine(sourceFolder, connectionString, _logger);

            // Progress reporting
            var lastPercentReported = -1;
            engine.ProgressChanged += (sender, e) =>
            {
                var percent = (int)e.Progress.PercentComplete;
                if (percent != lastPercentReported && percent % 10 == 0)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Progress: {percent}% ({e.Progress.ProcessedItems}/{e.Progress.TotalItems})");
                    lastPercentReported = percent;
                }

                if (!string.IsNullOrEmpty(e.Message) && e.Progress.ProcessedItems % 100 == 0)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {e.Message}");
                }
            };

            Console.WriteLine("Starting migration...");
            Console.WriteLine();

            var progress = await engine.MigrateAsync(validateOnly: dryRun);

            Console.WriteLine();
            Console.WriteLine("─────────────────────────────────────────────────────────────");
            Console.WriteLine("  Migration Results");
            Console.WriteLine("─────────────────────────────────────────────────────────────");
            Console.WriteLine($"Status:     {progress.Status}");
            Console.WriteLine($"Duration:   {progress.Duration.TotalSeconds:F1} seconds");
            Console.WriteLine($"Total:      {progress.TotalItems} documents");
            Console.WriteLine($"Successful: {progress.SuccessfulItems}");
            Console.WriteLine($"Failed:     {progress.FailedItems}");
            Console.WriteLine($"Skipped:    {progress.SkippedItems}");

            if (progress.Warnings.Count > 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Warnings:   {progress.Warnings.Count}");
                Console.ResetColor();
            }

            if (progress.Errors.Count > 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Errors:     {progress.Errors.Count}");
                foreach (var error in progress.Errors.Take(5))
                {
                    Console.WriteLine($"  - {error}");
                }
                if (progress.Errors.Count > 5)
                {
                    Console.WriteLine($"  ... and {progress.Errors.Count - 5} more");
                }
                Console.ResetColor();
            }

            Console.WriteLine("─────────────────────────────────────────────────────────────");

            // Run validation if requested
            if (validateAfter && !dryRun && progress.FailedItems == 0)
            {
                Console.WriteLine();
                Console.WriteLine("Running validation...");

                var validator = new MigrationValidator(sourceFolder, connectionString, _logger);
                var validationResult = await validator.ValidateAsync();

                Console.WriteLine();
                Console.WriteLine("─────────────────────────────────────────────────────────────");
                Console.WriteLine("  Validation Results");
                Console.WriteLine("─────────────────────────────────────────────────────────────");
                Console.WriteLine(validationResult.ToString());

                if (validationResult.Warnings.Count > 0)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"\nWarnings ({validationResult.Warnings.Count}):");
                    foreach (var warning in validationResult.Warnings.Take(5))
                    {
                        Console.WriteLine($"  - {warning}");
                    }
                    if (validationResult.Warnings.Count > 5)
                    {
                        Console.WriteLine($"  ... and {validationResult.Warnings.Count - 5} more");
                    }
                    Console.ResetColor();
                }

                if (validationResult.Errors.Count > 0)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"\nErrors ({validationResult.Errors.Count}):");
                    foreach (var error in validationResult.Errors.Take(5))
                    {
                        Console.WriteLine($"  - {error}");
                    }
                    if (validationResult.Errors.Count > 5)
                    {
                        Console.WriteLine($"  ... and {validationResult.Errors.Count - 5} more");
                    }
                    Console.ResetColor();
                }

                Console.WriteLine("─────────────────────────────────────────────────────────────");

                return validationResult.IsValid;
            }

            return progress.FailedItems == 0;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n✗ Migration failed: {ex.Message}");
            Console.ResetColor();
            _logger?.LogError(ex, "Migration failed");
            return false;
        }
    }

    /// <summary>
    /// Validates an existing SQLite database against file-based source.
    /// </summary>
    public async Task<bool> ValidateMigrationAsync(string sourceFolder, string sqliteDbPath)
    {
        try
        {
            Console.WriteLine("═══════════════════════════════════════════════════════════════");
            Console.WriteLine("  Hazina Migration Validation Tool");
            Console.WriteLine("═══════════════════════════════════════════════════════════════");
            Console.WriteLine();
            Console.WriteLine($"Source:      {Path.GetFullPath(sourceFolder)}");
            Console.WriteLine($"Database:    {Path.GetFullPath(sqliteDbPath)}");
            Console.WriteLine();

            var connectionString = new SqliteSettings
            {
                DatabasePath = sqliteDbPath
            }.GetConnectionString();

            var validator = new MigrationValidator(sourceFolder, connectionString, _logger);
            var result = await validator.ValidateAsync();

            Console.WriteLine(result.ToString());
            return result.IsValid;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n✗ Validation failed: {ex.Message}");
            Console.ResetColor();
            return false;
        }
    }
}
