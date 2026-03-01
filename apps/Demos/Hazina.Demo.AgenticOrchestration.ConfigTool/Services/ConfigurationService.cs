using System.Text.Json;
using System.Text.Json.Serialization;
using Hazina.Demo.AgenticOrchestration.ConfigTool.Models;

namespace Hazina.Demo.AgenticOrchestration.ConfigTool.Services;

public class ConfigurationService
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public AppSettings ReadConfiguration(string configPath)
    {
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException($"Configuration file not found: {configPath}");
        }

        var json = File.ReadAllText(configPath);
        var settings = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions);

        if (settings == null)
        {
            throw new InvalidOperationException("Failed to deserialize configuration");
        }

        return settings;
    }

    public void WriteConfiguration(string configPath, AppSettings settings, bool createBackup = true)
    {
        // Create backup if file exists and backup is requested
        if (createBackup && File.Exists(configPath))
        {
            var backupPath = $"{configPath}.backup-{DateTime.Now:yyyyMMdd-HHmmss}";
            File.Copy(configPath, backupPath, true);
        }

        // Write to temp file first (atomic operation)
        var tempPath = $"{configPath}.tmp";
        var json = JsonSerializer.Serialize(settings, _jsonOptions);
        File.WriteAllText(tempPath, json);

        // Replace original file
        File.Move(tempPath, configPath, true);
    }

    public void UpdateAuthentication(AppSettings settings, string? username = null, string? password = null,
        string? realm = null, bool? enabled = null)
    {
        if (username != null) settings.Authentication.Username = username;
        if (password != null) settings.Authentication.Password = password;
        if (realm != null) settings.Authentication.Realm = realm;
        if (enabled.HasValue) settings.Authentication.Enabled = enabled.Value;
    }

    public void UpdateKestrel(AppSettings settings, string protocol, int port, string? certPath = null, string? keyPath = null)
    {
        settings.Kestrel ??= new KestrelConfig();
        settings.Kestrel.Endpoints.Clear();

        var url = $"{protocol}://*:{port}";
        var endpoint = new EndpointConfig { Url = url };

        if (protocol.Equals("https", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrEmpty(certPath) || string.IsNullOrEmpty(keyPath))
            {
                throw new ArgumentException("Certificate and key paths required for HTTPS");
            }

            endpoint.Certificate = new CertificateConfig
            {
                Path = certPath,
                KeyPath = keyPath
            };
        }

        var endpointKey = protocol.Equals("https", StringComparison.OrdinalIgnoreCase) ? "Https" : "Http";
        settings.Kestrel.Endpoints[endpointKey] = endpoint;
    }

    public void UpdatePaths(AppSettings settings, string? databasePath = null, string? logsPath = null,
        string? uploadsPath = null)
    {
        if (databasePath != null) settings.AgenticOrchestration.DatabasePath = databasePath;
        if (logsPath != null) settings.AgenticOrchestration.LogsPath = logsPath;
        if (uploadsPath != null) settings.AgenticOrchestration.Uploads.Path = uploadsPath;
    }

    public void UpdateTerminal(AppSettings settings, string? command = null, string? workingDir = null,
        int? columns = null, int? rows = null, int? maxSessions = null, int? timeout = null)
    {
        if (command != null) settings.AgenticOrchestration.Terminal.DefaultCommand = command;
        if (workingDir != null) settings.AgenticOrchestration.Terminal.DefaultWorkingDirectory = workingDir;
        if (columns.HasValue) settings.AgenticOrchestration.Terminal.DefaultColumns = columns.Value;
        if (rows.HasValue) settings.AgenticOrchestration.Terminal.DefaultRows = rows.Value;
        if (maxSessions.HasValue) settings.AgenticOrchestration.Terminal.MaxConcurrentSessions = maxSessions.Value;
        if (timeout.HasValue) settings.AgenticOrchestration.Terminal.SessionTimeoutMinutes = timeout.Value;
    }

    public void UpdateOpenAI(AppSettings settings, string? apiKey = null, string? model = null,
        string? embeddingModel = null, string? imageModel = null, string? ttsModel = null, bool clearApiKey = false)
    {
        if (clearApiKey)
        {
            settings.OpenAI.ApiKey = "";
            return;
        }

        if (apiKey != null) settings.OpenAI.ApiKey = apiKey;
        if (model != null) settings.OpenAI.Model = model;
        if (embeddingModel != null) settings.OpenAI.EmbeddingModel = embeddingModel;
        if (imageModel != null) settings.OpenAI.ImageModel = imageModel;
        if (ttsModel != null) settings.OpenAI.TtsModel = ttsModel;
    }

    public string MaskSensitiveValue(string value, int visibleChars = 3)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Length <= visibleChars * 2) return new string('*', value.Length);

        var prefix = value.Substring(0, visibleChars);
        var suffix = value.Substring(value.Length - visibleChars);
        var maskedLength = value.Length - (visibleChars * 2);
        return $"{prefix}{"".PadLeft(maskedLength, '*')}{suffix}";
    }
}
