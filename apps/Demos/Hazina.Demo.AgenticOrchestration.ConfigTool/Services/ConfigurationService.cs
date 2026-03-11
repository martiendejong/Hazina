using System.Text.Json;
using System.Text.Json.Nodes;
using Hazina.Demo.AgenticOrchestration.ConfigTool.Models;

namespace Hazina.Demo.AgenticOrchestration.ConfigTool.Services;

/// <summary>
/// Non-destructive configuration service using JsonNode.
/// Preserves ALL existing JSON properties - only modifies specified values.
/// Unknown sections, comments-as-fields, and future properties are never lost.
/// </summary>
public class ConfigurationService
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// Read configuration as strongly-typed model (for validation and display).
    /// </summary>
    public AppSettings ReadConfiguration(string configPath)
    {
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException($"Configuration file not found: {configPath}");
        }

        var json = File.ReadAllText(configPath);
        return JsonSerializer.Deserialize<AppSettings>(json, ReadOptions)
            ?? throw new InvalidOperationException("Failed to deserialize configuration");
    }

    /// <summary>
    /// Read configuration as JsonNode (for non-destructive editing).
    /// </summary>
    public JsonNode ReadAsJsonNode(string configPath)
    {
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException($"Configuration file not found: {configPath}");
        }

        var json = File.ReadAllText(configPath);
        return JsonNode.Parse(json)
            ?? throw new InvalidOperationException("Failed to parse JSON");
    }

    /// <summary>
    /// Write JsonNode back to file with backup and atomic write.
    /// </summary>
    public void WriteJsonNode(string configPath, JsonNode root, bool createBackup = true)
    {
        if (createBackup && File.Exists(configPath))
        {
            var backupPath = $"{configPath}.backup-{DateTime.Now:yyyyMMdd-HHmmss}";
            File.Copy(configPath, backupPath, true);
        }

        var tempPath = $"{configPath}.tmp";
        var json = root.ToJsonString(WriteOptions);
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, configPath, true);
    }

    /// <summary>
    /// Set a value at a nested JSON path, creating intermediate objects as needed.
    /// Path format: "Authentication.Username" or "Kestrel.Endpoints.Https.Url"
    /// </summary>
    public static void SetValue(JsonNode root, string path, JsonNode? value)
    {
        var parts = path.Split('.');
        var current = root;

        for (int i = 0; i < parts.Length - 1; i++)
        {
            if (current is JsonObject obj)
            {
                if (obj[parts[i]] == null)
                {
                    obj[parts[i]] = new JsonObject();
                }
                current = obj[parts[i]]!;
            }
            else
            {
                throw new InvalidOperationException($"Cannot navigate path '{path}': '{parts[i]}' is not an object");
            }
        }

        if (current is JsonObject parentObj)
        {
            parentObj[parts[^1]] = value;
        }
    }

    /// <summary>
    /// Remove a key at a nested JSON path.
    /// </summary>
    public static bool RemoveKey(JsonNode root, string path)
    {
        var parts = path.Split('.');
        var current = root;

        for (int i = 0; i < parts.Length - 1; i++)
        {
            if (current is JsonObject obj && obj[parts[i]] != null)
            {
                current = obj[parts[i]]!;
            }
            else
            {
                return false;
            }
        }

        if (current is JsonObject parentObj)
        {
            return parentObj.Remove(parts[^1]);
        }
        return false;
    }

    // ================================================================
    // HIGH-LEVEL UPDATE METHODS (non-destructive via JsonNode)
    // ================================================================

    public void UpdateAuthentication(JsonNode root, string? username = null, string? password = null,
        string? realm = null, bool? enabled = null)
    {
        if (username != null) SetValue(root, "Authentication.Username", JsonValue.Create(username));
        if (password != null) SetValue(root, "Authentication.Password", JsonValue.Create(password));
        if (realm != null) SetValue(root, "Authentication.Realm", JsonValue.Create(realm));
        if (enabled.HasValue) SetValue(root, "Authentication.Enabled", JsonValue.Create(enabled.Value));
    }

    public void UpdateKestrel(JsonNode root, string protocol, int port, string? host = null, string? certPath = null, string? keyPath = null)
    {
        var bindHost = host ?? "localhost";
        var url = $"{protocol}://{bindHost}:{port}";

        // Use "Default" endpoint key for simple configs, named keys for cert-based
        var hasCert = !string.IsNullOrEmpty(certPath) && !string.IsNullOrEmpty(keyPath);
        var endpointKey = hasCert ? (protocol.Equals("https", StringComparison.OrdinalIgnoreCase) ? "Https" : "Http") : "Default";

        // Build endpoint object
        var endpoint = new JsonObject { ["Url"] = url };

        if (hasCert)
        {
            endpoint["Certificate"] = new JsonObject
            {
                ["Path"] = certPath,
                ["KeyPath"] = keyPath
            };
        }

        // Clear all existing endpoints and set the new one
        SetValue(root, "Kestrel.Endpoints", new JsonObject { [endpointKey] = endpoint });
    }

    public void UpdatePaths(JsonNode root, string? databasePath = null, string? logsPath = null,
        string? uploadsPath = null)
    {
        if (databasePath != null) SetValue(root, "AgenticOrchestration.DatabasePath", JsonValue.Create(databasePath));
        if (logsPath != null) SetValue(root, "AgenticOrchestration.LogsPath", JsonValue.Create(logsPath));
        if (uploadsPath != null) SetValue(root, "AgenticOrchestration.Uploads.Path", JsonValue.Create(uploadsPath));
    }

    public void UpdateTerminal(JsonNode root, string? command = null, string? workingDir = null,
        int? columns = null, int? rows = null, int? maxSessions = null, int? timeout = null)
    {
        if (command != null) SetValue(root, "AgenticOrchestration.Terminal.DefaultCommand", JsonValue.Create(command));
        if (workingDir != null) SetValue(root, "AgenticOrchestration.Terminal.DefaultWorkingDirectory", JsonValue.Create(workingDir));
        if (columns.HasValue) SetValue(root, "AgenticOrchestration.Terminal.DefaultColumns", JsonValue.Create(columns.Value));
        if (rows.HasValue) SetValue(root, "AgenticOrchestration.Terminal.DefaultRows", JsonValue.Create(rows.Value));
        if (maxSessions.HasValue) SetValue(root, "AgenticOrchestration.Terminal.MaxConcurrentSessions", JsonValue.Create(maxSessions.Value));
        if (timeout.HasValue) SetValue(root, "AgenticOrchestration.Terminal.SessionTimeoutMinutes", JsonValue.Create(timeout.Value));
    }

    public void UpdateOpenAI(JsonNode root, string? apiKey = null, string? model = null,
        string? embeddingModel = null, string? imageModel = null, string? ttsModel = null, bool clearApiKey = false)
    {
        if (clearApiKey)
        {
            SetValue(root, "OpenAI.ApiKey", JsonValue.Create(""));
            return;
        }

        if (apiKey != null) SetValue(root, "OpenAI.ApiKey", JsonValue.Create(apiKey));
        if (model != null) SetValue(root, "OpenAI.Model", JsonValue.Create(model));
        if (embeddingModel != null) SetValue(root, "OpenAI.EmbeddingModel", JsonValue.Create(embeddingModel));
        if (imageModel != null) SetValue(root, "OpenAI.ImageModel", JsonValue.Create(imageModel));
        if (ttsModel != null) SetValue(root, "OpenAI.TtsModel", JsonValue.Create(ttsModel));
    }

    public string MaskSensitiveValue(string value, int visibleChars = 3)
    {
        if (string.IsNullOrEmpty(value)) return "(not set)";
        if (value.Length <= visibleChars * 2) return new string('*', value.Length);

        var prefix = value[..visibleChars];
        var suffix = value[^visibleChars..];
        var maskedLength = value.Length - (visibleChars * 2);
        return $"{prefix}{"".PadLeft(maskedLength, '*')}{suffix}";
    }
}
