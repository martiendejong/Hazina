using Hazina.Demo.AgenticOrchestration.ConfigTool.Models;

namespace Hazina.Demo.AgenticOrchestration.ConfigTool.Services;

public class ValidationService
{
    public ValidationResult Validate(AppSettings settings, string configPath)
    {
        var result = new ValidationResult { IsValid = true };

        // Basic structure validation
        if (settings.Logging == null)
        {
            result.AddError("Logging configuration is missing");
            result.IsValid = false;
        }

        if (settings.Authentication == null)
        {
            result.AddError("Authentication configuration is missing");
            result.IsValid = false;
        }

        if (settings.AgenticOrchestration == null)
        {
            result.AddError("AgenticOrchestration configuration is missing");
            result.IsValid = false;
        }

        // Authentication validation
        if (settings.Authentication != null)
        {
            if (settings.Authentication.Enabled)
            {
                if (string.IsNullOrEmpty(settings.Authentication.Username))
                {
                    result.AddError("Authentication username is empty");
                    result.IsValid = false;
                }

                if (string.IsNullOrEmpty(settings.Authentication.Password))
                {
                    result.AddError("Authentication password is empty");
                    result.IsValid = false;
                }

                if (settings.Authentication.Password == "changeme" || settings.Authentication.Password == "admin")
                {
                    result.AddWarning("Using default/weak password - CHANGE THIS!");
                }
            }
        }

        // Kestrel validation
        if (settings.Kestrel != null && settings.Kestrel.Endpoints.Any())
        {
            foreach (var kvp in settings.Kestrel.Endpoints)
            {
                var endpoint = kvp.Value;
                if (string.IsNullOrEmpty(endpoint.Url))
                {
                    result.AddError($"Endpoint '{kvp.Key}' has empty URL");
                    result.IsValid = false;
                    continue;
                }

                // Parse URL to get port
                try
                {
                    var uri = new Uri(endpoint.Url.Replace("*", "localhost"));
                    if (uri.Port < 1 || uri.Port > 65535)
                    {
                        result.AddError($"Endpoint '{kvp.Key}' has invalid port: {uri.Port}");
                        result.IsValid = false;
                    }

                    // HTTPS validation
                    if (uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
                    {
                        if (endpoint.Certificate == null)
                        {
                            result.AddError($"HTTPS endpoint '{kvp.Key}' is missing certificate configuration");
                            result.IsValid = false;
                        }
                        else
                        {
                            ValidateCertificatePaths(result, endpoint.Certificate, configPath);
                        }
                    }
                }
                catch (Exception ex)
                {
                    result.AddError($"Invalid URL format for endpoint '{kvp.Key}': {ex.Message}");
                    result.IsValid = false;
                }
            }
        }
        else
        {
            result.AddWarning("No Kestrel endpoints configured - application will use default settings");
        }

        // Paths validation
        if (settings.AgenticOrchestration != null)
        {
            ValidateFilePath(result, "Database path", settings.AgenticOrchestration.DatabasePath, configPath, true);
            ValidateDirectoryPath(result, "Logs path", settings.AgenticOrchestration.LogsPath, configPath);
            ValidateDirectoryPath(result, "Uploads path", settings.AgenticOrchestration.Uploads.Path, configPath);
        }

        // Terminal validation
        if (settings.AgenticOrchestration?.Terminal != null)
        {
            var terminal = settings.AgenticOrchestration.Terminal;
            if (string.IsNullOrEmpty(terminal.DefaultCommand))
            {
                result.AddError("Terminal default command is empty");
                result.IsValid = false;
            }

            if (terminal.DefaultColumns <= 0)
            {
                result.AddError("Terminal columns must be greater than 0");
                result.IsValid = false;
            }

            if (terminal.DefaultRows <= 0)
            {
                result.AddError("Terminal rows must be greater than 0");
                result.IsValid = false;
            }

            if (terminal.MaxConcurrentSessions <= 0)
            {
                result.AddError("Max concurrent sessions must be greater than 0");
                result.IsValid = false;
            }

            if (terminal.SessionTimeoutMinutes <= 0)
            {
                result.AddError("Session timeout must be greater than 0");
                result.IsValid = false;
            }
        }

        // OpenAI validation
        if (settings.OpenAI != null)
        {
            if (string.IsNullOrEmpty(settings.OpenAI.ApiKey))
            {
                result.AddWarning("OpenAI API key not configured");
            }
            else if (!settings.OpenAI.ApiKey.StartsWith("sk-"))
            {
                result.AddWarning("OpenAI API key has unexpected format (should start with 'sk-')");
            }
        }

        return result;
    }

    private void ValidateCertificatePaths(ValidationResult result, CertificateConfig cert, string configPath)
    {
        var configDir = Path.GetDirectoryName(configPath) ?? Environment.CurrentDirectory;

        if (string.IsNullOrEmpty(cert.Path))
        {
            result.AddError("Certificate path is empty");
            result.IsValid = false;
        }
        else
        {
            var certPath = Path.IsPathRooted(cert.Path) ? cert.Path : Path.Combine(configDir, cert.Path);
            if (!File.Exists(certPath))
            {
                result.AddError($"Certificate file not found: {cert.Path}");
                result.IsValid = false;
            }
        }

        if (string.IsNullOrEmpty(cert.KeyPath))
        {
            result.AddError("Certificate key path is empty");
            result.IsValid = false;
        }
        else
        {
            var keyPath = Path.IsPathRooted(cert.KeyPath) ? cert.KeyPath : Path.Combine(configDir, cert.KeyPath);
            if (!File.Exists(keyPath))
            {
                result.AddError($"Certificate key file not found: {cert.KeyPath}");
                result.IsValid = false;
            }
        }
    }

    private void ValidateFilePath(ValidationResult result, string name, string path, string configPath, bool checkParentDir = false)
    {
        if (string.IsNullOrEmpty(path))
        {
            result.AddWarning($"{name} is empty");
            return;
        }

        try
        {
            var configDir = Path.GetDirectoryName(configPath) ?? Environment.CurrentDirectory;
            var fullPath = Path.IsPathRooted(path) ? path : Path.Combine(configDir, path);

            if (checkParentDir)
            {
                var parentDir = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
                {
                    result.AddWarning($"{name} parent directory does not exist: {parentDir}");
                }
            }
        }
        catch (Exception ex)
        {
            result.AddError($"{name} has invalid format: {ex.Message}");
            result.IsValid = false;
        }
    }

    private void ValidateDirectoryPath(ValidationResult result, string name, string path, string configPath)
    {
        if (string.IsNullOrEmpty(path))
        {
            result.AddWarning($"{name} is empty");
            return;
        }

        try
        {
            var configDir = Path.GetDirectoryName(configPath) ?? Environment.CurrentDirectory;
            var fullPath = Path.IsPathRooted(path) ? path : Path.Combine(configDir, path);

            if (!Directory.Exists(fullPath))
            {
                result.AddWarning($"{name} directory does not exist: {path}");
            }
        }
        catch (Exception ex)
        {
            result.AddError($"{name} has invalid format: {ex.Message}");
            result.IsValid = false;
        }
    }
}
