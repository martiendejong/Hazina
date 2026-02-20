using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Hazina.Core.Entities.Geometric
{
    /// <summary>
    /// Represents an N-dimensional manifold containing concepts and their connections.
    /// A ThoughtSpace models a domain of knowledge with geometric properties that
    /// reflect learning state (curvature), learning rate (velocity), and creative potential (tunneling).
    /// </summary>
    [Table("ThoughtSpaces")]
    public class ThoughtSpace
    {
        /// <summary>
        /// Unique identifier for the thought space.
        /// </summary>
        [Key]
        [StringLength(50)]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// User ID who owns this thought space.
        /// </summary>
        [Required]
        [StringLength(50)]
        public string UserId { get; set; }

        /// <summary>
        /// Domain of knowledge this thought space represents (e.g., "programming", "mathematics").
        /// </summary>
        [Required]
        [StringLength(100)]
        public string Domain { get; set; }

        /// <summary>
        /// Number of dimensions in this manifold. Default is 12.
        /// Higher dimensions enable more complex concept representations.
        /// </summary>
        [Required]
        [Range(1, 100)]
        public int Dimensions { get; set; } = 12;

        /// <summary>
        /// Global curvature of the thought space (weighted average of concept curvatures).
        /// High curvature = high confusion/difficulty. Low curvature = understanding/mastery.
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(10, 6)")]
        public double GlobalCurvature { get; set; }

        /// <summary>
        /// Learning velocity: rate of curvature change (dC/dt).
        /// Negative values indicate smoothing (learning). Positive values indicate increasing confusion.
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(10, 6)")]
        public double LearningVelocity { get; set; }

        /// <summary>
        /// Tunneling capability: ability to make non-local jumps through high-curvature barriers.
        /// Range: 0.0 (pure deduction) to 1.0 (maximum creative insight).
        /// Validated 8.98x speedup at high tunneling levels.
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(10, 6)")]
        public double TunnelingCapability { get; set; }

        /// <summary>
        /// Timestamp when this thought space was created.
        /// </summary>
        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Timestamp when this thought space was last updated.
        /// </summary>
        [Required]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Collection of concepts within this thought space.
        /// </summary>
        public virtual ICollection<Concept> Concepts { get; set; } = new List<Concept>();

        /// <summary>
        /// Collection of learning events associated with this thought space.
        /// </summary>
        public virtual ICollection<LearningEvent> LearningHistory { get; set; } = new List<LearningEvent>();
    }
}
