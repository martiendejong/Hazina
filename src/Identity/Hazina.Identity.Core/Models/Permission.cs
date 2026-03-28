namespace Hazina.Identity.Core.Models;

/// <summary>
/// Permission represents a specific action on a resource.
/// Example: users.create, organizations.read, etc.
/// </summary>
public class Permission
{
    public Guid Id { get; set; }
    public required string Resource { get; set; } // 'users', 'organizations', etc.
    public required string Action { get; set; } // 'create', 'read', 'update', 'delete', 'list'
    public string? Description { get; set; }

    // Navigation properties
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();

    /// <summary>
    /// Full permission string (e.g., "users.create").
    /// </summary>
    public string FullPermission => $"{Resource}.{Action}";
}
