namespace Hazina.Identity.Core.Models;

/// <summary>
/// RefreshToken stores JWT refresh tokens for users.
/// Allows users to obtain new access tokens without re-authenticating.
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public required string Token { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAt { get; set; }

    // Navigation properties
    public HazinaUser User { get; set; } = null!;

    /// <summary>
    /// Check if token is still valid (not expired, not revoked).
    /// </summary>
    public bool IsValid => !IsRevoked && ExpiresAt > DateTime.UtcNow;

    /// <summary>
    /// Revoke the refresh token.
    /// </summary>
    public void Revoke()
    {
        IsRevoked = true;
        RevokedAt = DateTime.UtcNow;
    }
}
