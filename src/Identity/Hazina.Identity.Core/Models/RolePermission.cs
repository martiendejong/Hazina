namespace Hazina.Identity.Core.Models;

/// <summary>
/// RolePermission maps which permissions a role has.
/// </summary>
public class RolePermission
{
    public Guid RoleId { get; set; }
    public Guid PermissionId { get; set; }

    // Navigation properties
    public Role Role { get; set; } = null!;
    public Permission Permission { get; set; } = null!;
}
