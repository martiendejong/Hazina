namespace Hazina.Core.Entities.Geometric
{
    /// <summary>
    /// Type of relationship between two concepts.
    /// </summary>
    public enum ConnectionType
    {
        /// <summary>
        /// FromConcept is a prerequisite for ToConcept.
        /// Must be learned first for optimal understanding.
        /// </summary>
        Prerequisite = 0,

        /// <summary>
        /// Concepts are related but neither depends on the other.
        /// Learning one may help understand the other.
        /// </summary>
        Related = 1,

        /// <summary>
        /// Concepts are opposites or contrasting ideas.
        /// Understanding the difference is important.
        /// </summary>
        Opposite = 2,

        /// <summary>
        /// FromConcept is an application or specific case of ToConcept.
        /// ToConcept is more general/abstract.
        /// </summary>
        Application = 3
    }
}
