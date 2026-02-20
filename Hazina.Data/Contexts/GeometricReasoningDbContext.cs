using Microsoft.EntityFrameworkCore;
using Hazina.Core.Entities.Geometric;

namespace Hazina.Data.Contexts
{
    /// <summary>
    /// Database context for Geometric Reasoning Service.
    /// Manages ThoughtSpaces, Concepts, ConceptConnections, and LearningEvents.
    /// </summary>
    public class GeometricReasoningDbContext : DbContext
    {
        public GeometricReasoningDbContext(
            DbContextOptions<GeometricReasoningDbContext> options)
            : base(options)
        {
        }

        /// <summary>
        /// ThoughtSpaces represent N-dimensional knowledge manifolds.
        /// </summary>
        public DbSet<ThoughtSpace> ThoughtSpaces { get; set; }

        /// <summary>
        /// Concepts are points within thought spaces with geometric properties.
        /// </summary>
        public DbSet<Concept> Concepts { get; set; }

        /// <summary>
        /// ConceptConnections define topology between concepts.
        /// </summary>
        public DbSet<ConceptConnection> ConceptConnections { get; set; }

        /// <summary>
        /// LearningEvents track practice history and drive mastery calculation.
        /// </summary>
        public DbSet<LearningEvent> LearningEvents { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            ConfigureThoughtSpace(modelBuilder);
            ConfigureConcept(modelBuilder);
            ConfigureConceptConnection(modelBuilder);
            ConfigureLearningEvent(modelBuilder);
        }

        private void ConfigureThoughtSpace(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ThoughtSpace>(entity =>
            {
                entity.HasKey(e => e.Id);

                // Index 1: UserId for user lookups
                entity.HasIndex(e => e.UserId)
                    .HasDatabaseName("IX_ThoughtSpaces_UserId");

                // Index 2: UserId + Domain (composite) for domain queries per user
                entity.HasIndex(e => new { e.UserId, e.Domain })
                    .HasDatabaseName("IX_ThoughtSpaces_UserDomain");

                // One ThoughtSpace has many Concepts (cascade delete)
                entity.HasMany(e => e.Concepts)
                    .WithOne(e => e.ThoughtSpace)
                    .HasForeignKey(e => e.ThoughtSpaceId)
                    .OnDelete(DeleteBehavior.Cascade);

                // One ThoughtSpace has many LearningEvents (cascade delete)
                entity.HasMany(e => e.LearningHistory)
                    .WithOne(e => e.ThoughtSpace)
                    .HasForeignKey(e => e.ThoughtSpaceId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }

        private void ConfigureConcept(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Concept>(entity =>
            {
                entity.HasKey(e => e.Id);

                // Index 3: ThoughtSpaceId for thought space concept lookups
                entity.HasIndex(e => e.ThoughtSpaceId)
                    .HasDatabaseName("IX_Concepts_ThoughtSpaceId");

                // Index 4: ThoughtSpaceId + ConceptId (unique composite)
                // Ensures one ConceptId per ThoughtSpace
                entity.HasIndex(e => new { e.ThoughtSpaceId, e.ConceptId })
                    .IsUnique()
                    .HasDatabaseName("IX_Concepts_ThoughtSpaceConceptId");

                // Index 5: LastPracticedAt for recency queries
                entity.HasIndex(e => e.LastPracticedAt)
                    .HasDatabaseName("IX_Concepts_LastPracticedAt");

                // One Concept has many outgoing connections (cascade delete)
                entity.HasMany(e => e.OutgoingConnections)
                    .WithOne(e => e.FromConcept)
                    .HasForeignKey(e => e.FromConceptId)
                    .OnDelete(DeleteBehavior.Cascade);

                // One Concept has many incoming connections (RESTRICT to prevent cascade loops)
                entity.HasMany(e => e.IncomingConnections)
                    .WithOne(e => e.ToConcept)
                    .HasForeignKey(e => e.ToConceptId)
                    .OnDelete(DeleteBehavior.Restrict);

                // One Concept has many LearningEvents (cascade delete)
                entity.HasMany(e => e.Events)
                    .WithOne(e => e.Concept)
                    .HasForeignKey(e => e.ConceptId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }

        private void ConfigureConceptConnection(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ConceptConnection>(entity =>
            {
                entity.HasKey(e => e.Id);

                // Index 6: FromConceptId for outgoing connection lookups
                entity.HasIndex(e => e.FromConceptId)
                    .HasDatabaseName("IX_ConceptConnections_FromConcept");

                // Index 7: ToConceptId for incoming connection lookups
                entity.HasIndex(e => e.ToConceptId)
                    .HasDatabaseName("IX_ConceptConnections_ToConcept");

                // Index 8: FromConceptId + ToConceptId (unique composite)
                // Prevents duplicate connections in same direction
                entity.HasIndex(e => new { e.FromConceptId, e.ToConceptId })
                    .IsUnique()
                    .HasDatabaseName("IX_ConceptConnections_FromTo");
            });
        }

        private void ConfigureLearningEvent(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<LearningEvent>(entity =>
            {
                entity.HasKey(e => e.Id);

                // Index 9: ConceptId for concept history lookups
                entity.HasIndex(e => e.ConceptId)
                    .HasDatabaseName("IX_LearningEvents_ConceptId");

                // Index 10: ThoughtSpaceId for space-wide history lookups
                entity.HasIndex(e => e.ThoughtSpaceId)
                    .HasDatabaseName("IX_LearningEvents_ThoughtSpaceId");

                // Index 11: OccurredAt for time-range queries
                entity.HasIndex(e => e.OccurredAt)
                    .HasDatabaseName("IX_LearningEvents_OccurredAt");

                // Index 12: ConceptId + OccurredAt (composite)
                // Optimizes queries for concept learning history over time
                entity.HasIndex(e => new { e.ConceptId, e.OccurredAt })
                    .HasDatabaseName("IX_LearningEvents_ConceptTime");
            });
        }
    }
}
