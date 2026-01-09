using System.ComponentModel.DataAnnotations;

namespace HazinaStore.Models
{
    /// <summary>
    /// Configuration settings for SQLite storage backend.
    /// SQLite provides single-file database storage ideal for local development and embedded scenarios.
    /// </summary>
    public class SqliteSettings
    {
        /// <summary>
        /// Enable SQLite storage backend.
        /// When true, SQLite will be used instead of file-based storage.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Path to the SQLite database file.
        /// If relative, resolved from project folder.
        /// Default: "./hazina.db" in project folder.
        /// </summary>
        public string? DatabasePath { get; set; }

        /// <summary>
        /// Connection string for SQLite.
        /// If not provided, auto-generated from DatabasePath.
        /// Example: "Data Source=C:\path\to\hazina.db;Mode=ReadWriteCreate"
        /// </summary>
        public string? ConnectionString { get; set; }

        /// <summary>
        /// Whether embeddings are optional (metadata-first mode).
        /// When true, the system works without generating/storing embeddings.
        /// Useful for cost savings or pure keyword/metadata search.
        /// </summary>
        public bool EmbeddingsOptional { get; set; } = false;

        /// <summary>
        /// Gets the connection string, using provided value or auto-generating from database path.
        /// </summary>
        public string GetConnectionString(string? projectFolder = null)
        {
            if (!string.IsNullOrWhiteSpace(ConnectionString))
            {
                return ConnectionString;
            }

            // Auto-generate from database path
            var dbPath = DatabasePath ?? "hazina.db";

            // Make absolute if relative
            if (!Path.IsPathRooted(dbPath) && !string.IsNullOrEmpty(projectFolder))
            {
                dbPath = Path.Combine(projectFolder, dbPath);
            }

            // Ensure directory exists
            var directory = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            return $"Data Source={dbPath};Mode=ReadWriteCreate";
        }

        /// <summary>
        /// Validates the settings and throws if invalid.
        /// </summary>
        public void Validate()
        {
            if (Enabled && string.IsNullOrWhiteSpace(DatabasePath) && string.IsNullOrWhiteSpace(ConnectionString))
            {
                throw new ValidationException("SQLite is enabled but neither DatabasePath nor ConnectionString is set.");
            }
        }
    }
}
