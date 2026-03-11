using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Hazina.Demo.AgenticOrchestration.Authentication;

/// <summary>
/// Basic HTTP Authentication handler.
/// Validates credentials from the Authorization header against configuration.
/// </summary>
public class BasicAuthenticationHandler : AuthenticationHandler<BasicAuthenticationOptions>
{
    public BasicAuthenticationHandler(
        IOptionsMonitor<BasicAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Check if authentication is enabled
        if (!Options.Enabled)
        {
            // If disabled, auto-authenticate everyone
            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Name, "anonymous"),
                new Claim(ClaimTypes.Role, "User")
            }, Scheme.Name);

            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }

        // Check for Authorization header
        if (!Request.Headers.ContainsKey("Authorization"))
        {
            return Task.FromResult(AuthenticateResult.Fail("Missing Authorization header"));
        }

        try
        {
            var authHeader = AuthenticationHeaderValue.Parse(Request.Headers.Authorization!);

            if (authHeader.Scheme != "Basic")
            {
                return Task.FromResult(AuthenticateResult.Fail("Invalid authentication scheme"));
            }

            if (string.IsNullOrEmpty(authHeader.Parameter))
            {
                return Task.FromResult(AuthenticateResult.Fail("Missing credentials"));
            }

            var credentialBytes = Convert.FromBase64String(authHeader.Parameter);
            var credentials = Encoding.UTF8.GetString(credentialBytes).Split(':', 2);

            if (credentials.Length != 2)
            {
                return Task.FromResult(AuthenticateResult.Fail("Invalid credentials format"));
            }

            var username = credentials[0];
            var password = credentials[1];

            // Validate against configured credentials
            if (username != Options.Username || password != Options.Password)
            {
                Logger.LogWarning("Invalid login attempt for user: {Username}", username);
                return Task.FromResult(AuthenticateResult.Fail("Invalid username or password"));
            }

            Logger.LogInformation("User {Username} authenticated successfully", username);

            var claims = new[]
            {
                new Claim(ClaimTypes.Name, username),
                new Claim(ClaimTypes.Role, "User"),
                new Claim(ClaimTypes.Role, "Admin")
            };

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
        catch (FormatException)
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid Base64 encoding"));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Authentication error");
            return Task.FromResult(AuthenticateResult.Fail("Authentication error"));
        }
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        if (Options.Enabled)
        {
            Response.StatusCode = 401;
            Response.Headers.WWWAuthenticate = $"Basic realm=\"{Options.Realm}\"";
        }
        return Task.CompletedTask;
    }
}

/// <summary>
/// Configuration options for basic authentication
/// </summary>
public class BasicAuthenticationOptions : AuthenticationSchemeOptions
{
    /// <summary>
    /// Whether authentication is enabled. If false, all requests are allowed.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// The username required for authentication
    /// </summary>
    public string Username { get; set; } = "admin";

    /// <summary>
    /// The password required for authentication
    /// </summary>
    public string Password { get; set; } = "changeme";

    /// <summary>
    /// The realm shown in the browser's login dialog
    /// </summary>
    public string Realm { get; set; } = "Hazina Agentic Orchestration";
}

/// <summary>
/// Extension methods for registering basic authentication
/// </summary>
public static class BasicAuthenticationExtensions
{
    public const string SchemeName = "BasicAuthentication";

    public static AuthenticationBuilder AddBasicAuthentication(
        this AuthenticationBuilder builder,
        Action<BasicAuthenticationOptions>? configureOptions = null)
    {
        return builder.AddScheme<BasicAuthenticationOptions, BasicAuthenticationHandler>(
            SchemeName, configureOptions);
    }
}
