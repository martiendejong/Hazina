using Hazina.API.Search.Data;
using Hazina.API.Search.Extensions;
using Hazina.API.Search.Integration;
using Hazina.API.Search.Middleware;
using Hazina.API.Search.Services;
using Hazina.API.Search.Services.FormatHandlers;
using Hazina.AI.Providers.Core;
using Hazina.LLMs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container

// Database - SQLite for metadata
builder.Services.AddDbContext<SearchDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("SQLite")));

// Authentication - JWT Bearer
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

// Swagger/OpenAPI
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
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token.",
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

// TODO: Add Hazina LLM Services when framework is available
// builder.Services.AddHazinaLLMs(...);

// Hazina Store Factories
builder.Services.AddSingleton<IDocumentStoreFactory, DocumentStoreFactory>();
builder.Services.AddSingleton<IEmbeddingStoreFactory, EmbeddingStoreFactory>();
builder.Services.AddSingleton<IRAGEngineFactory, RAGEngineFactory>();

// Format Handlers
builder.Services.AddTransient<IFormatHandler, TextFormatHandler>();
builder.Services.AddTransient<IFormatHandler, DocxFormatHandler>();
builder.Services.AddTransient<IFormatHandler, PdfFormatHandler>();
builder.Services.AddTransient<IFormatHandler, ImageFormatHandler>();

// Application Services
builder.Services.AddScoped<RAGStoreRepository>();
builder.Services.AddScoped<ChunkingService>();
builder.Services.AddScoped<DocumentProcessor>();
builder.Services.AddScoped<EmbeddingService>();
builder.Services.AddScoped<RAGStoreManager>();
builder.Services.AddScoped<SearchService>();

// Health checks
builder.Services.AddHealthChecks();

var app = builder.Build();

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<SearchDbContext>();
    dbContext.Database.EnsureCreated();
}

// Configure the HTTP request pipeline

// Exception handling middleware
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Hazina Search API v1");
        c.RoutePrefix = string.Empty; // Serve Swagger UI at root
    });
}

// Security headers
app.Use(async (context, next) =>
{
    context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Add("X-Frame-Options", "DENY");
    context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
    await next();
});

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
