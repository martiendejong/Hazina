using System.Security.Claims;
using HtmlMockupGenerator.Services;

namespace HtmlMockupGenerator.Middleware;

public class GoogleClaimsMiddleware
{
    private readonly RequestDelegate _next;

    public GoogleClaimsMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, UserService userService)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var claims = context.User.Claims.ToList();
            
            // Map Google OAuth claims to standard claims
            var email = claims.FirstOrDefault(c => c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress")?.Value
                       ?? claims.FirstOrDefault(c => c.Type == "email")?.Value;
            
            var name = claims.FirstOrDefault(c => c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name")?.Value
                      ?? claims.FirstOrDefault(c => c.Type == "name")?.Value;
            
            var picture = claims.FirstOrDefault(c => c.Type == "picture")?.Value;
            
            var userId = claims.FirstOrDefault(c => c.Type == "sub")?.Value
                        ?? claims.FirstOrDefault(c => c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;

            if (!string.IsNullOrEmpty(userId) && !string.IsNullOrEmpty(email) && !string.IsNullOrEmpty(name))
            {
                // Get or create user in database
                await userService.GetOrCreateUserAsync(userId, email, name, picture);
                
                // Add claims to the current user if they don't exist
                if (!context.User.HasClaim(c => c.Type == "email"))
                {
                    var identity = context.User.Identity as ClaimsIdentity;
                    identity?.AddClaim(new Claim("email", email));
                }
                
                if (!context.User.HasClaim(c => c.Type == "name"))
                {
                    var identity = context.User.Identity as ClaimsIdentity;
                    identity?.AddClaim(new Claim("name", name));
                }
                
                if (!string.IsNullOrEmpty(picture) && !context.User.HasClaim(c => c.Type == "picture"))
                {
                    var identity = context.User.Identity as ClaimsIdentity;
                    identity?.AddClaim(new Claim("picture", picture));
                }
            }
        }

        await _next(context);
    }
}

public static class GoogleClaimsMiddlewareExtensions
{
    public static IApplicationBuilder UseGoogleClaims(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<GoogleClaimsMiddleware>();
    }
} 