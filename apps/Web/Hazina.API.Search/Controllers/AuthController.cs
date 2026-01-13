using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Hazina.API.Search.Controllers;

/// <summary>
/// Controller for authentication and token generation
/// </summary>
[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IConfiguration configuration,
        ILogger<AuthController> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Generate JWT token for API access
    /// </summary>
    [HttpPost("token")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<TokenResponse> GenerateToken([FromBody] LoginRequest request)
    {
        // In a production system, validate credentials against a user store
        // For now, use a simple API key validation
        var apiKey = _configuration["Authentication:ApiKey"];
        if (string.IsNullOrEmpty(apiKey) || request.ApiKey != apiKey)
        {
            _logger.LogWarning("Invalid API key attempt");
            return Unauthorized(new { error = "Invalid API key" });
        }

        var token = GenerateJwtToken(request.UserId ?? "api-user");

        _logger.LogInformation("Generated token for user {UserId}", request.UserId);

        return Ok(new TokenResponse
        {
            Token = token,
            ExpiresIn = 3600,
            TokenType = "Bearer"
        });
    }

    private string GenerateJwtToken(string userId)
    {
        var jwtSettings = _configuration.GetSection("Authentication:Jwt");
        var secretKey = jwtSettings["SecretKey"]
            ?? throw new InvalidOperationException("JWT secret key not configured");
        var issuer = jwtSettings["Issuer"] ?? "Hazina.API.Search";
        var audience = jwtSettings["Audience"] ?? "Hazina.API.Search.Clients";

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Name, userId),
            new Claim(ClaimTypes.Role, "User")
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public class LoginRequest
{
    public string ApiKey { get; set; } = string.Empty;
    public string? UserId { get; set; }
}

public class TokenResponse
{
    public string Token { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
    public string TokenType { get; set; } = string.Empty;
}
