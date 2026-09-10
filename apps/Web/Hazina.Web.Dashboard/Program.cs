using Hazina.Web.Dashboard.Hubs;
using Hazina.Web.Dashboard.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:5000", "https://localhost:5001")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Add dashboard services
builder.Services.AddSingleton<DashboardService>();
builder.Services.AddSingleton<MetricsCollector>();
builder.Services.AddHostedService<MetricsCollector>(sp => sp.GetRequiredService<MetricsCollector>());

var app = builder.Build();

// Configure middleware
app.UseCors();
app.UseStaticFiles();
app.UseRouting();

app.MapControllers();
app.MapHub<DashboardHub>("/dashboardHub");

// Serve SPA
app.MapFallbackToFile("index.html");

Console.WriteLine("🎛️  Jengo Control Dashboard");
Console.WriteLine("📊 Dashboard: http://localhost:5000");
Console.WriteLine("🔌 SignalR Hub: http://localhost:5000/dashboardHub");

app.Run();
