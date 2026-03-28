using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Hazina.Identity.Core.Models;
using Hazina.Identity.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Database=hazina_identity;Username=postgres;Password=postgres";

builder.Services.AddDbContext<IdentityDbContext>(options =>
    options.UseNpgsql(connectionString));

// JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? "your-super-secret-key-at-least-32-characters-long!";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "HazinaIdentity";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "HazinaIdentityClients";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS (allow frontend)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();

// ============================================================================
// AUTH ENDPOINTS
// ============================================================================

app.MapPost("/api/auth/login", async (LoginRequest request, IdentityDbContext db) =>
{
    // Find user by email
    var user = await db.Users
        .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
        .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Organization)
        .FirstOrDefaultAsync(u => u.Email == request.Email);

    if (user == null || !user.IsActive)
        return Results.Unauthorized();

    // Validate password
    if (!user.ValidatePassword(request.Password))
        return Results.Unauthorized();

    // Update last login
    user.LastLoginAt = DateTime.UtcNow;
    await db.SaveChangesAsync();

    // Generate access token (1 hour)
    var accessToken = GenerateJwtToken(user, jwtKey, jwtIssuer, jwtAudience, expiresInMinutes: 60);

    // Generate refresh token (30 days)
    var refreshToken = new RefreshToken
    {
        UserId = user.Id,
        Token = GenerateRefreshToken(),
        ExpiresAt = DateTime.UtcNow.AddDays(30)
    };
    db.RefreshTokens.Add(refreshToken);

    // Audit log
    var auditLog = new AuditLog
    {
        TenantId = user.TenantId,
        UserId = user.Id,
        Action = "user.login",
        ResourceType = "user",
        ResourceId = user.Id,
        Details = $"{{\"email\":\"{user.Email}\"}}"
    };
    db.AuditLogs.Add(auditLog);

    await db.SaveChangesAsync();

    return Results.Ok(new LoginResponse
    {
        AccessToken = accessToken,
        RefreshToken = refreshToken.Token,
        ExpiresIn = 3600,
        User = new UserDto
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Roles = user.UserRoles.Select(ur => new RoleDto
            {
                Name = ur.Role.Name,
                OrganizationId = ur.OrganizationId,
                OrganizationName = ur.Organization.Name
            }).ToList()
        }
    });
});

app.MapPost("/api/auth/refresh", async (RefreshRequest request, IdentityDbContext db) =>
{
    var refreshToken = await db.RefreshTokens
        .Include(rt => rt.User)
            .ThenInclude(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
        .Include(rt => rt.User)
            .ThenInclude(u => u.UserRoles)
                .ThenInclude(ur => ur.Organization)
        .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken);

    if (refreshToken == null || !refreshToken.IsValid)
        return Results.Unauthorized();

    var user = refreshToken.User;

    // Generate new access token
    var accessToken = GenerateJwtToken(user, jwtKey, jwtIssuer, jwtAudience, expiresInMinutes: 60);

    return Results.Ok(new RefreshResponse
    {
        AccessToken = accessToken,
        ExpiresIn = 3600
    });
});

app.MapPost("/api/auth/logout", async (LogoutRequest request, IdentityDbContext db) =>
{
    var refreshToken = await db.RefreshTokens
        .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken);

    if (refreshToken != null)
    {
        refreshToken.Revoke();
        await db.SaveChangesAsync();
    }

    return Results.Ok(new { message = "Logged out successfully" });
});

app.MapGet("/api/auth/me", async (HttpContext context, IdentityDbContext db) =>
{
    var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    if (string.IsNullOrEmpty(userId))
        return Results.Unauthorized();

    var user = await db.Users
        .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
        .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Organization)
        .FirstOrDefaultAsync(u => u.Id == Guid.Parse(userId));

    if (user == null)
        return Results.NotFound();

    return Results.Ok(new UserDto
    {
        Id = user.Id,
        Email = user.Email,
        FirstName = user.FirstName,
        LastName = user.LastName,
        Roles = user.UserRoles.Select(ur => new RoleDto
        {
            Name = ur.Role.Name,
            OrganizationId = ur.OrganizationId,
            OrganizationName = ur.Organization.Name
        }).ToList()
    });
}).RequireAuthorization();

// ============================================================================
// USER ENDPOINTS (protected)
// ============================================================================

app.MapGet("/api/users", async (IdentityDbContext db, HttpContext context) =>
{
    var tenantId = GetTenantIdFromClaims(context);
    if (tenantId == null)
        return Results.Unauthorized();

    var users = await db.Users
        .Where(u => u.TenantId == tenantId.Value)
        .Select(u => new UserDto
        {
            Id = u.Id,
            Email = u.Email,
            FirstName = u.FirstName,
            LastName = u.LastName,
            Roles = u.UserRoles.Select(ur => new RoleDto
            {
                Name = ur.Role.Name,
                OrganizationId = ur.OrganizationId,
                OrganizationName = ur.Organization.Name
            }).ToList()
        })
        .ToListAsync();

    return Results.Ok(users);
}).RequireAuthorization();

app.MapGet("/api/users/{id:guid}", async (Guid id, IdentityDbContext db, HttpContext context) =>
{
    var tenantId = GetTenantIdFromClaims(context);
    if (tenantId == null)
        return Results.Unauthorized();

    var user = await db.Users
        .Where(u => u.Id == id && u.TenantId == tenantId.Value)
        .Select(u => new UserDto
        {
            Id = u.Id,
            Email = u.Email,
            FirstName = u.FirstName,
            LastName = u.LastName,
            Roles = u.UserRoles.Select(ur => new RoleDto
            {
                Name = ur.Role.Name,
                OrganizationId = ur.OrganizationId,
                OrganizationName = ur.Organization.Name
            }).ToList()
        })
        .FirstOrDefaultAsync();

    return user == null ? Results.NotFound() : Results.Ok(user);
}).RequireAuthorization();

// ============================================================================
// ORGANIZATION ENDPOINTS (protected)
// ============================================================================

app.MapGet("/api/organizations", async (IdentityDbContext db, HttpContext context) =>
{
    var tenantId = GetTenantIdFromClaims(context);
    if (tenantId == null)
        return Results.Unauthorized();

    var organizations = await db.Organizations
        .Where(o => o.TenantId == tenantId.Value)
        .Select(o => new OrganizationDto
        {
            Id = o.Id,
            Name = o.Name,
            Type = o.Type,
            ParentId = o.ParentId
        })
        .ToListAsync();

    return Results.Ok(organizations);
}).RequireAuthorization();

app.MapPost("/api/organizations", async (CreateOrganizationRequest request, IdentityDbContext db, HttpContext context) =>
{
    var tenantId = GetTenantIdFromClaims(context);
    if (tenantId == null)
        return Results.Unauthorized();

    // Validate parent exists if provided
    if (request.ParentId.HasValue)
    {
        var parentExists = await db.Organizations
            .AnyAsync(o => o.Id == request.ParentId.Value && o.TenantId == tenantId.Value);
        if (!parentExists)
            return Results.BadRequest(new { error = "Parent organization not found" });
    }

    var organization = new Organization
    {
        TenantId = tenantId.Value,
        ParentId = request.ParentId,
        Name = request.Name,
        Type = request.Type,
        Metadata = request.Metadata
    };

    db.Organizations.Add(organization);

    // Audit log
    var userId = GetUserIdFromClaims(context);
    var auditLog = new AuditLog
    {
        TenantId = tenantId.Value,
        UserId = userId,
        Action = "organization.created",
        ResourceType = "organization",
        ResourceId = organization.Id,
        Details = $"{{\"name\":\"{organization.Name}\",\"type\":\"{organization.Type}\"}}"
    };
    db.AuditLogs.Add(auditLog);

    await db.SaveChangesAsync();

    return Results.Created($"/api/organizations/{organization.Id}", new OrganizationDto
    {
        Id = organization.Id,
        Name = organization.Name,
        Type = organization.Type,
        ParentId = organization.ParentId
    });
}).RequireAuthorization();

app.MapPut("/api/organizations/{id:guid}", async (Guid id, UpdateOrganizationRequest request, IdentityDbContext db, HttpContext context) =>
{
    var tenantId = GetTenantIdFromClaims(context);
    if (tenantId == null)
        return Results.Unauthorized();

    var organization = await db.Organizations
        .FirstOrDefaultAsync(o => o.Id == id && o.TenantId == tenantId.Value);

    if (organization == null)
        return Results.NotFound();

    organization.Name = request.Name;
    organization.Type = request.Type;
    organization.Metadata = request.Metadata;
    organization.UpdatedAt = DateTime.UtcNow;

    // Audit log
    var userId = GetUserIdFromClaims(context);
    var auditLog = new AuditLog
    {
        TenantId = tenantId.Value,
        UserId = userId,
        Action = "organization.updated",
        ResourceType = "organization",
        ResourceId = organization.Id,
        Details = $"{{\"name\":\"{organization.Name}\",\"type\":\"{organization.Type}\"}}"
    };
    db.AuditLogs.Add(auditLog);

    await db.SaveChangesAsync();

    return Results.Ok(new OrganizationDto
    {
        Id = organization.Id,
        Name = organization.Name,
        Type = organization.Type,
        ParentId = organization.ParentId
    });
}).RequireAuthorization();

app.MapDelete("/api/organizations/{id:guid}", async (Guid id, IdentityDbContext db, HttpContext context) =>
{
    var tenantId = GetTenantIdFromClaims(context);
    if (tenantId == null)
        return Results.Unauthorized();

    var organization = await db.Organizations
        .FirstOrDefaultAsync(o => o.Id == id && o.TenantId == tenantId.Value);

    if (organization == null)
        return Results.NotFound();

    // Soft delete
    organization.Delete();

    // Audit log
    var userId = GetUserIdFromClaims(context);
    var auditLog = new AuditLog
    {
        TenantId = tenantId.Value,
        UserId = userId,
        Action = "organization.deleted",
        ResourceType = "organization",
        ResourceId = organization.Id,
        Details = $"{{\"name\":\"{organization.Name}\"}}"
    };
    db.AuditLogs.Add(auditLog);

    await db.SaveChangesAsync();

    return Results.NoContent();
}).RequireAuthorization();

// ============================================================================
// USER CRUD ENDPOINTS
// ============================================================================

app.MapPost("/api/users", async (CreateUserRequest request, IdentityDbContext db, HttpContext context) =>
{
    var tenantId = GetTenantIdFromClaims(context);
    if (tenantId == null)
        return Results.Unauthorized();

    // Check if email already exists
    var emailExists = await db.Users
        .AnyAsync(u => u.Email == request.Email && u.TenantId == tenantId.Value);
    if (emailExists)
        return Results.BadRequest(new { error = "Email already exists" });

    var user = new HazinaUser
    {
        TenantId = tenantId.Value,
        Email = request.Email,
        PasswordHash = HazinaUser.HashPassword(request.Password),
        FirstName = request.FirstName,
        LastName = request.LastName,
        EmailVerified = false
    };

    db.Users.Add(user);

    // Audit log
    var currentUserId = GetUserIdFromClaims(context);
    var auditLog = new AuditLog
    {
        TenantId = tenantId.Value,
        UserId = currentUserId,
        Action = "user.created",
        ResourceType = "user",
        ResourceId = user.Id,
        Details = $"{{\"email\":\"{user.Email}\"}}"
    };
    db.AuditLogs.Add(auditLog);

    await db.SaveChangesAsync();

    return Results.Created($"/api/users/{user.Id}", new UserDto
    {
        Id = user.Id,
        Email = user.Email,
        FirstName = user.FirstName,
        LastName = user.LastName,
        Roles = new List<RoleDto>()
    });
}).RequireAuthorization();

app.MapPut("/api/users/{id:guid}", async (Guid id, UpdateUserRequest request, IdentityDbContext db, HttpContext context) =>
{
    var tenantId = GetTenantIdFromClaims(context);
    if (tenantId == null)
        return Results.Unauthorized();

    var user = await db.Users
        .FirstOrDefaultAsync(u => u.Id == id && u.TenantId == tenantId.Value);

    if (user == null)
        return Results.NotFound();

    // Check if email is being changed and already exists
    if (user.Email != request.Email)
    {
        var emailExists = await db.Users
            .AnyAsync(u => u.Email == request.Email && u.TenantId == tenantId.Value && u.Id != id);
        if (emailExists)
            return Results.BadRequest(new { error = "Email already exists" });
    }

    user.Email = request.Email;
    user.FirstName = request.FirstName;
    user.LastName = request.LastName;
    user.UpdatedAt = DateTime.UtcNow;

    // Update password if provided
    if (!string.IsNullOrEmpty(request.Password))
    {
        user.PasswordHash = HazinaUser.HashPassword(request.Password);
    }

    // Audit log
    var currentUserId = GetUserIdFromClaims(context);
    var auditLog = new AuditLog
    {
        TenantId = tenantId.Value,
        UserId = currentUserId,
        Action = "user.updated",
        ResourceType = "user",
        ResourceId = user.Id,
        Details = $"{{\"email\":\"{user.Email}\"}}"
    };
    db.AuditLogs.Add(auditLog);

    await db.SaveChangesAsync();

    return Results.Ok(new UserDto
    {
        Id = user.Id,
        Email = user.Email,
        FirstName = user.FirstName,
        LastName = user.LastName,
        Roles = new List<RoleDto>()
    });
}).RequireAuthorization();

app.MapDelete("/api/users/{id:guid}", async (Guid id, IdentityDbContext db, HttpContext context) =>
{
    var tenantId = GetTenantIdFromClaims(context);
    if (tenantId == null)
        return Results.Unauthorized();

    var user = await db.Users
        .FirstOrDefaultAsync(u => u.Id == id && u.TenantId == tenantId.Value);

    if (user == null)
        return Results.NotFound();

    // Soft delete
    user.Delete();

    // Audit log
    var currentUserId = GetUserIdFromClaims(context);
    var auditLog = new AuditLog
    {
        TenantId = tenantId.Value,
        UserId = currentUserId,
        Action = "user.deleted",
        ResourceType = "user",
        ResourceId = user.Id,
        Details = $"{{\"email\":\"{user.Email}\"}}"
    };
    db.AuditLogs.Add(auditLog);

    await db.SaveChangesAsync();

    return Results.NoContent();
}).RequireAuthorization();

// ============================================================================
// ROLE MANAGEMENT ENDPOINTS
// ============================================================================

app.MapGet("/api/roles", async (IdentityDbContext db) =>
{
    var roles = await db.Roles
        .Select(r => new RoleInfoDto
        {
            Id = r.Id,
            Name = r.Name,
            Description = r.Description
        })
        .ToListAsync();

    return Results.Ok(roles);
}).RequireAuthorization();

app.MapPost("/api/users/{userId:guid}/roles", async (Guid userId, AssignRoleRequest request, IdentityDbContext db, HttpContext context) =>
{
    var tenantId = GetTenantIdFromClaims(context);
    if (tenantId == null)
        return Results.Unauthorized();

    // Verify user exists
    var user = await db.Users
        .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId.Value);
    if (user == null)
        return Results.NotFound(new { error = "User not found" });

    // Verify role exists
    var role = await db.Roles.FindAsync(request.RoleId);
    if (role == null)
        return Results.NotFound(new { error = "Role not found" });

    // Verify organization exists and belongs to tenant
    var organization = await db.Organizations
        .FirstOrDefaultAsync(o => o.Id == request.OrganizationId && o.TenantId == tenantId.Value);
    if (organization == null)
        return Results.NotFound(new { error = "Organization not found" });

    // Check if already assigned
    var existingAssignment = await db.UserRoles
        .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == request.RoleId && ur.OrganizationId == request.OrganizationId);
    if (existingAssignment != null)
        return Results.BadRequest(new { error = "Role already assigned to user in this organization" });

    var userRole = new UserRole
    {
        UserId = userId,
        RoleId = request.RoleId,
        OrganizationId = request.OrganizationId
    };

    db.UserRoles.Add(userRole);

    // Audit log
    var currentUserId = GetUserIdFromClaims(context);
    var auditLog = new AuditLog
    {
        TenantId = tenantId.Value,
        UserId = currentUserId,
        Action = "role.assigned",
        ResourceType = "user_role",
        ResourceId = userRole.Id,
        Details = $"{{\"userId\":\"{userId}\",\"roleId\":\"{request.RoleId}\",\"roleName\":\"{role.Name}\",\"organizationId\":\"{request.OrganizationId}\"}}"
    };
    db.AuditLogs.Add(auditLog);

    await db.SaveChangesAsync();

    return Results.Created($"/api/users/{userId}/roles/{userRole.Id}", new
    {
        id = userRole.Id,
        userId = userRole.UserId,
        roleId = userRole.RoleId,
        roleName = role.Name,
        organizationId = userRole.OrganizationId,
        organizationName = organization.Name
    });
}).RequireAuthorization();

app.MapDelete("/api/users/{userId:guid}/roles/{userRoleId:guid}", async (Guid userId, Guid userRoleId, IdentityDbContext db, HttpContext context) =>
{
    var tenantId = GetTenantIdFromClaims(context);
    if (tenantId == null)
        return Results.Unauthorized();

    var userRole = await db.UserRoles
        .Include(ur => ur.User)
        .Include(ur => ur.Role)
        .FirstOrDefaultAsync(ur => ur.Id == userRoleId && ur.UserId == userId && ur.User.TenantId == tenantId.Value);

    if (userRole == null)
        return Results.NotFound();

    db.UserRoles.Remove(userRole);

    // Audit log
    var currentUserId = GetUserIdFromClaims(context);
    var auditLog = new AuditLog
    {
        TenantId = tenantId.Value,
        UserId = currentUserId,
        Action = "role.revoked",
        ResourceType = "user_role",
        ResourceId = userRoleId,
        Details = $"{{\"userId\":\"{userId}\",\"roleId\":\"{userRole.RoleId}\",\"roleName\":\"{userRole.Role.Name}\"}}"
    };
    db.AuditLogs.Add(auditLog);

    await db.SaveChangesAsync();

    return Results.NoContent();
}).RequireAuthorization();

// ============================================================================
// HELPER FUNCTIONS
// ============================================================================

string GenerateJwtToken(HazinaUser user, string key, string issuer, string audience, int expiresInMinutes)
{
    var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
    var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new(ClaimTypes.Email, user.Email),
        new("tenant_id", user.TenantId.ToString()),
        new("full_name", user.FullName)
    };

    // Add roles as claims
    foreach (var userRole in user.UserRoles)
    {
        claims.Add(new Claim(ClaimTypes.Role, userRole.Role.Name));
        claims.Add(new Claim("org_id", userRole.OrganizationId.ToString()));
    }

    var token = new JwtSecurityToken(
        issuer: issuer,
        audience: audience,
        claims: claims,
        expires: DateTime.UtcNow.AddMinutes(expiresInMinutes),
        signingCredentials: credentials
    );

    return new JwtSecurityTokenHandler().WriteToken(token);
}

string GenerateRefreshToken()
{
    return Convert.ToBase64String(Guid.NewGuid().ToByteArray()) + Convert.ToBase64String(Guid.NewGuid().ToByteArray());
}

Guid? GetTenantIdFromClaims(HttpContext context)
{
    var tenantIdClaim = context.User.FindFirst("tenant_id")?.Value;
    return string.IsNullOrEmpty(tenantIdClaim) ? null : Guid.Parse(tenantIdClaim);
}

Guid? GetUserIdFromClaims(HttpContext context)
{
    var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    return string.IsNullOrEmpty(userIdClaim) ? null : Guid.Parse(userIdClaim);
}

app.Run();

// ============================================================================
// DTOs
// ============================================================================

record LoginRequest(string Email, string Password);
record LoginResponse(string AccessToken, string RefreshToken, int ExpiresIn, UserDto User);
record RefreshRequest(string RefreshToken);
record RefreshResponse(string AccessToken, int ExpiresIn);
record LogoutRequest(string RefreshToken);

record UserDto
{
    public Guid Id { get; init; }
    public string Email { get; init; } = null!;
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public List<RoleDto> Roles { get; init; } = new();
}

record RoleDto
{
    public string Name { get; init; } = null!;
    public Guid OrganizationId { get; init; }
    public string OrganizationName { get; init; } = null!;
}

record OrganizationDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = null!;
    public string Type { get; init; } = null!;
    public Guid? ParentId { get; init; }
}

record CreateUserRequest(string Email, string Password, string? FirstName, string? LastName);
record UpdateUserRequest(string Email, string? FirstName, string? LastName, string? Password);

record CreateOrganizationRequest(string Name, string Type, Guid? ParentId, string? Metadata);
record UpdateOrganizationRequest(string Name, string Type, string? Metadata);

record RoleInfoDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = null!;
    public string? Description { get; init; }
}

record AssignRoleRequest(Guid RoleId, Guid OrganizationId);
