using Microsoft.EntityFrameworkCore;
using Hazina.Core.Entities.Geometric;
using Hazina.Core.Interfaces.Repositories;
using Hazina.Data.Contexts;

namespace Hazina.Data.Repositories
{
    /// <summary>
    /// Repository implementation for Geometric Reasoning Service.
    /// Provides data access layer using Entity Framework Core.
    /// </summary>
    public class GeometricReasoningRepository : IGeometricReasoningRepository
    {
        private readonly GeometricReasoningDbContext _context;

        public GeometricReasoningRepository(GeometricReasoningDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        // ===== ThoughtSpace Operations =====

        public async Task<ThoughtSpace> CreateThoughtSpaceAsync(ThoughtSpace space)
        {
            _context.ThoughtSpaces.Add(space);
            await _context.SaveChangesAsync();
            return space;
        }

        public async Task<ThoughtSpace?> GetThoughtSpaceAsync(string id, bool includeRelated = true)
        {
            IQueryable<ThoughtSpace> query = _context.ThoughtSpaces;

            if (includeRelated)
            {
                query = query
                    .Include(ts => ts.Concepts)
                        .ThenInclude(c => c.OutgoingConnections)
                    .Include(ts => ts.Concepts)
                        .ThenInclude(c => c.IncomingConnections)
                    .Include(ts => ts.LearningHistory);
            }

            return await query.FirstOrDefaultAsync(ts => ts.Id == id);
        }

        public async Task<List<ThoughtSpace>> GetUserThoughtSpacesAsync(string userId)
        {
            return await _context.ThoughtSpaces
                .Where(ts => ts.UserId == userId)
                .Include(ts => ts.Concepts)
                .OrderByDescending(ts => ts.UpdatedAt)
                .ToListAsync();
        }

        public async Task<ThoughtSpace> UpdateThoughtSpaceAsync(ThoughtSpace space)
        {
            space.UpdatedAt = DateTime.UtcNow;
            _context.ThoughtSpaces.Update(space);
            await _context.SaveChangesAsync();
            return space;
        }

        public async Task DeleteThoughtSpaceAsync(string id)
        {
            var space = await _context.ThoughtSpaces.FindAsync(id);
            if (space != null)
            {
                _context.ThoughtSpaces.Remove(space);
                await _context.SaveChangesAsync();
            }
        }

        // ===== Concept Operations =====

        public async Task<Concept> CreateConceptAsync(Concept concept)
        {
            _context.Concepts.Add(concept);
            await _context.SaveChangesAsync();
            return concept;
        }

        public async Task<Concept?> GetConceptAsync(string id, bool includeRelated = true)
        {
            IQueryable<Concept> query = _context.Concepts;

            if (includeRelated)
            {
                query = query
                    .Include(c => c.OutgoingConnections)
                        .ThenInclude(conn => conn.ToConcept)
                    .Include(c => c.IncomingConnections)
                        .ThenInclude(conn => conn.FromConcept)
                    .Include(c => c.Events);
            }

            return await query.FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task<List<Concept>> GetConceptsAsync(string thoughtSpaceId)
        {
            return await _context.Concepts
                .Where(c => c.ThoughtSpaceId == thoughtSpaceId)
                .Include(c => c.OutgoingConnections)
                .Include(c => c.IncomingConnections)
                .OrderBy(c => c.Name)
                .ToListAsync();
        }

        public async Task<Concept?> GetConceptByConceptIdAsync(string thoughtSpaceId, string conceptId)
        {
            return await _context.Concepts
                .Include(c => c.OutgoingConnections)
                .Include(c => c.IncomingConnections)
                .Include(c => c.Events)
                .FirstOrDefaultAsync(c => c.ThoughtSpaceId == thoughtSpaceId && c.ConceptId == conceptId);
        }

        public async Task<Concept> UpdateConceptAsync(Concept concept)
        {
            concept.UpdatedAt = DateTime.UtcNow;
            _context.Concepts.Update(concept);
            await _context.SaveChangesAsync();
            return concept;
        }

        public async Task DeleteConceptAsync(string id)
        {
            var concept = await _context.Concepts.FindAsync(id);
            if (concept != null)
            {
                _context.Concepts.Remove(concept);
                await _context.SaveChangesAsync();
            }
        }

        // ===== ConceptConnection Operations =====

        public async Task<ConceptConnection> CreateConnectionAsync(ConceptConnection connection)
        {
            _context.ConceptConnections.Add(connection);
            await _context.SaveChangesAsync();
            return connection;
        }

        public async Task<List<ConceptConnection>> GetConceptConnectionsAsync(string conceptId)
        {
            var outgoing = await _context.ConceptConnections
                .Include(conn => conn.ToConcept)
                .Where(conn => conn.FromConceptId == conceptId)
                .ToListAsync();

            var incoming = await _context.ConceptConnections
                .Include(conn => conn.FromConcept)
                .Where(conn => conn.ToConceptId == conceptId)
                .ToListAsync();

            return outgoing.Concat(incoming).ToList();
        }

        public async Task DeleteConnectionAsync(string id)
        {
            var connection = await _context.ConceptConnections.FindAsync(id);
            if (connection != null)
            {
                _context.ConceptConnections.Remove(connection);
                await _context.SaveChangesAsync();
            }
        }

        // ===== LearningEvent Operations =====

        public async Task<LearningEvent> CreateLearningEventAsync(LearningEvent learningEvent)
        {
            _context.LearningEvents.Add(learningEvent);
            await _context.SaveChangesAsync();
            return learningEvent;
        }

        public async Task<List<LearningEvent>> GetConceptLearningEventsAsync(
            string conceptId,
            DateTime? startDate = null,
            DateTime? endDate = null)
        {
            IQueryable<LearningEvent> query = _context.LearningEvents
                .Where(e => e.ConceptId == conceptId);

            if (startDate.HasValue)
            {
                query = query.Where(e => e.OccurredAt >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(e => e.OccurredAt <= endDate.Value);
            }

            return await query
                .OrderByDescending(e => e.OccurredAt)
                .ToListAsync();
        }

        public async Task<List<LearningEvent>> GetThoughtSpaceLearningEventsAsync(
            string thoughtSpaceId,
            DateTime? startDate = null,
            DateTime? endDate = null)
        {
            IQueryable<LearningEvent> query = _context.LearningEvents
                .Include(e => e.Concept)
                .Where(e => e.ThoughtSpaceId == thoughtSpaceId);

            if (startDate.HasValue)
            {
                query = query.Where(e => e.OccurredAt >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(e => e.OccurredAt <= endDate.Value);
            }

            return await query
                .OrderByDescending(e => e.OccurredAt)
                .ToListAsync();
        }

        public async Task<List<LearningEvent>> GetRecentLearningEventsAsync(string userId, int count = 10)
        {
            return await _context.LearningEvents
                .Include(e => e.Concept)
                .Include(e => e.ThoughtSpace)
                .Where(e => e.ThoughtSpace.UserId == userId)
                .OrderByDescending(e => e.OccurredAt)
                .Take(count)
                .ToListAsync();
        }

        // ===== Batch Operations =====

        public async Task<int> SaveChangesAsync()
        {
            return await _context.SaveChangesAsync();
        }
    }
}
