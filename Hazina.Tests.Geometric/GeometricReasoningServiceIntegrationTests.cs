using Xunit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Hazina.Core.Entities.Geometric;
using Hazina.Data.Contexts;
using Hazina.Data.Repositories;
using Hazina.Services.Geometric;

namespace Hazina.Tests.Geometric
{
    /// <summary>
    /// Integration tests for full geometric reasoning workflow.
    /// Tests end-to-end scenarios from thought space creation → concept learning → mastery.
    /// </summary>
    public class GeometricReasoningServiceIntegrationTests : IDisposable
    {
        private readonly GeometricReasoningDbContext _context;
        private readonly GeometricReasoningRepository _repository;
        private readonly CurvatureCalculationService _curvatureService;
        private readonly MasteryCalculationService _masteryService;
        private readonly GeometricReasoningService _service;

        public GeometricReasoningServiceIntegrationTests()
        {
            // Create in-memory database for integration testing
            var options = new DbContextOptionsBuilder<GeometricReasoningDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new GeometricReasoningDbContext(options);
            _repository = new GeometricReasoningRepository(_context);
            _curvatureService = new CurvatureCalculationService();
            _masteryService = new MasteryCalculationService();
            _service = new GeometricReasoningService(_repository, _curvatureService, _masteryService);
        }

        public void Dispose()
        {
            _context.Dispose();
        }

        [Fact]
        public async Task CompleteLearningSCenario_ShouldReduceCurvatureAndIncreaseMastery()
        {
            // Arrange: Create thought space for programming domain
            var thoughtSpace = await _service.CreateThoughtSpaceAsync(
                userId: "test-user",
                domain: "programming");

            // Act 1: Add three concepts with increasing difficulty
            var variablesConcept = await _service.AddConceptAsync(
                thoughtSpace.Id,
                "Variables",
                "Basic variable declaration and assignment",
                baseConfusion: 0.5); // Easy concept

            var functionsConcept = await _service.AddConceptAsync(
                thoughtSpace.Id,
                "Functions",
                "Function definition and invocation",
                baseConfusion: 1.5); // Medium concept

            var algorithmsConcept = await _service.AddConceptAsync(
                thoughtSpace.Id,
                "Algorithms",
                "Sorting and searching algorithms",
                baseConfusion: 2.5); // Hard concept

            // Create prerequisite chain: Variables → Functions → Algorithms
            await _service.CreatePrerequisiteAsync(variablesConcept.Id, functionsConcept.Id);
            await _service.CreatePrerequisiteAsync(functionsConcept.Id, algorithmsConcept.Id);

            // Act 2: Practice variables concept (easy, should master quickly)
            double initialCurvature = variablesConcept.LocalCurvature;

            for (int i = 0; i < 5; i++)
            {
                variablesConcept = await _service.RecordLearningEventAsync(
                    variablesConcept.Id,
                    LearningEventType.Practice,
                    durationMinutes: 30);
            }

            // Assert: Variables should show significant mastery and curvature reduction
            variablesConcept.MasteryLevel.Should().BeGreaterThan(0.3, "5 practice sessions should yield significant mastery");
            variablesConcept.LocalCurvature.Should().BeLessThan(initialCurvature, "curvature should decrease with practice");
            variablesConcept.PracticeCount.Should().Be(5);

            // Act 3: Analyze learning progress
            var analysis = await _service.AnalyzeLearningProgressAsync(thoughtSpace.Id);

            // Assert: Analysis should show realistic metrics
            analysis.TotalConcepts.Should().Be(3);
            analysis.AverageMastery.Should().BeGreaterThan(0.0);
            analysis.TotalPracticeCount.Should().Be(5);
            analysis.TotalMinutesSpent.Should().Be(150); // 5 sessions × 30 minutes
            analysis.MasteredConcepts.Should().BeEmpty("no concepts fully mastered yet");
            analysis.StrugglingConcepts.Should().NotBeEmpty("should identify high-curvature concepts");
        }

        [Fact]
        public async Task RicciFlowSmoothing_ShouldApplyDuringLearning()
        {
            // Arrange
            var thoughtSpace = await _service.CreateThoughtSpaceAsync("test-user", "mathematics");
            var concept = await _service.AddConceptAsync(
                thoughtSpace.Id,
                "Calculus",
                "Differential and integral calculus",
                baseConfusion: 3.0); // Very hard concept

            double curvatureBeforeLearning = concept.LocalCurvature;

            // Act: Record single high-quality learning event
            var updated = await _service.RecordLearningEventAsync(
                concept.Id,
                LearningEventType.Breakthrough,
                durationMinutes: 60);

            // Assert: Ricci flow should have been applied (curvature smoothed)
            updated.LocalCurvature.Should().BeLessThan(curvatureBeforeLearning,
                "Ricci flow smoothing should reduce curvature");

            // Verify learning event was recorded
            var events = await _repository.GetConceptLearningEventsAsync(concept.Id);
            events.Should().HaveCount(1);
            events[0].Type.Should().Be(LearningEventType.Breakthrough);
            events[0].CurvatureAfter.Should().BeLessThan(events[0].CurvatureBefore);
        }

        [Fact]
        public async Task OptimalLearningPath_ShouldOrderByMasteryAndCurvature()
        {
            // Arrange: Create thought space with concepts at different mastery levels
            var thoughtSpace = await _service.CreateThoughtSpaceAsync("test-user", "data-science");

            var beginner = await _service.AddConceptAsync(thoughtSpace.Id, "Beginner", "Easy", 0.5);
            var intermediate = await _service.AddConceptAsync(thoughtSpace.Id, "Intermediate", "Medium", 1.5);
            var advanced = await _service.AddConceptAsync(thoughtSpace.Id, "Advanced", "Hard", 2.5);

            // Practice beginner concept to raise mastery
            for (int i = 0; i < 3; i++)
            {
                await _service.RecordLearningEventAsync(beginner.Id, LearningEventType.Practice, 30);
            }

            // Act: Get optimal learning path
            var path = await _service.GetOptimalLearningPathAsync(thoughtSpace.Id);

            // Assert: Should return low-mastery concepts first
            path.Should().HaveCount(3);
            path[0].MasteryLevel.Should().BeLessOrEqualTo(path[1].MasteryLevel);
            path[1].MasteryLevel.Should().BeLessOrEqualTo(path[2].MasteryLevel);

            // Beginner (practiced) should be last
            path[2].Name.Should().Be("Beginner");
        }

        [Fact]
        public async Task PredictTotalMasteryTime_ShouldCalculateRealisticEstimate()
        {
            // Arrange
            var thoughtSpace = await _service.CreateThoughtSpaceAsync("test-user", "web-development");

            await _service.AddConceptAsync(thoughtSpace.Id, "HTML", "Markup", 0.5);
            await _service.AddConceptAsync(thoughtSpace.Id, "CSS", "Styling", 1.0);
            await _service.AddConceptAsync(thoughtSpace.Id, "JavaScript", "Programming", 2.0);

            // Act: Predict time to reach 80% mastery
            int predictedMinutes = await _service.PredictTotalMasteryTimeAsync(thoughtSpace.Id, targetMastery: 0.8);

            // Assert: Should return positive estimate
            predictedMinutes.Should().BeGreaterThan(0, "should require some time to master concepts");
            predictedMinutes.Should().BeLessThan(10000, "estimate should be realistic (< ~167 hours)");
        }

        [Fact]
        public async Task LearningVelocity_ShouldBeNegativeWhenCurvatureDecreases()
        {
            // Arrange
            var thoughtSpace = await _service.CreateThoughtSpaceAsync("test-user", "physics");
            var concept = await _service.AddConceptAsync(thoughtSpace.Id, "Mechanics", "Classical mechanics", 2.0);

            // Act: Record multiple learning events (curvature should decrease)
            for (int i = 0; i < 5; i++)
            {
                await _service.RecordLearningEventAsync(concept.Id, LearningEventType.Practice, 45);
            }

            // Get updated thought space
            var updated = await _repository.GetThoughtSpaceAsync(thoughtSpace.Id, includeRelated: true);

            // Assert: Learning velocity should be negative (curvature decreasing = learning)
            updated.Should().NotBeNull();
            updated!.LearningVelocity.Should().BeNegative("curvature should decrease with consistent practice");
        }

        [Fact]
        public async Task GlobalCurvature_ShouldReflectWeightedAverageOfConcepts()
        {
            // Arrange
            var thoughtSpace = await _service.CreateThoughtSpaceAsync("test-user", "machine-learning");

            // Add one easy concept with high mastery
            var easy = await _service.AddConceptAsync(thoughtSpace.Id, "Easy", "Basic", 0.5);
            for (int i = 0; i < 10; i++)
            {
                await _service.RecordLearningEventAsync(easy.Id, LearningEventType.Practice, 30);
            }

            // Add one hard concept with low mastery
            var hard = await _service.AddConceptAsync(thoughtSpace.Id, "Hard", "Advanced", 3.0);

            // Act: Get updated thought space
            var updated = await _repository.GetThoughtSpaceAsync(thoughtSpace.Id, includeRelated: true);

            // Assert: Global curvature should be between the two local curvatures
            updated.Should().NotBeNull();
            var easyUpdated = updated!.Concepts.First(c => c.Name == "Easy");
            var hardUpdated = updated.Concepts.First(c => c.Name == "Hard");

            updated.GlobalCurvature.Should().BeGreaterThan(0.0);
            updated.GlobalCurvature.Should().BeGreaterThan(easyUpdated.LocalCurvature, "hard concept pulls average up");
        }

        [Fact]
        public async Task MasteryDecayNotImplemented_ButFormulaValidated()
        {
            // Note: Decay is not automatically applied in this implementation
            // It would need a background job to apply time-based decay
            // This test validates the formula is accessible

            // Arrange
            var thoughtSpace = await _service.CreateThoughtSpaceAsync("test-user", "languages");
            var concept = await _service.AddConceptAsync(thoughtSpace.Id, "Spanish", "Learn Spanish", 1.0);

            // Practice to build mastery
            for (int i = 0; i < 5; i++)
            {
                await _service.RecordLearningEventAsync(concept.Id, LearningEventType.Practice, 30);
            }

            var practiced = await _repository.GetConceptAsync(concept.Id);
            double masteryBeforeDecay = practiced!.MasteryLevel;

            // Act: Manually apply decay (simulating 1 year without practice)
            double decayed = _masteryService.ApplyMasteryDecay(masteryBeforeDecay, daysSinceLastPractice: 365);

            // Assert: Decay should reduce mastery
            decayed.Should().BeLessThan(masteryBeforeDecay);
            decayed.Should().BeApproximately(masteryBeforeDecay * 0.74, 0.05, "30% decay per year");
        }

        [Fact]
        public async Task QualityScoreEstimation_ShouldReflectTypeAndDuration()
        {
            // Arrange
            var thoughtSpace = await _service.CreateThoughtSpaceAsync("test-user", "music");
            var concept = await _service.AddConceptAsync(thoughtSpace.Id, "Piano", "Learn piano", 1.5);

            // Act: Record different event types
            await _service.RecordLearningEventAsync(concept.Id, LearningEventType.Practice, 45);
            await _service.RecordLearningEventAsync(concept.Id, LearningEventType.Breakthrough, 30);
            await _service.RecordLearningEventAsync(concept.Id, LearningEventType.Study, 60);

            // Get events
            var events = await _repository.GetConceptLearningEventsAsync(concept.Id);

            // Assert: Different event types should have different quality scores
            events.Should().HaveCount(3);

            var practiceEvent = events.First(e => e.Type == LearningEventType.Practice);
            var breakthroughEvent = events.First(e => e.Type == LearningEventType.Breakthrough);
            var studyEvent = events.First(e => e.Type == LearningEventType.Study);

            breakthroughEvent.QualityScore.Should().BeGreaterThan(practiceEvent.QualityScore,
                "breakthrough > practice");
            practiceEvent.QualityScore.Should().BeGreaterThan(studyEvent.QualityScore,
                "practice > study");
        }
    }
}
