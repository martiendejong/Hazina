using Microsoft.EntityFrameworkCore;
using Xunit;
using FluentAssertions;
using Hazina.Core.Entities.Geometric;
using Hazina.Data.Contexts;
using Hazina.Data.Repositories;

namespace Hazina.Tests.Geometric
{
    public class GeometricReasoningRepositoryTests : IDisposable
    {
        private readonly GeometricReasoningDbContext _context;
        private readonly GeometricReasoningRepository _repository;

        public GeometricReasoningRepositoryTests()
        {
            // Use in-memory database for testing
            var options = new DbContextOptionsBuilder<GeometricReasoningDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new GeometricReasoningDbContext(options);
            _repository = new GeometricReasoningRepository(_context);
        }

        public void Dispose()
        {
            _context.Dispose();
        }

        [Fact]
        public async Task CreateThoughtSpace_ShouldCreateAndReturnThoughtSpace()
        {
            // Arrange
            var thoughtSpace = new ThoughtSpace
            {
                Id = "test-space-1",
                UserId = "user-1",
                Domain = "mathematics",
                Dimensions = 12,
                GlobalCurvature = 0.5,
                LearningVelocity = 0.0,
                TunnelingCapability = 0.7
            };

            // Act
            var result = await _repository.CreateThoughtSpaceAsync(thoughtSpace);

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().Be("test-space-1");
            result.Domain.Should().Be("mathematics");

            var retrieved = await _repository.GetThoughtSpaceAsync("test-space-1", includeRelated: false);
            retrieved.Should().NotBeNull();
            retrieved!.Dimensions.Should().Be(12);
        }

        [Fact]
        public async Task GetUserThoughtSpaces_ShouldReturnAllUserSpaces()
        {
            // Arrange
            var space1 = new ThoughtSpace { Id = "space-1", UserId = "user-1", Domain = "math", Dimensions = 12 };
            var space2 = new ThoughtSpace { Id = "space-2", UserId = "user-1", Domain = "physics", Dimensions = 12 };
            var space3 = new ThoughtSpace { Id = "space-3", UserId = "user-2", Domain = "chemistry", Dimensions = 12 };

            await _repository.CreateThoughtSpaceAsync(space1);
            await _repository.CreateThoughtSpaceAsync(space2);
            await _repository.CreateThoughtSpaceAsync(space3);

            // Act
            var result = await _repository.GetUserThoughtSpacesAsync("user-1");

            // Assert
            result.Should().HaveCount(2);
            result.Should().Contain(s => s.Domain == "math");
            result.Should().Contain(s => s.Domain == "physics");
            result.Should().NotContain(s => s.Domain == "chemistry");
        }

        [Fact]
        public async Task CreateConcept_ShouldCreateAndReturnConcept()
        {
            // Arrange
            var thoughtSpace = new ThoughtSpace
            {
                Id = "space-1",
                UserId = "user-1",
                Domain = "programming",
                Dimensions = 12
            };
            await _repository.CreateThoughtSpaceAsync(thoughtSpace);

            var concept = new Concept
            {
                Id = "concept-1",
                ThoughtSpaceId = "space-1",
                ConceptId = "variables",
                Name = "Variables",
                Description = "Variable concepts",
                BaseConfusion = 0.5,
                MasteryLevel = 0.7,
                LocalCurvature = 0.15,
                PracticeCount = 10,
                PositionJson = "[0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1.0, 1.1, 1.2]"
            };

            // Act
            var result = await _repository.CreateConceptAsync(concept);

            // Assert
            result.Should().NotBeNull();
            result.Name.Should().Be("Variables");
            result.MasteryLevel.Should().Be(0.7);

            var retrieved = await _repository.GetConceptAsync("concept-1", includeRelated: false);
            retrieved.Should().NotBeNull();
            retrieved!.LocalCurvature.Should().Be(0.15);
        }

        [Fact]
        public async Task GetConceptByConceptId_ShouldReturnCorrectConcept()
        {
            // Arrange
            var thoughtSpace = new ThoughtSpace { Id = "space-1", UserId = "user-1", Domain = "test", Dimensions = 12 };
            await _repository.CreateThoughtSpaceAsync(thoughtSpace);

            var concept = new Concept
            {
                Id = "concept-1",
                ThoughtSpaceId = "space-1",
                ConceptId = "functions",
                Name = "Functions",
                Description = "Function concepts",
                BaseConfusion = 1.0,
                MasteryLevel = 0.5,
                LocalCurvature = 0.5,
                PracticeCount = 5,
                PositionJson = "[0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5]"
            };
            await _repository.CreateConceptAsync(concept);

            // Act
            var result = await _repository.GetConceptByConceptIdAsync("space-1", "functions");

            // Assert
            result.Should().NotBeNull();
            result!.Name.Should().Be("Functions");
            result.ConceptId.Should().Be("functions");
        }

        [Fact]
        public async Task CreateConnection_ShouldCreateAndReturnConnection()
        {
            // Arrange
            var thoughtSpace = new ThoughtSpace { Id = "space-1", UserId = "user-1", Domain = "test", Dimensions = 12 };
            await _repository.CreateThoughtSpaceAsync(thoughtSpace);

            var concept1 = new Concept
            {
                Id = "concept-1",
                ThoughtSpaceId = "space-1",
                ConceptId = "var",
                Name = "Variables",
                Description = "Variable concepts",
                BaseConfusion = 0.5,
                MasteryLevel = 0.7,
                LocalCurvature = 0.15,
                PracticeCount = 10,
                PositionJson = "[0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1.0, 1.1, 1.2]"
            };
            var concept2 = new Concept
            {
                Id = "concept-2",
                ThoughtSpaceId = "space-1",
                ConceptId = "func",
                Name = "Functions",
                Description = "Function concepts",
                BaseConfusion = 1.0,
                MasteryLevel = 0.4,
                LocalCurvature = 0.6,
                PracticeCount = 5,
                PositionJson = "[0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1.0, 1.1, 1.2, 1.3]"
            };

            await _repository.CreateConceptAsync(concept1);
            await _repository.CreateConceptAsync(concept2);

            var connection = new ConceptConnection
            {
                Id = "conn-1",
                FromConceptId = "concept-1",
                ToConceptId = "concept-2",
                Type = ConnectionType.Prerequisite,
                Strength = 0.9,
                Reason = "Variables needed for functions"
            };

            // Act
            var result = await _repository.CreateConnectionAsync(connection);

            // Assert
            result.Should().NotBeNull();
            result.Type.Should().Be(ConnectionType.Prerequisite);
            result.Strength.Should().Be(0.9);

            var connections = await _repository.GetConceptConnectionsAsync("concept-1");
            connections.Should().HaveCount(1);
            connections[0].ToConceptId.Should().Be("concept-2");
        }

        [Fact]
        public async Task CreateLearningEvent_ShouldCreateAndReturnEvent()
        {
            // Arrange
            var thoughtSpace = new ThoughtSpace { Id = "space-1", UserId = "user-1", Domain = "test", Dimensions = 12 };
            await _repository.CreateThoughtSpaceAsync(thoughtSpace);

            var concept = new Concept
            {
                Id = "concept-1",
                ThoughtSpaceId = "space-1",
                ConceptId = "test",
                Name = "Test Concept",
                Description = "Test concept description",
                BaseConfusion = 1.0,
                MasteryLevel = 0.5,
                LocalCurvature = 0.5,
                PracticeCount = 5,
                PositionJson = "[0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5]"
            };
            await _repository.CreateConceptAsync(concept);

            var learningEvent = new LearningEvent
            {
                Id = "event-1",
                ThoughtSpaceId = "space-1",
                ConceptId = "concept-1",
                Type = LearningEventType.Practice,
                DurationMinutes = 30,
                QualityScore = 0.8,
                CurvatureBefore = 0.7,
                CurvatureAfter = 0.5,
                Notes = "Good practice session"
            };

            // Act
            var result = await _repository.CreateLearningEventAsync(learningEvent);

            // Assert
            result.Should().NotBeNull();
            result.Type.Should().Be(LearningEventType.Practice);
            result.DurationMinutes.Should().Be(30);

            var events = await _repository.GetConceptLearningEventsAsync("concept-1");
            events.Should().HaveCount(1);
            events[0].QualityScore.Should().Be(0.8);
        }

        [Fact]
        public async Task DeleteThoughtSpace_ShouldCascadeDeleteConcepts()
        {
            // Arrange
            var thoughtSpace = new ThoughtSpace { Id = "space-1", UserId = "user-1", Domain = "test", Dimensions = 12 };
            await _repository.CreateThoughtSpaceAsync(thoughtSpace);

            var concept = new Concept
            {
                Id = "concept-1",
                ThoughtSpaceId = "space-1",
                ConceptId = "test",
                Name = "Test",
                Description = "Test description",
                BaseConfusion = 1.0,
                MasteryLevel = 0.5,
                LocalCurvature = 0.5,
                PracticeCount = 5,
                PositionJson = "[0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5]"
            };
            await _repository.CreateConceptAsync(concept);

            // Act
            await _repository.DeleteThoughtSpaceAsync("space-1");

            // Assert
            var deletedSpace = await _repository.GetThoughtSpaceAsync("space-1", includeRelated: false);
            deletedSpace.Should().BeNull();

            var deletedConcept = await _repository.GetConceptAsync("concept-1", includeRelated: false);
            deletedConcept.Should().BeNull();
        }

        [Fact]
        public async Task UpdateConcept_ShouldUpdateAndReturnConcept()
        {
            // Arrange
            var thoughtSpace = new ThoughtSpace { Id = "space-1", UserId = "user-1", Domain = "test", Dimensions = 12 };
            await _repository.CreateThoughtSpaceAsync(thoughtSpace);

            var concept = new Concept
            {
                Id = "concept-1",
                ThoughtSpaceId = "space-1",
                ConceptId = "test",
                Name = "Test",
                Description = "Test description",
                BaseConfusion = 1.0,
                MasteryLevel = 0.3,
                LocalCurvature = 0.7,
                PracticeCount = 3,
                PositionJson = "[0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5]"
            };
            await _repository.CreateConceptAsync(concept);

            // Act - Update mastery and curvature after practice
            concept.MasteryLevel = 0.6;
            concept.LocalCurvature = 0.4;
            concept.PracticeCount = 6;
            var result = await _repository.UpdateConceptAsync(concept);

            // Assert
            result.MasteryLevel.Should().Be(0.6);
            result.LocalCurvature.Should().Be(0.4);
            result.PracticeCount.Should().Be(6);

            var retrieved = await _repository.GetConceptAsync("concept-1", includeRelated: false);
            retrieved!.MasteryLevel.Should().Be(0.6);
        }
    }
}
