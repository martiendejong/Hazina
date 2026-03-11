using System.Text;
using Hazina.Auth.Core.Interfaces;
using Hazina.Auth.Core.Models;
using Hazina.Auth.Identity.Data;
using Hazina.Auth.Identity.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Hazina.Auth.Identity.Configuration;

public static class HazinaAuthExtensions
{
    /// <summary>
    /// Add Hazina authentication with Identity, JWT, and optional OAuth providers
    /// </summary>
    public static IServiceCollection AddHazinaAuth(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<HazinaAuthOptions>? configureOptions = null)
    {
        var options = new HazinaAuthOptions();
        configureOptions?.Invoke(options);

        // Add DbContext
        services.AddDbContext<ApplicationDbContext>(opts =>
        {
            if (options.DbContextOptions != null)
            {
                options.DbContextOptions(opts);
            }
            else
            {
                // Default to SQLite if not configured
                opts.UseSqlite(configuration.GetConnectionString("DefaultConnection")
                    ?? "Data Source=app.db");
            }
        });

        // Add Identity
        services.AddIdentity<ApplicationUser, IdentityRole>(opts =>
        {
            opts.Password.RequireDigit = options.RequireDigit;
            opts.Password.RequireLowercase = options.RequireLowercase;
            opts.Password.RequireUppercase = options.RequireUppercase;
            opts.Password.RequireNonAlphanumeric = options.RequireNonAlphanumeric;
            opts.Password.RequiredLength = options.RequiredPasswordLength;
            opts.User.RequireUniqueEmail = true;
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

        // Add JWT Authentication
        var jwtSecret = configuration["JWT:Secret"]
            ?? throw new InvalidOperationException("JWT:Secret not configured");

        services.AddAuthentication(opts =>
        {
            opts.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            opts.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(opts =>
        {
            opts.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = configuration["JWT:Issuer"] ?? "HazinaAuth",
                ValidAudience = configuration["JWT:Audience"] ?? "HazinaAuthUsers",
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
            };
        });

        // Add Google OAuth if configured
        if (options.UseGoogleOAuth)
        {
            services.AddAuthentication()
                .AddGoogle(opts =>
                {
                    opts.ClientId = configuration["Google:ClientId"]
                        ?? throw new InvalidOperationException("Google:ClientId not configured");
                    opts.ClientSecret = configuration["Google:ClientSecret"]
                        ?? throw new InvalidOperationException("Google:ClientSecret not configured");
                });
        }

        // Add Microsoft OAuth if configured
        if (options.UseMicrosoftOAuth)
        {
            services.AddAuthentication()
                .AddMicrosoftAccount(opts =>
                {
                    opts.ClientId = configuration["Microsoft:ClientId"]
                        ?? throw new InvalidOperationException("Microsoft:ClientId not configured");
                    opts.ClientSecret = configuration["Microsoft:ClientSecret"]
                        ?? throw new InvalidOperationException("Microsoft:ClientSecret not configured");
                });
        }

        // Register services
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IAuthService, AuthService>();

        return services;
    }

    /// <summary>
    /// Use Hazina authentication middleware
    /// </summary>
    public static IApplicationBuilder UseHazinaAuth(this IApplicationBuilder app)
    {
        app.UseAuthentication();
        app.UseAuthorization();
        return app;
    }
}

/// <summary>
/// Configuration options for Hazina authentication
/// </summary>
public class HazinaAuthOptions
{
    /// <summary>
    /// Custom DbContext configuration
    /// </summary>
    public Action<DbContextOptionsBuilder>? DbContextOptions { get; set; }

    /// <summary>
    /// Enable Google OAuth provider
    /// </summary>
    public bool UseGoogleOAuth { get; set; }

    /// <summary>
    /// Enable Microsoft OAuth provider
    /// </summary>
    public bool UseMicrosoftOAuth { get; set; }

    // Password requirements
    public bool RequireDigit { get; set; } = true;
    public bool RequireLowercase { get; set; } = true;
    public bool RequireUppercase { get; set; } = true;
    public bool RequireNonAlphanumeric { get; set; } = false;
    public int RequiredPasswordLength { get; set; } = 6;
}
