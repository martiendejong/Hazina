using System;

namespace Hazina.Tools.Data.Interfaces
{
    /// <summary>
    /// Interface for entities that support soft delete (logical delete).
    /// Entities implementing this interface will be filtered automatically by EF Core query filters.
    /// </summary>
    public interface ISoftDeletable
    {
        /// <summary>
        /// Indicates whether the entity has been soft-deleted
        /// </summary>
        bool IsDeleted { get; set; }

        /// <summary>
        /// Timestamp when the entity was soft-deleted (null if not deleted)
        /// </summary>
        DateTime? DeletedAt { get; set; }

        /// <summary>
        /// User ID who deleted the entity (optional, for audit purposes)
        /// </summary>
        string? DeletedBy { get; set; }
    }
}
