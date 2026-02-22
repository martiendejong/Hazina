using System.Security.Claims;
using Hazina.Auth.Core.DTOs;
using Hazina.Auth.Core.Interfaces;
using Hazina.Auth.Core.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Hazina.Auth.Identity.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IJwtService _jwtService;

    public AuthService(UserManager<ApplicationUser> userManager, IJwtService jwtService)
    {
        _userManager = userManager;
        _jwtService = jwtService;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser != null)
            throw new InvalidOperationException("User with this email already exists");

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FullName = request.FullName,
            EmailConfirmed = true // Auto-confirm for now
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to create user: {errors}");
        }

        return await GenerateAuthResponseAsync(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null || !await _userManager.CheckPasswordAsync(user, request.Password))
            throw new UnauthorizedAccessException("Invalid email or password");

        if (!user.IsActive)
            throw new UnauthorizedAccessException("Account is disabled");

        user.LastLoginAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        return await GenerateAuthResponseAsync(user);
    }

    public async Task<AuthResponse> OAuthLoginAsync(OAuthLoginRequest request)
    {
        // Find user by OAuth provider + provider ID
        var user = await _userManager.Users
            .FirstOrDefaultAsync(u => u.OAuthProvider == request.Provider
                                   && u.OAuthProviderId == request.AccessToken);

        if (user == null && request.Email != null)
        {
            // Try to find by email (user might exist but not linked to OAuth)
            user = await _userManager.FindByEmailAsync(request.Email);
        }

        if (user == null)
        {
            // Create new user from OAuth
            user = new ApplicationUser
            {
                UserName = request.Email ?? $"{request.Provider}_{Guid.NewGuid():N}",
                Email = request.Email,
                FullName = request.FullName,
                ProfilePictureUrl = request.ProfilePictureUrl,
                OAuthProvider = request.Provider,
                OAuthProviderId = request.AccessToken,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to create OAuth user: {errors}");
            }
        }
        else if (user.OAuthProvider == null)
        {
            // Link existing user to OAuth provider
            user.OAuthProvider = request.Provider;
            user.OAuthProviderId = request.AccessToken;
            user.ProfilePictureUrl ??= request.ProfilePictureUrl;
            await _userManager.UpdateAsync(user);
        }

        if (!user.IsActive)
            throw new UnauthorizedAccessException("Account is disabled");

        user.LastLoginAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        return await GenerateAuthResponseAsync(user);
    }

    public async Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request)
    {
        var principal = _jwtService.GetPrincipalFromExpiredToken(request.AccessToken);
        if (principal == null)
            throw new UnauthorizedAccessException("Invalid access token");

        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
            throw new UnauthorizedAccessException("Invalid token claims");

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null || user.RefreshToken != request.RefreshToken)
            throw new UnauthorizedAccessException("Invalid refresh token");

        if (user.RefreshTokenExpiryTime <= DateTime.UtcNow)
            throw new UnauthorizedAccessException("Refresh token expired");

        return await GenerateAuthResponseAsync(user);
    }

    public async Task<UserInfo?> GetCurrentUserAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return null;

        var roles = await _userManager.GetRolesAsync(user);

        return new UserInfo(
            user.Id,
            user.Email ?? "",
            user.FullName,
            user.ProfilePictureUrl,
            roles.ToArray()
        );
    }

    public async Task RevokeTokenAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user != null)
        {
            user.RefreshToken = null;
            user.RefreshTokenExpiryTime = null;
            await _userManager.UpdateAsync(user);
        }
    }

    private async Task<AuthResponse> GenerateAuthResponseAsync(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Email, user.Email ?? ""),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (user.FullName != null)
            claims.Add(new Claim(ClaimTypes.Name, user.FullName));

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var accessToken = _jwtService.GenerateAccessToken(claims);
        var refreshToken = _jwtService.GenerateRefreshToken();

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7); // Refresh token valid for 7 days
        await _userManager.UpdateAsync(user);

        return new AuthResponse(
            accessToken,
            refreshToken,
            DateTime.UtcNow.AddHours(1), // Match JWT expiration
            new UserInfo(
                user.Id,
                user.Email ?? "",
                user.FullName,
                user.ProfilePictureUrl,
                roles.ToArray()
            )
        );
    }
}
