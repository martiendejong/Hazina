using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Hazina.Core.Entities.Geometric
{
    /// <summary>
    /// Represents a directed connection between two concepts in a thought space.
    /// Connections define the topology of the knowledge manifold and affect curvature calculations.
    /// </summary>
    [Table("ConceptConnections")]
    public class ConceptConnection
    {
        /// <summary>
        /// Unique identifier for the connection.
        /// </summary>
        [Key]
        [StringLength(50)]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Foreign key to the source concept.
        /// </summary>
        [Required]
        [StringLength(50)]
        public string FromConceptId { get; set; }

        /// <summary>
        /// Foreign key to the target concept.
        /// </summary>
        [Required]
        [StringLength(50)]
        public string ToConceptId { get; set; }

        /// <summary>
        /// Type of connection (Prerequisite, Related, Opposite, Application).
        /// </summary>
        [Required]
        public ConnectionType Type { get; set; }

        /// <summary>
        /// Strength of this connection (0.0 = weak, 1.0 = strong).
        /// Affects weighting in curvature calculations and path finding.
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(10, 6)")]
        public double Strength { get; set; }

        /// <summary>
        /// Optional description of why this connection exists.
        /// </summary>
        [StringLength(500)]
        public string Reason { get; set; }

        /// <summary>
        /// Timestamp when this connection was created.
        /// </summary>
        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Navigation property to the source concept.
        /// </summary>
        public virtual Concept FromConcept { get; set; }

        /// <summary>
        /// Navigation property to the target concept.
        /// </summary>
        public virtual Concept ToConcept { get; set; }
    }
}
