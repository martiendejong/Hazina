namespace Hazina.Identity.Core.Models;

/// <summary>
/// UserRole represents a user having a role in a specific organization.
/// Roles are scoped to organizations (e.g., "Admin in Building A").
/// </summary>
public class UserRole
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }
    public Guid OrganizationId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public HazinaUser User { get; set; } = null!;
    public Role Role { get; set; } = null!;
    public Organization Organization { get; set; } = null!;
}
