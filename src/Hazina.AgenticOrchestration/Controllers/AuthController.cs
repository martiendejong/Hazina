using Hazina.AgenticOrchestration.Models;
using Hazina.AgenticOrchestration.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace Hazina.AgenticOrchestration.Controllers;

/// <summary>
/// Authentication controller for JWT-based login
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly JwtService _jwtService;
    private readonly RefreshTokenStore _tokenStore;
    private readonly IConfiguration _configuration;
    private readonly string _configuredUsername;
    private readonly string _configuredPassword;

    public AuthController(
        JwtService jwtService,
        RefreshTokenStore tokenStore,
        IConfiguration configuration)
    {
        _jwtService = jwtService;
        _tokenStore = tokenStore;
        _configuration = configuration;

        var authConfig = configuration.GetSection("Authentication");
        _configuredUsername = authConfig["Username"] ?? "admin";
        _configuredPassword = authConfig["Password"] ?? string.Empty;
    }

    /// <summary>
    /// Login with username and password to receive JWT tokens
    /// </summary>
    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        // Validate credentials against configuration
        if (string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
        {
            return BadRequest(new { message = "Username and password are required" });
        }

        if (request.Username != _configuredUsername || request.Password != _configuredPassword)
        {
            return Unauthorized(new { message = "Invalid username or password" });
        }

        // Generate tokens
        var accessToken = _jwtService.GenerateAccessToken(request.Username);
        var refreshToken = _jwtService.GenerateRefreshToken();
        var accessTokenExpiry = _jwtService.GetAccessTokenExpiry();
        var refreshTokenExpiry = _jwtService.GetRefreshTokenExpiry();

        // Store refresh token
        _tokenStore.StoreToken(refreshToken, request.Username, refreshTokenExpiry);

        return Ok(new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            AccessTokenExpiry = accessTokenExpiry,
            RefreshTokenExpiry = refreshTokenExpiry
        });
    }

    /// <summary>
    /// Refresh access token using a valid refresh token
    /// </summary>
    [HttpPost("refresh")]
    public IActionResult Refresh([FromBody] RefreshTokenRequest request)
    {
        if (string.IsNullOrEmpty(request.RefreshToken))
        {
            return BadRequest(new { message = "Refresh token is required" });
        }

        // Validate refresh token
        if (!_tokenStore.ValidateToken(request.RefreshToken, out var username))
        {
            return Unauthorized(new { message = "Invalid or expired refresh token" });
        }

        // Generate new tokens
        var accessToken = _jwtService.GenerateAccessToken(username!);
        var newRefreshToken = _jwtService.GenerateRefreshToken();
        var accessTokenExpiry = _jwtService.GetAccessTokenExpiry();
        var refreshTokenExpiry = _jwtService.GetRefreshTokenExpiry();

        // Revoke old refresh token and store new one
        _tokenStore.RevokeToken(request.RefreshToken);
        _tokenStore.StoreToken(newRefreshToken, username!, refreshTokenExpiry);

        return Ok(new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = newRefreshToken,
            AccessTokenExpiry = accessTokenExpiry,
            RefreshTokenExpiry = refreshTokenExpiry
        });
    }

    /// <summary>
    /// Check authentication status (requires valid JWT token)
    /// </summary>
    [HttpGet("status")]
    [Authorize]
    public IActionResult Status()
    {
        var username = User.Identity?.Name;
        return Ok(new { authenticated = true, username });
    }

    /// <summary>
    /// Revoke a refresh token (logout)
    /// </summary>
    [HttpPost("revoke")]
    public IActionResult Revoke([FromBody] RefreshTokenRequest request)
    {
        if (string.IsNullOrEmpty(request.RefreshToken))
        {
            return BadRequest(new { message = "Refresh token is required" });
        }

        _tokenStore.RevokeToken(request.RefreshToken);
        return Ok(new { message = "Token revoked successfully" });
    }
}
