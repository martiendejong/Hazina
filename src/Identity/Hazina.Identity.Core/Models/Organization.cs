namespace Hazina.Identity.Core.Models;

/// <summary>
/// Organization represents a hierarchical entity (Agency, Building, Floor, Room, Device).
/// Organizations can have parent-child relationships to model building structures.
/// </summary>
public class Organization
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? ParentId { get; set; }
    public required string Name { get; set; }
    public required string Type { get; set; } // 'agency', 'building', 'floor', 'room', 'device'
    public string? Metadata { get; set; } // JSON for custom attributes
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }

    // Navigation properties
    public Tenant Tenant { get; set; } = null!;
    public Organization? Parent { get; set; }
    public ICollection<Organization> Children { get; set; } = new List<Organization>();
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();

    /// <summary>
    /// Soft delete the organization.
    /// </summary>
    public void Delete()
    {
        DeletedAt = DateTime.UtcNow;
    }
}
