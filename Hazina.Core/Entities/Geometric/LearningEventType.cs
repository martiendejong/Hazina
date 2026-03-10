namespace Hazina.Core.Entities.Geometric
{
    /// <summary>
    /// Type of learning activity performed.
    /// </summary>
    public enum LearningEventType
    {
        /// <summary>
        /// Deliberate practice exercises.
        /// Highest quality score potential (0.8-1.0).
        /// </summary>
        Practice = 0,

        /// <summary>
        /// Reading, watching tutorials, passive learning.
        /// Moderate quality score (0.4-0.7).
        /// </summary>
        Study = 1,

        /// <summary>
        /// Sudden insight or "aha" moment.
        /// Very high quality score (0.9-1.0).
        /// Often involves tunneling through curvature barriers.
        /// </summary>
        Breakthrough = 2,

        /// <summary>
        /// Teaching or explaining the concept to others.
        /// High quality score (0.7-0.9).
        /// Deepens understanding through articulation.
        /// </summary>
        Teaching = 3,

        /// <summary>
        /// Applying the concept to solve real problems.
        /// Very high quality score (0.8-1.0).
        /// Validates understanding through use.
        /// </summary>
        Application = 4,

        /// <summary>
        /// Reviewing previously learned material.
        /// Moderate quality score (0.5-0.7).
        /// Maintains mastery and prevents forgetting.
        /// </summary>
        Review = 5
    }
}
