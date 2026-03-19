namespace Hazina.Auth.Core.DTOs;

/// <summary>
/// Request to register a new user
/// </summary>
public record RegisterRequest(
    string Email,
    string Password,
    string? FullName = null
);

/// <summary>
/// Request to log in with email and password
/// </summary>
public record LoginRequest(
    string Email,
    string Password
);

/// <summary>
/// Request to log in with OAuth provider
/// </summary>
public record OAuthLoginRequest(
    string Provider,
    string AccessToken,
    string? Email = null,
    string? FullName = null,
    string? ProfilePictureUrl = null
);

/// <summary>
/// Request to refresh JWT access token
/// </summary>
public record RefreshTokenRequest(
    string AccessToken,
    string RefreshToken
);

/// <summary>
/// Successful authentication response
/// </summary>
public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    UserInfo User
);

/// <summary>
/// User information
/// </summary>
public record UserInfo(
    string Id,
    string Email,
    string? FullName,
    string? ProfilePictureUrl,
    string[]? Roles
);

/// <summary>
/// Error response
/// </summary>
public record ErrorResponse(
    string Message,
    string[]? Errors = null
);
