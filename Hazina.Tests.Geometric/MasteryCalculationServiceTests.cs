using Xunit;
using FluentAssertions;
using Hazina.Core.Entities.Geometric;
using Hazina.Services.Geometric;

namespace Hazina.Tests.Geometric
{
    public class MasteryCalculationServiceTests
    {
        private readonly MasteryCalculationService _service;

        public MasteryCalculationServiceTests()
        {
            _service = new MasteryCalculationService();
        }

        [Fact]
        public void CalculateMasteryLevel_WithNoPractice_ShouldReturnZero()
        {
            // Arrange
            var concept = new Concept
            {
                PracticeCount = 0,
                Events = new List<LearningEvent>()
            };

            // Act
            var mastery = _service.CalculateMasteryLevel(concept);

            // Assert
            mastery.Should().Be(0.0);
        }

        [Theory]
        [InlineData(5, 0.8, 0.33)]  // 5 practice × 0.8 quality ≈ 33% mastery
        [InlineData(10, 0.8, 0.55)] // 10 practice × 0.8 quality ≈ 55% mastery
        [InlineData(20, 0.9, 0.84)] // 20 practice × 0.9 quality ≈ 84% mastery
        public void CalculateMasteryLevel_WithPracticeAndQuality_ShouldFollowExponentialCurve(
            int practiceCount,
            double quality,
            double expectedMastery)
        {
            // Act
            var mastery = _service.CalculateMasteryLevel(practiceCount, quality);

            // Assert
            mastery.Should().BeApproximately(expectedMastery, 0.05);
        }

        [Fact]
        public void CalculateMasteryLevel_ShouldNeverExceedOne()
        {
            // Arrange - Very high practice count
            int practiceCount = 100;
            double quality = 1.0;

            // Act
            var mastery = _service.CalculateMasteryLevel(practiceCount, quality);

            // Assert
            mastery.Should().BeLessOrEqualTo(1.0);
            mastery.Should().BeGreaterThan(0.99); // Should be very close to 1.0
        }

        [Fact]
        public void PredictPracticeCountNeeded_ForHalfMastery_ShouldBeReasonable()
        {
            // Arrange
            double targetMastery = 0.5;
            double averageQuality = 0.8;

            // Act
            var practiceCount = _service.PredictPracticeCountNeeded(targetMastery, averageQuality);

            // Assert
            practiceCount.Should().BeInRange(8, 10); // ~9 practices for 50% mastery at 0.8 quality
        }

        [Fact]
        public void PredictPracticeCountNeeded_ForHighMastery_ShouldRequireMorePractice()
        {
            // Arrange
            double lowTarget = 0.5;
            double highTarget = 0.9;
            double quality = 0.8;

            // Act
            var lowPractice = _service.PredictPracticeCountNeeded(lowTarget, quality);
            var highPractice = _service.PredictPracticeCountNeeded(highTarget, quality);

            // Assert
            highPractice.Should().BeGreaterThan(lowPractice);
        }

        [Fact]
        public void UpdateMasteryAfterPractice_ShouldIncreaseMastery()
        {
            // Arrange
            double currentMastery = 0.3;
            int currentPracticeCount = 5;
            double newQualityScore = 0.9;
            double previousQualityAverage = 0.7;

            // Act
            var newMastery = _service.UpdateMasteryAfterPractice(
                currentMastery,
                currentPracticeCount,
                newQualityScore,
                previousQualityAverage);

            // Assert
            newMastery.Should().BeGreaterThan(currentMastery);
        }

        [Theory]
        [InlineData(LearningEventType.Practice, 45, 0.85)]
        [InlineData(LearningEventType.Application, 60, 0.90)]
        [InlineData(LearningEventType.Breakthrough, 30, 0.95)]
        [InlineData(LearningEventType.Study, 45, 0.55)]
        [InlineData(LearningEventType.Review, 30, 0.60)]
        public void EstimateQualityScore_ShouldReflectEventType(
            LearningEventType eventType,
            int duration,
            double expectedQuality)
        {
            // Act
            var quality = _service.EstimateQualityScore(eventType, duration);

            // Assert
            quality.Should().BeApproximately(expectedQuality, 0.15);
        }

        [Fact]
        public void EstimateQualityScore_WithShortDuration_ShouldReduceQuality()
        {
            // Arrange
            var normalDuration = 45;
            var shortDuration = 10;

            // Act
            var normalQuality = _service.EstimateQualityScore(LearningEventType.Practice, normalDuration);
            var shortQuality = _service.EstimateQualityScore(LearningEventType.Practice, shortDuration);

            // Assert
            shortQuality.Should().BeLessThan(normalQuality);
        }

        [Fact]
        public void ApplyMasteryDecay_WithNoTime_ShouldNotDecay()
        {
            // Arrange
            double mastery = 0.8;
            double days = 0;

            // Act
            var decayedMastery = _service.ApplyMasteryDecay(mastery, days);

            // Assert
            decayedMastery.Should().BeApproximately(mastery, 0.01);
        }

        [Fact]
        public void ApplyMasteryDecay_WithOneYear_ShouldDecayByRate()
        {
            // Arrange
            double mastery = 0.8;
            double days = 365;
            double decayRate = 0.3; // 30% decay per year

            // Act
            var decayedMastery = _service.ApplyMasteryDecay(mastery, days, decayRate);

            // Assert
            // After 1 year with 30% decay: 0.8 × exp(-0.3) ≈ 0.8 × 0.74 ≈ 0.59
            decayedMastery.Should().BeApproximately(0.59, 0.05);
        }

        [Fact]
        public void ApplyMasteryDecay_ShouldNeverBeNegative()
        {
            // Arrange
            double mastery = 0.5;
            double days = 10000; // Very long time

            // Act
            var decayedMastery = _service.ApplyMasteryDecay(mastery, days);

            // Assert
            decayedMastery.Should().BeGreaterOrEqualTo(0.0);
        }
    }
}
