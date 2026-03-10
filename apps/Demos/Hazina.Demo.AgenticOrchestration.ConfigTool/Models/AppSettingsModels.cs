namespace Hazina.Demo.AgenticOrchestration.ConfigTool.Models;

public class AppSettings
{
    public LoggingConfig Logging { get; set; } = new();
    public string AllowedHosts { get; set; } = "*";
    public KestrelConfig? Kestrel { get; set; }
    public AuthenticationConfig Authentication { get; set; } = new();
    public AgenticOrchestrationConfig AgenticOrchestration { get; set; } = new();
    public SwaggerConfig Swagger { get; set; } = new();
    public OpenAIConfig OpenAI { get; set; } = new();
}

public class LoggingConfig
{
    public Dictionary<string, string> LogLevel { get; set; } = new()
    {
        ["Default"] = "Information",
        ["Microsoft.AspNetCore"] = "Warning"
    };
    public EventLogConfig? EventLog { get; set; }
}

public class EventLogConfig
{
    public Dictionary<string, string> LogLevel { get; set; } = new()
    {
        ["Default"] = "Information"
    };
}

public class KestrelConfig
{
    public Dictionary<string, EndpointConfig> Endpoints { get; set; } = new();
}

public class EndpointConfig
{
    public string Url { get; set; } = "";
    public CertificateConfig? Certificate { get; set; }
}

public class CertificateConfig
{
    public string Path { get; set; } = "";
    public string KeyPath { get; set; } = "";
}

public class AuthenticationConfig
{
    public bool Enabled { get; set; } = true;
    public string Username { get; set; } = "admin";
    public string Password { get; set; } = "changeme";
    public string Realm { get; set; } = "Hazina Agentic Orchestration";
}

public class AgenticOrchestrationConfig
{
    public string DatabasePath { get; set; } = "data\\agent-activity.db";
    public string LogsPath { get; set; } = "logs";
    public string EntitiesYamlPath { get; set; } = "entities.yaml";
    public SignalRConfig SignalR { get; set; } = new();
    public PollingConfig Polling { get; set; } = new();
    public FeaturesConfig Features { get; set; } = new();
    public TerminalConfig Terminal { get; set; } = new();
    public SessionLoggingConfig SessionLogging { get; set; } = new();
    public UploadsConfig Uploads { get; set; } = new();
}

public class SignalRConfig
{
    public bool Enabled { get; set; } = true;
    public string HubPath { get; set; } = "/hubs/agentic";
}

public class PollingConfig
{
    public int InstanceHeartbeatTimeoutSeconds { get; set; } = 60;
    public int InteractionExpiryMinutes { get; set; } = 60;
}

public class FeaturesConfig
{
    public bool EnableTaskQueue { get; set; } = true;
    public bool EnableOutputStreaming { get; set; } = true;
    public bool EnableRealtimeNotifications { get; set; } = true;
}

public class TerminalConfig
{
    public string DefaultCommand { get; set; } = "claude";
    public string DefaultWorkingDirectory { get; set; } = "";
    public List<string> DefaultArguments { get; set; } = new();
    public int DefaultColumns { get; set; } = 120;
    public int DefaultRows { get; set; } = 30;
    public int MaxConcurrentSessions { get; set; } = 10;
    public int SessionTimeoutMinutes { get; set; } = 60;
}

public class SessionLoggingConfig
{
    public bool Enabled { get; set; } = true;
    public string BasePath { get; set; } = "logs\\agent-sessions";
}

public class UploadsConfig
{
    public string Path { get; set; } = "uploads";
    public int MaxFileSizeMB { get; set; } = 50;
}

public class SwaggerConfig
{
    public bool Enabled { get; set; } = true;
    public string Title { get; set; } = "Hazina Agentic Orchestration API";
    public string Description { get; set; } = "Web API for managing Claude Code CLI instances";
    public string Version { get; set; } = "v1";
}

public class OpenAIConfig
{
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "gpt-4o-mini";
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";
    public string ImageModel { get; set; } = "dall-e-3";
    public string TtsModel { get; set; } = "gpt-4o-mini-tts";
}

public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public List<string> Info { get; set; } = new();

    public void AddError(string message) => Errors.Add(message);
    public void AddWarning(string message) => Warnings.Add(message);
    public void AddInfo(string message) => Info.Add(message);
}
