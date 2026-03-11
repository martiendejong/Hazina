using Hazina.Auth.Core.DTOs;

namespace Hazina.Auth.Core.Interfaces;

/// <summary>
/// Service for user authentication and registration
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Register a new user
    /// </summary>
    Task<AuthResponse> RegisterAsync(RegisterRequest request);

    /// <summary>
    /// Log in with email and password
    /// </summary>
    Task<AuthResponse> LoginAsync(LoginRequest request);

    /// <summary>
    /// Log in with OAuth provider
    /// </summary>
    Task<AuthResponse> OAuthLoginAsync(OAuthLoginRequest request);

    /// <summary>
    /// Refresh access token
    /// </summary>
    Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request);

    /// <summary>
    /// Get current user information
    /// </summary>
    Task<UserInfo?> GetCurrentUserAsync(string userId);

    /// <summary>
    /// Revoke refresh token (logout)
    /// </summary>
    Task RevokeTokenAsync(string userId);
}
