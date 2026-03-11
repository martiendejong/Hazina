using Microsoft.AspNetCore.Identity;

namespace Hazina.Auth.Core.Models;

/// <summary>
/// Application user extending IdentityUser with custom properties
/// </summary>
public class ApplicationUser : IdentityUser
{
    /// <summary>
    /// User's full name
    /// </summary>
    public string? FullName { get; set; }

    /// <summary>
    /// User's profile picture URL
    /// </summary>
    public string? ProfilePictureUrl { get; set; }

    /// <summary>
    /// OAuth provider (Google, Microsoft, GitHub, etc.)
    /// </summary>
    public string? OAuthProvider { get; set; }

    /// <summary>
    /// OAuth provider user ID
    /// </summary>
    public string? OAuthProviderId { get; set; }

    /// <summary>
    /// Date when user was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Date when user last logged in
    /// </summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>
    /// Whether user account is active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Refresh token for JWT authentication
    /// </summary>
    public string? RefreshToken { get; set; }

    /// <summary>
    /// Refresh token expiry date
    /// </summary>
    public DateTime? RefreshTokenExpiryTime { get; set; }
}
