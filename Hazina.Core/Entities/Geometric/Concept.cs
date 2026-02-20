using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace Hazina.Core.Entities.Geometric
{
    /// <summary>
    /// Represents a concept as a point in an N-dimensional thought space manifold.
    /// Concepts have geometric properties (position, curvature) and learning state (mastery, practice count).
    /// </summary>
    [Table("Concepts")]
    public class Concept
    {
        /// <summary>
        /// Unique identifier for the concept record.
        /// </summary>
        [Key]
        [StringLength(50)]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Foreign key to the thought space containing this concept.
        /// </summary>
        [Required]
        [StringLength(50)]
        public string ThoughtSpaceId { get; set; }

        /// <summary>
        /// Domain-specific concept identifier (e.g., "variables", "functions", "algorithms").
        /// </summary>
        [Required]
        [StringLength(100)]
        public string ConceptId { get; set; }

        /// <summary>
        /// Human-readable name of the concept.
        /// </summary>
        [Required]
        [StringLength(200)]
        public string Name { get; set; }

        /// <summary>
        /// Description of what this concept represents.
        /// </summary>
        [StringLength(1000)]
        public string Description { get; set; }

        /// <summary>
        /// Intrinsic complexity/confusion of this concept (0.0 to 10.0).
        /// Independent of learner's mastery level.
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(10, 6)")]
        public double BaseConfusion { get; set; }

        /// <summary>
        /// Current mastery level (0.0 = no understanding, 1.0 = complete mastery).
        /// Calculated from practice count and quality via exponential formula.
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(10, 6)")]
        public double MasteryLevel { get; set; }

        /// <summary>
        /// Local curvature at this concept point.
        /// Calculated from BaseConfusion, MasteryLevel, recency, and connection complexity.
        /// Formula: BaseConfusion × (1 - Mastery) × RecencyFactor × ConnectionComplexity
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(10, 6)")]
        public double LocalCurvature { get; set; }

        /// <summary>
        /// Number of times this concept has been practiced.
        /// </summary>
        [Required]
        public int PracticeCount { get; set; }

        /// <summary>
        /// JSON-serialized position vector in N-dimensional space.
        /// Array length must match ThoughtSpace.Dimensions.
        /// </summary>
        [Required]
        [Column(TypeName = "nvarchar(max)")]
        public string PositionJson { get; set; }

        /// <summary>
        /// Position vector as double array (not mapped to database).
        /// Serialized/deserialized via PositionJson.
        /// </summary>
        [NotMapped]
        public double[] Position
        {
            get => string.IsNullOrEmpty(PositionJson)
                ? Array.Empty<double>()
                : JsonSerializer.Deserialize<double[]>(PositionJson) ?? Array.Empty<double>();
            set => PositionJson = JsonSerializer.Serialize(value);
        }

        /// <summary>
        /// Timestamp when this concept was last practiced.
        /// Used to calculate recency factor in curvature formula.
        /// </summary>
        public DateTime? LastPracticedAt { get; set; }

        /// <summary>
        /// Timestamp when this concept was created.
        /// </summary>
        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Timestamp when this concept was last updated.
        /// </summary>
        [Required]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Navigation property to the containing thought space.
        /// </summary>
        public virtual ThoughtSpace ThoughtSpace { get; set; }

        /// <summary>
        /// Collection of connections originating from this concept.
        /// </summary>
        public virtual ICollection<ConceptConnection> OutgoingConnections { get; set; } = new List<ConceptConnection>();

        /// <summary>
        /// Collection of connections pointing to this concept.
        /// </summary>
        public virtual ICollection<ConceptConnection> IncomingConnections { get; set; } = new List<ConceptConnection>();

        /// <summary>
        /// Collection of learning events for this concept.
        /// </summary>
        public virtual ICollection<LearningEvent> Events { get; set; } = new List<LearningEvent>();
    }
}
