using Hazina.Agent.API.Services;
using OpenAI;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add OpenAI client
var openAiApiKey = builder.Configuration["OpenAI:ApiKey"]
    ?? throw new InvalidOperationException("OpenAI API key not configured");

builder.Services.AddSingleton(new OpenAIClient(openAiApiKey));

// Add agent services
builder.Services.AddSingleton<ISessionLogger, SessionLogger>();
builder.Services.AddScoped<IAgentExecutionService, AgentExecutionService>();

var app = builder.Build();

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
