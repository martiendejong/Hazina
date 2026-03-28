namespace Hazina.Identity.Core.Models;

/// <summary>
/// AuditLog records all actions performed in the system for compliance and security.
/// </summary>
public class AuditLog
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? UserId { get; set; }
    public required string Action { get; set; } // 'user.created', 'user.login', etc.
    public string? ResourceType { get; set; } // 'user', 'organization', 'role'
    public Guid? ResourceId { get; set; }
    public string? Details { get; set; } // JSON
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Tenant Tenant { get; set; } = null!;
    public HazinaUser? User { get; set; }
}
