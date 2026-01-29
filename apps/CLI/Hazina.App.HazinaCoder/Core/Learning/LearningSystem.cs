using Azure.AI.OpenAI;
using Qdrant.Client;

namespace Hazina.App.HazinaCoder.Core.Learning;

/// <summary>
/// Main orchestrator for persistent learning system (POC 1)
/// Captures experiences, stores them in vector DB, and retrieves relevant knowledge
/// </summary>
public class LearningSystem
{
    private readonly ExperienceCapture _capture;
    private readonly ExperienceStorage _storage;
    private readonly ExperienceRetrieval _retrieval;

    private bool _initialized = false;

    public LearningSystem(
        ExperienceCapture capture,
        ExperienceStorage storage,
        ExperienceRetrieval retrieval)
    {
        _capture = capture ?? throw new ArgumentNullException(nameof(capture));
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _retrieval = retrieval ?? throw new ArgumentNullException(nameof(retrieval));
    }

    /// <summary>
    /// Factory method to create and initialize learning system
    /// </summary>
    public static async Task<LearningSystem> CreateAsync(string qdrantHost, int qdrantPort, string openaiApiKey)
    {
        try
        {
            // Initialize Qdrant client
            var qdrantClient = new QdrantClient(host: qdrantHost, port: qdrantPort);

            // Initialize OpenAI client
            var openaiClient = new OpenAIClient(openaiApiKey);

            // Create components
            var storage = new ExperienceStorage(qdrantClient, openaiClient);
            var capture = new ExperienceCapture(storage);
            var retrieval = new ExperienceRetrieval(qdrantClient, openaiClient, storage);

            // Initialize collection
            await storage.InitializeCollectionAsync();

            var learningSystem = new LearningSystem(capture, storage, retrieval);
            learningSystem._initialized = true;

            return learningSystem;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Warning: Could not initialize learning system: {ex.Message}");
            Console.WriteLine("   Learning features will be disabled");

            // Return stub system that does nothing
            return CreateStubSystem();
        }
    }

    /// <summary>
    /// Create a stub system that gracefully does nothing (fallback)
    /// </summary>
    private static LearningSystem CreateStubSystem()
    {
        var stubQdrant = new QdrantClient("localhost", 6334);
        var stubOpenAI = new OpenAIClient("stub-key");
        var stubStorage = new ExperienceStorage(stubQdrant, stubOpenAI);
        var stubCapture = new ExperienceCapture(stubStorage);
        var stubRetrieval = new ExperienceRetrieval(stubQdrant, stubOpenAI, stubStorage);

        return new LearningSystem(stubCapture, stubStorage, stubRetrieval)
        {
            _initialized = false
        };
    }

    /// <summary>
    /// Process a user interaction and capture learning
    /// </summary>
    public async Task ProcessInteractionAsync(UserInteraction interaction)
    {
        if (!_initialized)
            return;

        try
        {
            await _capture.CaptureFromInteractionAsync(interaction);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Warning: Could not process interaction: {ex.Message}");
        }
    }

    /// <summary>
    /// Recall relevant past experiences for current context
    /// </summary>
    public async Task<List<Experience>> RecallRelevantExperiencesAsync(string context, int topK = 5)
    {
        if (!_initialized)
            return new List<Experience>();

        try
        {
            return await _retrieval.FindSimilarExperiencesAsync(context, topK);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Warning: Could not recall experiences: {ex.Message}");
            return new List<Experience>();
        }
    }

    /// <summary>
    /// Get user preferences relevant to current task
    /// </summary>
    public async Task<List<Experience>> GetUserPreferencesAsync(string context)
    {
        if (!_initialized)
            return new List<Experience>();

        try
        {
            return await _retrieval.GetUserPreferencesAsync(context, topK: 10);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Warning: Could not get preferences: {ex.Message}");
            return new List<Experience>();
        }
    }

    /// <summary>
    /// Apply learned preferences and generate explanation
    /// </summary>
    public async Task<string?> ApplyLearnedPreferencesAsync(string task)
    {
        if (!_initialized)
            return null;

        try
        {
            var preferences = await GetUserPreferencesAsync(task);

            if (preferences.Any())
            {
                var explanation = "💡 Based on your preferences:\n";
                foreach (var pref in preferences.Take(3))
                {
                    var timeAgo = GetTimeAgoString(pref.Timestamp);
                    explanation += $"   • {pref.Description} (learned {timeAgo})\n";
                }

                return explanation;
            }

            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Warning: Could not apply preferences: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Find similar solutions to a problem
    /// </summary>
    public async Task<List<Experience>> FindSimilarSolutionsAsync(string problemDescription)
    {
        if (!_initialized)
            return new List<Experience>();

        try
        {
            return await _retrieval.FindSimilarPatternsAsync(problemDescription, topK: 5);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Warning: Could not find similar solutions: {ex.Message}");
            return new List<Experience>();
        }
    }

    /// <summary>
    /// Find similar error resolutions
    /// </summary>
    public async Task<List<Experience>> FindSimilarErrorResolutionsAsync(string errorDescription)
    {
        if (!_initialized)
            return new List<Experience>();

        try
        {
            return await _retrieval.FindSimilarErrorResolutionsAsync(errorDescription, topK: 5);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Warning: Could not find error resolutions: {ex.Message}");
            return new List<Experience>();
        }
    }

    /// <summary>
    /// Get statistics about learning system
    /// </summary>
    public async Task<LearningStatistics> GetStatisticsAsync()
    {
        if (!_initialized)
            return new LearningStatistics();

        try
        {
            var totalCount = await _storage.GetExperienceCountAsync();
            var recentExperiences = _capture.GetRecentExperiences(10);

            return new LearningStatistics
            {
                TotalExperiencesStored = (int)totalCount,
                RecentExperiencesCount = recentExperiences.Count,
                SystemInitialized = _initialized
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Warning: Could not get statistics: {ex.Message}");
            return new LearningStatistics { SystemInitialized = false };
        }
    }

    /// <summary>
    /// Format time ago string
    /// </summary>
    private string GetTimeAgoString(DateTime timestamp)
    {
        var span = DateTime.UtcNow - timestamp;

        if (span.TotalDays >= 365)
            return $"{(int)(span.TotalDays / 365)} year{((int)(span.TotalDays / 365) > 1 ? "s" : "")} ago";

        if (span.TotalDays >= 30)
            return $"{(int)(span.TotalDays / 30)} month{((int)(span.TotalDays / 30) > 1 ? "s" : "")} ago";

        if (span.TotalDays >= 1)
            return $"{(int)span.TotalDays} day{((int)span.TotalDays > 1 ? "s" : "")} ago";

        if (span.TotalHours >= 1)
            return $"{(int)span.TotalHours} hour{((int)span.TotalHours > 1 ? "s" : "")} ago";

        if (span.TotalMinutes >= 1)
            return $"{(int)span.TotalMinutes} minute{((int)span.TotalMinutes > 1 ? "s" : "")} ago";

        return "just now";
    }

    /// <summary>
    /// Check if learning system is operational
    /// </summary>
    public bool IsInitialized => _initialized;
}

/// <summary>
/// Statistics about the learning system
/// </summary>
public class LearningStatistics
{
    public int TotalExperiencesStored { get; set; }
    public int RecentExperiencesCount { get; set; }
    public bool SystemInitialized { get; set; }
    public Dictionary<ExperienceType, int> ExperiencesByType { get; set; } = new();
}
