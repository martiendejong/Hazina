using System;
using System.Threading.Tasks;

namespace Hazina.Tools.Data
{
    /// <summary>
    /// Static utility for tracking token usage across the application
    /// </summary>
    public static class TokenUsageTracker
    {
        private static TokenUsageRepository? _repository;
        private static bool _isInitialized = false;

        /// <summary>
        /// Initialize the tracker with a repository instance
        /// </summary>
        public static void Initialize(TokenUsageRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _isInitialized = true;
        }

        /// <summary>
        /// Tracks token usage for a project. Safe to call even if not initialized.
        /// </summary>
        public static void Track(string projectId, int promptTokens, int completionTokens, string model = "gpt-4")
        {
            if (!_isInitialized || _repository == null)
                return; // Gracefully handle uninitialized state

            try
            {
                _repository.RecordUsage(projectId, promptTokens, completionTokens, model);
            }
            catch (Exception ex)
            {
                // Log but don't crash - token tracking is non-critical
                Console.WriteLine($"Error tracking token usage: {ex.Message}");
            }
        }

        /// <summary>
        /// Tracks token usage asynchronously
        /// </summary>
        public static Task TrackAsync(string projectId, int promptTokens, int completionTokens, string model = "gpt-4")
        {
            return Task.Run(() => Track(projectId, promptTokens, completionTokens, model));
        }
    }
}
