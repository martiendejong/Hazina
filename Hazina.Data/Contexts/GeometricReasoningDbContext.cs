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
            SeedData(modelBuilder);
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

        private void SeedData(ModelBuilder modelBuilder)
        {
            // Seed example thought space (programming domain, 12D)
            var thoughtSpaceId = "ts-example-programming";
            modelBuilder.Entity<ThoughtSpace>().HasData(new ThoughtSpace
            {
                Id = thoughtSpaceId,
                UserId = "demo-user",
                Domain = "programming",
                Dimensions = 12,
                GlobalCurvature = 0.85,
                LearningVelocity = 0.0,
                TunnelingCapability = 0.5,
                CreatedAt = new DateTime(2026, 2, 20, 4, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 2, 20, 4, 0, 0, DateTimeKind.Utc)
            });

            // Seed 3 concepts with realistic curvature values
            var conceptVariables = "concept-variables";
            var conceptFunctions = "concept-functions";
            var conceptAlgorithms = "concept-algorithms";

            modelBuilder.Entity<Concept>().HasData(
                new Concept
                {
                    Id = conceptVariables,
                    ThoughtSpaceId = thoughtSpaceId,
                    ConceptId = "variables",
                    Name = "Variables and Data Types",
                    Description = "Understanding variable declaration, assignment, and different data types",
                    BaseConfusion = 0.5,
                    MasteryLevel = 0.7,
                    LocalCurvature = 0.15,
                    PracticeCount = 10,
                    PositionJson = "[0.2, 0.3, 0.1, 0.4, 0.5, 0.2, 0.3, 0.4, 0.1, 0.2, 0.3, 0.2]",
                    LastPracticedAt = new DateTime(2026, 2, 19, 10, 0, 0, DateTimeKind.Utc),
                    CreatedAt = new DateTime(2026, 2, 18, 10, 0, 0, DateTimeKind.Utc),
                    UpdatedAt = new DateTime(2026, 2, 19, 10, 0, 0, DateTimeKind.Utc)
                },
                new Concept
                {
                    Id = conceptFunctions,
                    ThoughtSpaceId = thoughtSpaceId,
                    ConceptId = "functions",
                    Name = "Functions and Methods",
                    Description = "Understanding function definitions, parameters, return values, and scope",
                    BaseConfusion = 1.2,
                    MasteryLevel = 0.4,
                    LocalCurvature = 0.72,
                    PracticeCount = 5,
                    PositionJson = "[0.4, 0.5, 0.3, 0.6, 0.7, 0.4, 0.5, 0.6, 0.3, 0.4, 0.5, 0.4]",
                    LastPracticedAt = new DateTime(2026, 2, 18, 14, 0, 0, DateTimeKind.Utc),
                    CreatedAt = new DateTime(2026, 2, 17, 10, 0, 0, DateTimeKind.Utc),
                    UpdatedAt = new DateTime(2026, 2, 18, 14, 0, 0, DateTimeKind.Utc)
                },
                new Concept
                {
                    Id = conceptAlgorithms,
                    ThoughtSpaceId = thoughtSpaceId,
                    ConceptId = "algorithms",
                    Name = "Algorithms and Problem Solving",
                    Description = "Understanding algorithmic thinking, sorting, searching, and optimization",
                    BaseConfusion = 2.5,
                    MasteryLevel = 0.1,
                    LocalCurvature = 2.25,
                    PracticeCount = 2,
                    PositionJson = "[0.7, 0.8, 0.6, 0.9, 1.0, 0.7, 0.8, 0.9, 0.6, 0.7, 0.8, 0.7]",
                    LastPracticedAt = new DateTime(2026, 2, 17, 16, 0, 0, DateTimeKind.Utc),
                    CreatedAt = new DateTime(2026, 2, 16, 10, 0, 0, DateTimeKind.Utc),
                    UpdatedAt = new DateTime(2026, 2, 17, 16, 0, 0, DateTimeKind.Utc)
                }
            );

            // Seed 2 prerequisite connections
            modelBuilder.Entity<ConceptConnection>().HasData(
                new ConceptConnection
                {
                    Id = "conn-var-to-func",
                    FromConceptId = conceptVariables,
                    ToConceptId = conceptFunctions,
                    Type = ConnectionType.Prerequisite,
                    Strength = 0.9,
                    Reason = "Understanding variables is essential before learning functions",
                    CreatedAt = new DateTime(2026, 2, 18, 10, 0, 0, DateTimeKind.Utc)
                },
                new ConceptConnection
                {
                    Id = "conn-func-to-algo",
                    FromConceptId = conceptFunctions,
                    ToConceptId = conceptAlgorithms,
                    Type = ConnectionType.Prerequisite,
                    Strength = 0.95,
                    Reason = "Functions are the building blocks for implementing algorithms",
                    CreatedAt = new DateTime(2026, 2, 17, 10, 0, 0, DateTimeKind.Utc)
                }
            );
        }
    }
}
