namespace Hazina.Identity.Core.Models;

/// <summary>
/// Tenant represents a customer in the multi-tenant system.
/// Each tenant has complete data isolation.
/// </summary>
public class Tenant
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }

    // Navigation properties
    public ICollection<HazinaUser> Users { get; set; } = new List<HazinaUser>();
    public ICollection<Organization> Organizations { get; set; } = new List<Organization>();
}
