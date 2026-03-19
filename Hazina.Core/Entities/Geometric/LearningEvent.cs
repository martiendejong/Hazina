using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Hazina.Core.Entities.Geometric
{
    /// <summary>
    /// Records a learning activity for a concept.
    /// Learning events drive mastery calculation and curvature updates via Ricci flow.
    /// </summary>
    [Table("LearningEvents")]
    public class LearningEvent
    {
        /// <summary>
        /// Unique identifier for the learning event.
        /// </summary>
        [Key]
        [StringLength(50)]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Foreign key to the thought space this event belongs to.
        /// </summary>
        [Required]
        [StringLength(50)]
        public string ThoughtSpaceId { get; set; }

        /// <summary>
        /// Foreign key to the concept that was practiced.
        /// </summary>
        [Required]
        [StringLength(50)]
        public string ConceptId { get; set; }

        /// <summary>
        /// Type of learning activity (Practice, Study, Breakthrough, etc.).
        /// </summary>
        [Required]
        public LearningEventType Type { get; set; }

        /// <summary>
        /// Duration of the learning session in minutes.
        /// </summary>
        [Required]
        public int DurationMinutes { get; set; }

        /// <summary>
        /// Quality of the learning session (0.0 = poor, 1.0 = excellent).
        /// Used in mastery calculation: M = 1 - exp(-PracticeCount × QualityAvg / 10)
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(10, 6)")]
        public double QualityScore { get; set; }

        /// <summary>
        /// Concept's local curvature before this learning event.
        /// Enables tracking curvature reduction over time.
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(10, 6)")]
        public double CurvatureBefore { get; set; }

        /// <summary>
        /// Concept's local curvature after this learning event.
        /// Should be lower than CurvatureBefore for effective learning.
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(10, 6)")]
        public double CurvatureAfter { get; set; }

        /// <summary>
        /// Optional notes about this learning session.
        /// </summary>
        [StringLength(1000)]
        public string Notes { get; set; }

        /// <summary>
        /// Timestamp when this learning event occurred.
        /// </summary>
        [Required]
        public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Navigation property to the thought space.
        /// </summary>
        public virtual ThoughtSpace ThoughtSpace { get; set; }

        /// <summary>
        /// Navigation property to the concept.
        /// </summary>
        public virtual Concept Concept { get; set; }
    }
}
