using Hazina.Core.Entities.Geometric;

namespace Hazina.Services.Geometric
{
    /// <summary>
    /// Service for calculating mastery levels based on practice history.
    /// Implements exponential learning curve: M = 1 - exp(-PracticeCount × QualityAvg / 10)
    /// </summary>
    public class MasteryCalculationService
    {
        /// <summary>
        /// Calculates mastery level from practice count and quality scores.
        /// Formula: M = 1 - exp(-PracticeCount × QualityAvg / 10)
        /// Returns value between 0.0 (no mastery) and 1.0 (complete mastery).
        /// </summary>
        /// <param name="concept">Concept with practice count and learning events.</param>
        /// <returns>Calculated mastery level (0.0 to 1.0).</returns>
        public double CalculateMasteryLevel(Concept concept)
        {
            if (concept == null)
                throw new ArgumentNullException(nameof(concept));

            if (concept.PracticeCount == 0)
                return 0.0;

            // Calculate average quality from learning events
            double qualityAvg = CalculateAverageQuality(concept.Events);

            // Exponential learning curve
            // Practice count and quality combine multiplicatively
            double exponent = -(concept.PracticeCount * qualityAvg / 10.0);
            double mastery = 1.0 - Math.Exp(exponent);

            return Math.Clamp(mastery, 0.0, 1.0);
        }

        /// <summary>
        /// Calculates mastery level from explicit parameters (for testing/prediction).
        /// </summary>
        /// <param name="practiceCount">Number of practice sessions.</param>
        /// <param name="averageQuality">Average quality score (0.0 to 1.0).</param>
        /// <returns>Predicted mastery level.</returns>
        public double CalculateMasteryLevel(int practiceCount, double averageQuality)
        {
            if (practiceCount < 0)
                throw new ArgumentException("Practice count must be non-negative", nameof(practiceCount));

            if (averageQuality < 0 || averageQuality > 1.0)
                throw new ArgumentException("Quality must be between 0 and 1", nameof(averageQuality));

            if (practiceCount == 0)
                return 0.0;

            double exponent = -(practiceCount * averageQuality / 10.0);
            double mastery = 1.0 - Math.Exp(exponent);

            return Math.Clamp(mastery, 0.0, 1.0);
        }

        /// <summary>
        /// Predicts practice count needed to reach target mastery level.
        /// Inverts the mastery formula: PracticeCount = -10 × ln(1 - M) / QualityAvg
        /// </summary>
        /// <param name="targetMastery">Target mastery level (0.0 to 1.0).</param>
        /// <param name="averageQuality">Expected average quality of practice.</param>
        /// <returns>Estimated practice count needed.</returns>
        public int PredictPracticeCountNeeded(double targetMastery, double averageQuality)
        {
            if (targetMastery < 0 || targetMastery >= 1.0)
                throw new ArgumentException("Target mastery must be between 0 and 1", nameof(targetMastery));

            if (averageQuality <= 0 || averageQuality > 1.0)
                throw new ArgumentException("Quality must be between 0 and 1", nameof(averageQuality));

            // Invert the exponential formula
            double practiceCount = -10.0 * Math.Log(1.0 - targetMastery) / averageQuality;

            return (int)Math.Ceiling(practiceCount);
        }

        /// <summary>
        /// Updates mastery level after a new learning event.
        /// Recalculates based on new practice count and quality average.
        /// </summary>
        /// <param name="currentMastery">Current mastery level.</param>
        /// <param name="currentPracticeCount">Current practice count.</param>
        /// <param name="newQualityScore">Quality score of new practice session.</param>
        /// <param name="previousQualityAverage">Previous average quality.</param>
        /// <returns>Updated mastery level.</returns>
        public double UpdateMasteryAfterPractice(
            double currentMastery,
            int currentPracticeCount,
            double newQualityScore,
            double previousQualityAverage)
        {
            // Calculate new average quality
            double totalQuality = previousQualityAverage * currentPracticeCount + newQualityScore;
            int newPracticeCount = currentPracticeCount + 1;
            double newQualityAverage = totalQuality / newPracticeCount;

            // Recalculate mastery with new values
            return CalculateMasteryLevel(newPracticeCount, newQualityAverage);
        }

        /// <summary>
        /// Calculates quality score for a learning event based on type and duration.
        /// Higher quality for active learning (Practice, Application, Breakthrough).
        /// </summary>
        /// <param name="eventType">Type of learning activity.</param>
        /// <param name="durationMinutes">Duration of practice session.</param>
        /// <returns>Quality score between 0.0 and 1.0.</returns>
        public double EstimateQualityScore(LearningEventType eventType, int durationMinutes)
        {
            // Base quality by event type (validated ranges from design)
            double baseQuality = eventType switch
            {
                LearningEventType.Practice => 0.85,      // High quality (0.8-1.0 range)
                LearningEventType.Application => 0.90,   // Very high (0.8-1.0 range)
                LearningEventType.Breakthrough => 0.95,  // Highest (0.9-1.0 range)
                LearningEventType.Teaching => 0.80,      // High (0.7-0.9 range)
                LearningEventType.Study => 0.55,         // Moderate (0.4-0.7 range)
                LearningEventType.Review => 0.60,        // Moderate (0.5-0.7 range)
                _ => 0.50
            };

            // Duration modifier: optimal practice is 30-60 minutes
            double durationFactor = 1.0;
            if (durationMinutes < 15)
            {
                // Too short: reduced effectiveness
                durationFactor = durationMinutes / 15.0;
            }
            else if (durationMinutes > 120)
            {
                // Too long: diminishing returns
                durationFactor = Math.Max(0.8, 120.0 / durationMinutes);
            }

            double quality = baseQuality * durationFactor;
            return Math.Clamp(quality, 0.0, 1.0);
        }

        /// <summary>
        /// Calculates average quality from learning events.
        /// Filters out events with zero quality (review/passive learning).
        /// </summary>
        private double CalculateAverageQuality(IEnumerable<LearningEvent> events)
        {
            if (events == null || !events.Any())
                return 0.5; // Default moderate quality if no history

            var qualityScores = events
                .Where(e => e.QualityScore > 0)
                .Select(e => e.QualityScore)
                .ToList();

            if (!qualityScores.Any())
                return 0.5;

            return qualityScores.Average();
        }

        /// <summary>
        /// Calculates decay in mastery due to lack of practice (forgetting curve).
        /// Formula: M_decayed = M × exp(-decayRate × daysSinceLastPractice / 365)
        /// </summary>
        /// <param name="currentMastery">Current mastery level.</param>
        /// <param name="daysSinceLastPractice">Days since last practice.</param>
        /// <param name="decayRate">Decay rate (default 0.3 = 30% decay per year).</param>
        /// <returns>Decayed mastery level.</returns>
        public double ApplyMasteryDecay(
            double currentMastery,
            double daysSinceLastPractice,
            double decayRate = 0.3)
        {
            if (currentMastery < 0 || currentMastery > 1.0)
                throw new ArgumentException("Mastery must be between 0 and 1", nameof(currentMastery));

            if (daysSinceLastPractice < 0)
                throw new ArgumentException("Days must be non-negative", nameof(daysSinceLastPractice));

            // Exponential decay over time
            double decayFactor = Math.Exp(-decayRate * daysSinceLastPractice / 365.0);
            double decayedMastery = currentMastery * decayFactor;

            return Math.Clamp(decayedMastery, 0.0, 1.0);
        }
    }
}
