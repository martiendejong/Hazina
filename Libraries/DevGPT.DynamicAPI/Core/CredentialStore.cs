using System.Text.Json;

namespace DevGPT.DynamicAPI.Core;

/// <summary>
/// Centralized credential management system.
/// Loads credentials from files or environment variables.
/// </summary>
public class CredentialStore
{
    private readonly string _credentialsPath;
    private readonly Dictionary<string, Dictionary<string, string>> _cache = new();

    public CredentialStore(string credentialsPath)
    {
        _credentialsPath = credentialsPath;

        // Create credentials directory if it doesn't exist
        if (!Directory.Exists(_credentialsPath))
        {
            Directory.CreateDirectory(_credentialsPath);
        }
    }

    /// <summary>
    /// Get a credential value for a specific service and key.
    /// First checks cached credentials, then credential files, then environment variables.
    /// </summary>
    /// <param name="serviceName">Name of the service (e.g., "stripe", "google_analytics")</param>
    /// <param name="keyName">Name of the credential key (e.g., "api_key", "client_secret")</param>
    /// <returns>The credential value</returns>
    /// <exception cref="CredentialNotFoundException">Thrown when credential is not found</exception>
    public async Task<string> GetCredential(string serviceName, string keyName)
    {
        serviceName = serviceName.ToLower();

        // Check cache first
        if (_cache.TryGetValue(serviceName, out var serviceCredentials))
        {
            if (serviceCredentials.TryGetValue(keyName, out var cachedValue))
            {
                return cachedValue;
            }
        }

        // Try to load from file
        var fileValue = await LoadFromFile(serviceName, keyName);
        if (fileValue != null)
        {
            CacheCredential(serviceName, keyName, fileValue);
            return fileValue;
        }

        // Try to load from environment variable
        var envValue = LoadFromEnvironment(serviceName, keyName);
        if (envValue != null)
        {
            CacheCredential(serviceName, keyName, envValue);
            return envValue;
        }

        throw new CredentialNotFoundException($"Credential not found: {serviceName}/{keyName}");
    }

    /// <summary>
    /// Get all credentials for a specific service
    /// </summary>
    public async Task<Dictionary<string, string>> GetAllCredentials(string serviceName)
    {
        serviceName = serviceName.ToLower();

        // Check cache
        if (_cache.TryGetValue(serviceName, out var cached))
        {
            return new Dictionary<string, string>(cached);
        }

        // Load from file
        var credentials = await LoadAllFromFile(serviceName);
        if (credentials != null && credentials.Count > 0)
        {
            _cache[serviceName] = credentials;
            return new Dictionary<string, string>(credentials);
        }

        return new Dictionary<string, string>();
    }

    /// <summary>
    /// Store a credential in a file
    /// </summary>
    public async Task StoreCredential(string serviceName, string keyName, string value)
    {
        serviceName = serviceName.ToLower();

        var filePath = GetCredentialFilePath(serviceName);
        var credentials = await LoadAllFromFile(serviceName) ?? new Dictionary<string, string>();

        credentials[keyName] = value;

        var json = JsonSerializer.Serialize(credentials, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(filePath, json);

        // Update cache
        CacheCredential(serviceName, keyName, value);
    }

    /// <summary>
    /// Check if a credential exists
    /// </summary>
    public async Task<bool> HasCredential(string serviceName, string keyName)
    {
        try
        {
            await GetCredential(serviceName, keyName);
            return true;
        }
        catch (CredentialNotFoundException)
        {
            return false;
        }
    }

    /// <summary>
    /// List all available services with stored credentials
    /// </summary>
    public IEnumerable<string> ListServices()
    {
        if (!Directory.Exists(_credentialsPath))
        {
            return Enumerable.Empty<string>();
        }

        return Directory.GetFiles(_credentialsPath, "*.json")
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .Where(s => !string.IsNullOrEmpty(s))
            .Select(s => s!.ToLower());
    }

    private async Task<string?> LoadFromFile(string serviceName, string keyName)
    {
        var credentials = await LoadAllFromFile(serviceName);
        if (credentials != null && credentials.TryGetValue(keyName, out var value))
        {
            return value;
        }
        return null;
    }

    private async Task<Dictionary<string, string>?> LoadAllFromFile(string serviceName)
    {
        var filePath = GetCredentialFilePath(serviceName);

        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            var credentials = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            return credentials;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to load credentials from {filePath}: {ex.Message}");
            return null;
        }
    }

    private string? LoadFromEnvironment(string serviceName, string keyName)
    {
        // Try multiple environment variable formats
        var formats = new[]
        {
            $"{serviceName.ToUpper()}_{keyName.ToUpper()}",
            $"{serviceName.ToUpper()}{keyName.ToUpper()}",
            $"DEVGPT_{serviceName.ToUpper()}_{keyName.ToUpper()}"
        };

        foreach (var format in formats)
        {
            var value = Environment.GetEnvironmentVariable(format);
            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }
        }

        return null;
    }

    private void CacheCredential(string serviceName, string keyName, string value)
    {
        if (!_cache.ContainsKey(serviceName))
        {
            _cache[serviceName] = new Dictionary<string, string>();
        }
        _cache[serviceName][keyName] = value;
    }

    private string GetCredentialFilePath(string serviceName)
    {
        return Path.Combine(_credentialsPath, $"{serviceName}.json");
    }

    /// <summary>
    /// Clear all cached credentials (useful for testing or when credentials are updated)
    /// </summary>
    public void ClearCache()
    {
        _cache.Clear();
    }
}

public class CredentialNotFoundException : Exception
{
    public CredentialNotFoundException(string message) : base(message) { }
}
