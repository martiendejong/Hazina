namespace HazinaStore.Models
{
    /// <summary>
    /// Configuration settings for Supabase integration
    /// </summary>
    public class SupabaseSettings
    {
        /// <summary>
        /// Supabase project URL (e.g., https://your-project.supabase.co)
        /// </summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// Supabase anonymous key (public key for client-side use)
        /// </summary>
        public string AnonKey { get; set; } = string.Empty;

        /// <summary>
        /// Supabase service role key (server-side key with elevated permissions)
        /// Use this for administrative operations
        /// </summary>
        public string ServiceRoleKey { get; set; } = string.Empty;

        /// <summary>
        /// Whether to use Supabase as the primary database (defaults to false for optional integration)
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Database connection string (for direct PostgreSQL access via Npgsql)
        /// Format: Host=db.your-project.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=your-password
        /// </summary>
        public string ConnectionString { get; set; } = string.Empty;

        /// <summary>
        /// Whether to use Supabase Storage for file uploads (defaults to false)
        /// </summary>
        public bool UseSupabaseStorage { get; set; } = false;

        /// <summary>
        /// Default bucket name for Supabase Storage
        /// </summary>
        public string DefaultBucket { get; set; } = "hazina-files";

        /// <summary>
        /// Whether to use Supabase Auth for authentication (defaults to false)
        /// </summary>
        public bool UseSupabaseAuth { get; set; } = false;

        /// <summary>
        /// Validates that required settings are configured
        /// </summary>
        public bool IsValid()
        {
            if (!Enabled) return true; // If not enabled, no validation needed

            return !string.IsNullOrWhiteSpace(Url) &&
                   !string.IsNullOrWhiteSpace(AnonKey);
        }

        /// <summary>
        /// Gets the connection string with fallback to environment variable
        /// </summary>
        public string GetConnectionString()
        {
            if (!string.IsNullOrWhiteSpace(ConnectionString))
                return ConnectionString;

            // Fallback to environment variable
            var envConnString = Environment.GetEnvironmentVariable("SUPABASE_CONNECTION_STRING")
                             ?? Environment.GetEnvironmentVariable("SUPABASE_DB_URL");

            return envConnString ?? string.Empty;
        }
    }
}
