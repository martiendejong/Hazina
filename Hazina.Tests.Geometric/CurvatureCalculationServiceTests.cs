using Xunit;
using FluentAssertions;
using Hazina.Core.Entities.Geometric;
using Hazina.Services.Geometric;

namespace Hazina.Tests.Geometric
{
    public class CurvatureCalculationServiceTests
    {
        private readonly CurvatureCalculationService _service;

        public CurvatureCalculationServiceTests()
        {
            _service = new CurvatureCalculationService();
        }

        [Fact]
        public void CalculateLocalCurvature_WithHighMastery_ShouldReturnLowCurvature()
        {
            // Arrange
            var concept = new Concept
            {
                BaseConfusion = 1.0,
                MasteryLevel = 0.9, // High mastery
                LastPracticedAt = DateTime.UtcNow.AddDays(-7),
                OutgoingConnections = new List<ConceptConnection>(),
                IncomingConnections = new List<ConceptConnection>()
            };

            // Act
            var curvature = _service.CalculateLocalCurvature(concept);

            // Assert
            curvature.Should().BeLessThan(0.2); // Low curvature with high mastery
        }

        [Fact]
        public void CalculateLocalCurvature_WithZeroMastery_ShouldReturnHighCurvature()
        {
            // Arrange
            var concept = new Concept
            {
                BaseConfusion = 2.0,
                MasteryLevel = 0.0, // No mastery
                LastPracticedAt = null, // Never practiced
                OutgoingConnections = new List<ConceptConnection>(),
                IncomingConnections = new List<ConceptConnection>()
            };

            // Act
            var curvature = _service.CalculateLocalCurvature(concept);

            // Assert
            curvature.Should().BeGreaterOrEqualTo(1.5); // High curvature with no mastery (exactly 2.0 × 1.0 × 1.5 × 0.5 = 1.5)
        }

        [Fact]
        public void CalculateGlobalCurvature_WithMixedConcepts_ShouldReturnWeightedAverage()
        {
            // Arrange
            var space = new ThoughtSpace
            {
                Concepts = new List<Concept>
                {
                    new Concept
                    {
                        LocalCurvature = 0.2,
                        OutgoingConnections = new List<ConceptConnection> { new ConceptConnection() },
                        IncomingConnections = new List<ConceptConnection>()
                    },
                    new Concept
                    {
                        LocalCurvature = 0.8,
                        OutgoingConnections = new List<ConceptConnection>(),
                        IncomingConnections = new List<ConceptConnection>()
                    }
                }
            };

            // Act
            var globalCurvature = _service.CalculateGlobalCurvature(space);

            // Assert
            globalCurvature.Should().BeInRange(0.2, 0.8);
        }

        [Fact]
        public void CalculateGlobalCurvature_WithNoConcepts_ShouldReturnZero()
        {
            // Arrange
            var space = new ThoughtSpace
            {
                Concepts = new List<Concept>()
            };

            // Act
            var globalCurvature = _service.CalculateGlobalCurvature(space);

            // Assert
            globalCurvature.Should().Be(0.0);
        }

        [Fact]
        public void PredictMasteryTimeMinutes_WithHighCurvature_ShouldReturnLongerTime()
        {
            // Arrange
            var highCurvatureConcept = new Concept
            {
                BaseConfusion = 2.0,
                MasteryLevel = 0.1,
                LocalCurvature = 1.8 // High curvature
            };

            var lowCurvatureConcept = new Concept
            {
                BaseConfusion = 0.5,
                MasteryLevel = 0.7,
                LocalCurvature = 0.15 // Low curvature
            };

            // Act
            var highTime = _service.PredictMasteryTimeMinutes(highCurvatureConcept);
            var lowTime = _service.PredictMasteryTimeMinutes(lowCurvatureConcept);

            // Assert
            highTime.Should().BeGreaterThan(lowTime);
        }

        [Theory]
        [InlineData(1.0, 0.1, 10, 0.37)] // 10 steps: 1.0 → ~0.37
        [InlineData(0.5, 0.1, 5, 0.30)]  // 5 steps: 0.5 → ~0.30
        [InlineData(2.0, 0.2, 10, 0.27)] // Higher flow rate = faster smoothing
        public void ApplyRicciFlow_ShouldSmoothCurvature(
            double initialCurvature,
            double flowRate,
            int timeSteps,
            double expectedCurvature)
        {
            // Act
            var result = _service.ApplyRicciFlow(initialCurvature, flowRate, timeSteps);

            // Assert
            result.Should().BeLessThan(initialCurvature); // Curvature should decrease
            result.Should().BeApproximately(expectedCurvature, 0.1);
        }

        [Fact]
        public void ApplyRicciFlow_WithNegativeCurvature_ShouldThrowException()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
                _service.ApplyRicciFlow(-0.5, 0.1, 1));
        }

        [Fact]
        public void CalculateLearningVelocity_WithDecreasingCurvature_ShouldReturnNegative()
        {
            // Arrange
            var events = new List<LearningEvent>
            {
                new LearningEvent
                {
                    OccurredAt = DateTime.UtcNow.AddDays(-2),
                    CurvatureAfter = 0.8
                },
                new LearningEvent
                {
                    OccurredAt = DateTime.UtcNow.AddDays(-1),
                    CurvatureAfter = 0.6
                },
                new LearningEvent
                {
                    OccurredAt = DateTime.UtcNow,
                    CurvatureAfter = 0.4
                }
            };

            // Act
            var velocity = _service.CalculateLearningVelocity(events);

            // Assert
            velocity.Should().BeNegative(); // Curvature decreasing = learning
        }

        [Fact]
        public void CalculateLearningVelocity_WithNoEvents_ShouldReturnZero()
        {
            // Arrange
            var events = new List<LearningEvent>();

            // Act
            var velocity = _service.CalculateLearningVelocity(events);

            // Assert
            velocity.Should().Be(0.0);
        }
    }
}
