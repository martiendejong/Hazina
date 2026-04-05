using Hazina.API.DocumentStore.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ──────────────────────────────────────────────
// Hazina Document Store API (library)
// ──────────────────────────────────────────────
builder.Services.AddHazinaDocumentStoreApi(options =>
{
    options.FileStoragePath = builder.Configuration["Hazina:FileStoragePath"] ?? "./data";
    options.MaxUploadSizeMB = builder.Configuration.GetValue<int>("Hazina:MaxUploadSizeMB", 100);
    options.EmbeddingDimensions = builder.Configuration.GetValue<int>("Hazina:EmbeddingDimensions", 1536);
    options.DefaultLLMModel = builder.Configuration["Hazina:DefaultLLMModel"] ?? "gpt-4o";
    options.DefaultEmbeddingModel = builder.Configuration["Hazina:DefaultEmbeddingModel"] ?? "text-embedding-3-small";
    options.TesseractDataPath = builder.Configuration["Tesseract:DataPath"] ?? "./tessdata";
    options.MetadataConnectionString = builder.Configuration.GetConnectionString("SQLite")
                                       ?? "Data Source=search.db";
});

// ──────────────────────────────────────────────
// Host-level concerns: JWT, CORS, Swagger
// ──────────────────────────────────────────────

// Authentication
var jwtSettings = builder.Configuration.GetSection("Authentication:Jwt");
var secretKey = jwtSettings["SecretKey"]
    ?? throw new InvalidOperationException("JWT secret key not configured");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"] ?? "Hazina.API.Search",
            ValidAudience = jwtSettings["Audience"] ?? "Hazina.API.Search.Clients",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
        };
    });

builder.Services.AddAuthorization();

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Controllers
builder.Services.AddControllers();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Hazina Search API",
        Version = "v1",
        Description = "RAG-powered document search and retrieval API using Hazina framework"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Health checks
builder.Services.AddHealthChecks();

var app = builder.Build();

// ──────────────────────────────────────────────
// Pipeline
// ──────────────────────────────────────────────

// Library middleware (DB init + exception handling)
app.UseHazinaDocumentStoreApi();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Hazina Search API v1");
        c.RoutePrefix = string.Empty;
    });
}

// Security headers
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    await next();
});

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
