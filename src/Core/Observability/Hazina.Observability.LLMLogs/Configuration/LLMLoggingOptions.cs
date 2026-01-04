namespace Hazina.Observability.LLMLogs.Configuration
{
    /// <summary>
    /// Configuration options for LLM logging.
    /// </summary>
    public class LLMLoggingOptions
    {
        /// <summary>
        /// Configuration section name in appsettings.json.
        /// </summary>
        public const string SectionName = "LLMLogging";

        /// <summary>
        /// Whether LLM logging is enabled.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Path to the SQLite database file.
        /// If relative, will be resolved from application base directory.
        /// </summary>
        public string DatabasePath { get; set; } = "llm-logs.db";

        /// <summary>
        /// How many days to keep log entries. Older entries will be deleted automatically.
        /// Set to 0 to keep logs indefinitely.
        /// </summary>
        public int RetentionDays { get; set; } = 30;

        /// <summary>
        /// Whether to log the full request messages (can be large).
        /// </summary>
        public bool LogRequestMessages { get; set; } = true;

        /// <summary>
        /// Whether to log the full response data (can be large).
        /// </summary>
        public bool LogResponseData { get; set; } = true;

        /// <summary>
        /// Whether to estimate and log costs.
        /// Requires cost estimation configuration per provider/model.
        /// </summary>
        public bool EstimateCosts { get; set; } = true;
    }
}
