namespace Hazina.Identity.Core.Models;

/// <summary>
/// Role represents a named collection of permissions.
/// System-wide roles: Admin, Manager, Agent, Viewer.
/// </summary>
public class Role
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();

    // Well-known role IDs (matching database seeds)
    public static readonly Guid AdminRoleId = new("00000000-0000-0000-0000-000000000001");
    public static readonly Guid ManagerRoleId = new("00000000-0000-0000-0000-000000000002");
    public static readonly Guid AgentRoleId = new("00000000-0000-0000-0000-000000000003");
    public static readonly Guid ViewerRoleId = new("00000000-0000-0000-0000-000000000004");
}
