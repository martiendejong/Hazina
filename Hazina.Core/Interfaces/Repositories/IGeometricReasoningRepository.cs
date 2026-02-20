using Hazina.Core.Entities.Geometric;

namespace Hazina.Core.Interfaces.Repositories
{
    /// <summary>
    /// Repository interface for Geometric Reasoning Service data access.
    /// Provides CRUD operations and specialized queries for ThoughtSpaces, Concepts, and LearningEvents.
    /// </summary>
    public interface IGeometricReasoningRepository
    {
        // ===== ThoughtSpace Operations =====

        /// <summary>
        /// Creates a new thought space.
        /// </summary>
        Task<ThoughtSpace> CreateThoughtSpaceAsync(ThoughtSpace space);

        /// <summary>
        /// Gets a thought space by ID, including related concepts and learning history.
        /// </summary>
        Task<ThoughtSpace?> GetThoughtSpaceAsync(string id, bool includeRelated = true);

        /// <summary>
        /// Gets all thought spaces for a user.
        /// </summary>
        Task<List<ThoughtSpace>> GetUserThoughtSpacesAsync(string userId);

        /// <summary>
        /// Updates an existing thought space.
        /// </summary>
        Task<ThoughtSpace> UpdateThoughtSpaceAsync(ThoughtSpace space);

        /// <summary>
        /// Deletes a thought space and all related data (cascade delete via FK).
        /// </summary>
        Task DeleteThoughtSpaceAsync(string id);

        // ===== Concept Operations =====

        /// <summary>
        /// Creates a new concept within a thought space.
        /// </summary>
        Task<Concept> CreateConceptAsync(Concept concept);

        /// <summary>
        /// Gets a concept by ID, including connections and learning events.
        /// </summary>
        Task<Concept?> GetConceptAsync(string id, bool includeRelated = true);

        /// <summary>
        /// Gets all concepts within a thought space.
        /// </summary>
        Task<List<Concept>> GetConceptsAsync(string thoughtSpaceId);

        /// <summary>
        /// Gets concepts by their ConceptId (domain identifier) within a thought space.
        /// </summary>
        Task<Concept?> GetConceptByConceptIdAsync(string thoughtSpaceId, string conceptId);

        /// <summary>
        /// Updates an existing concept.
        /// </summary>
        Task<Concept> UpdateConceptAsync(Concept concept);

        /// <summary>
        /// Deletes a concept and all related connections/events (cascade delete via FK).
        /// </summary>
        Task DeleteConceptAsync(string id);

        // ===== ConceptConnection Operations =====

        /// <summary>
        /// Creates a new connection between two concepts.
        /// </summary>
        Task<ConceptConnection> CreateConnectionAsync(ConceptConnection connection);

        /// <summary>
        /// Gets all connections for a concept (both incoming and outgoing).
        /// </summary>
        Task<List<ConceptConnection>> GetConceptConnectionsAsync(string conceptId);

        /// <summary>
        /// Deletes a connection between concepts.
        /// </summary>
        Task DeleteConnectionAsync(string id);

        // ===== LearningEvent Operations =====

        /// <summary>
        /// Creates a new learning event.
        /// </summary>
        Task<LearningEvent> CreateLearningEventAsync(LearningEvent learningEvent);

        /// <summary>
        /// Gets learning events for a concept, optionally filtered by date range.
        /// </summary>
        Task<List<LearningEvent>> GetConceptLearningEventsAsync(
            string conceptId,
            DateTime? startDate = null,
            DateTime? endDate = null);

        /// <summary>
        /// Gets learning events for a thought space, optionally filtered by date range.
        /// </summary>
        Task<List<LearningEvent>> GetThoughtSpaceLearningEventsAsync(
            string thoughtSpaceId,
            DateTime? startDate = null,
            DateTime? endDate = null);

        /// <summary>
        /// Gets the most recent learning events across all thought spaces for a user.
        /// </summary>
        Task<List<LearningEvent>> GetRecentLearningEventsAsync(string userId, int count = 10);

        // ===== Batch Operations =====

        /// <summary>
        /// Saves all pending changes to the database.
        /// </summary>
        Task<int> SaveChangesAsync();
    }
}
