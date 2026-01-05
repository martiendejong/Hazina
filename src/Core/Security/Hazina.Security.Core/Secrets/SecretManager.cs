using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text;

namespace Hazina.Security.Core.Secrets;

/// <summary>
/// Secure secret manager using ASP.NET Core Data Protection API
/// Provides encryption at rest for sensitive data like API keys
/// </summary>
public class SecretManager : ISecretManager
{
    private readonly IDataProtector _protector;
    private readonly ILogger<SecretManager> _logger;
    private readonly SecretManagerOptions _options;
    private readonly ConcurrentDictionary<string, string> _encryptedSecrets;

    public SecretManager(
        IDataProtectionProvider dataProtectionProvider,
        ILogger<SecretManager> logger,
        SecretManagerOptions options)
    {
        _protector = dataProtectionProvider.CreateProtector("Hazina.Secrets.v1");
        _logger = logger;
        _options = options;
        _encryptedSecrets = new ConcurrentDictionary<string, string>();

        _logger.LogInformation("[SECRET MANAGER] Initialized with storage: {StorageType}",
            _options.UseEnvironmentVariables ? "Environment + Memory" : "Memory only");
    }

    public Task StoreSecretAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        ValidateKey(key);
        ValidateValue(value);

        try
        {
            var encrypted = Encrypt(value);
            _encryptedSecrets[key] = encrypted;

            _logger.LogInformation("[SECRET MANAGER] Secret stored successfully for key: {Key}", MaskKey(key));

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SECRET MANAGER] Failed to store secret for key: {Key}", MaskKey(key));
            throw new SecretManagementException($"Failed to store secret for key: {key}", ex);
        }
    }

    public Task<string?> GetSecretAsync(string key, CancellationToken cancellationToken = default)
    {
        ValidateKey(key);

        try
        {
            // Try memory cache first
            if (_encryptedSecrets.TryGetValue(key, out var encrypted))
            {
                var decrypted = Decrypt(encrypted);
                _logger.LogDebug("[SECRET MANAGER] Secret retrieved from memory for key: {Key}", MaskKey(key));
                return Task.FromResult<string?>(decrypted);
            }

            // Fallback to environment variable if enabled
            if (_options.UseEnvironmentVariables)
            {
                var envValue = Environment.GetEnvironmentVariable(key);
                if (!string.IsNullOrEmpty(envValue))
                {
                    _logger.LogDebug("[SECRET MANAGER] Secret retrieved from environment for key: {Key}", MaskKey(key));
                    return Task.FromResult<string?>(envValue);
                }
            }

            _logger.LogDebug("[SECRET MANAGER] Secret not found for key: {Key}", MaskKey(key));
            return Task.FromResult<string?>(null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SECRET MANAGER] Failed to retrieve secret for key: {Key}", MaskKey(key));
            throw new SecretManagementException($"Failed to retrieve secret for key: {key}", ex);
        }
    }

    public Task DeleteSecretAsync(string key, CancellationToken cancellationToken = default)
    {
        ValidateKey(key);

        try
        {
            _encryptedSecrets.TryRemove(key, out _);
            _logger.LogInformation("[SECRET MANAGER] Secret deleted for key: {Key}", MaskKey(key));

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SECRET MANAGER] Failed to delete secret for key: {Key}", MaskKey(key));
            throw new SecretManagementException($"Failed to delete secret for key: {key}", ex);
        }
    }

    public Task<bool> SecretExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        ValidateKey(key);

        var exists = _encryptedSecrets.ContainsKey(key) ||
                     (_options.UseEnvironmentVariables && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)));

        return Task.FromResult(exists);
    }

    public string Encrypt(string value)
    {
        ValidateValue(value);

        try
        {
            var protectedBytes = _protector.Protect(Encoding.UTF8.GetBytes(value));
            return Convert.ToBase64String(protectedBytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SECRET MANAGER] Encryption failed");
            throw new SecretManagementException("Encryption failed", ex);
        }
    }

    public string Decrypt(string encryptedValue)
    {
        if (string.IsNullOrWhiteSpace(encryptedValue))
            throw new ArgumentNullException(nameof(encryptedValue));

        try
        {
            var protectedBytes = Convert.FromBase64String(encryptedValue);
            var unprotectedBytes = _protector.Unprotect(protectedBytes);
            return Encoding.UTF8.GetString(unprotectedBytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SECRET MANAGER] Decryption failed");
            throw new SecretManagementException("Decryption failed", ex);
        }
    }

    private void ValidateKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentNullException(nameof(key));

        if (key.Length > _options.MaxKeyLength)
            throw new ArgumentException($"Key length exceeds maximum of {_options.MaxKeyLength} characters", nameof(key));
    }

    private void ValidateValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentNullException(nameof(value));

        if (value.Length > _options.MaxSecretLength)
            throw new ArgumentException($"Secret length exceeds maximum of {_options.MaxSecretLength} characters", nameof(value));
    }

    private string MaskKey(string key)
    {
        if (key.Length <= 4)
            return "****";

        return $"{key.Substring(0, 2)}****{key.Substring(key.Length - 2)}";
    }
}

/// <summary>
/// Configuration options for SecretManager
/// </summary>
public class SecretManagerOptions
{
    /// <summary>
    /// Whether to use environment variables as fallback for secret retrieval
    /// </summary>
    public bool UseEnvironmentVariables { get; set; } = true;

    /// <summary>
    /// Maximum key length in characters
    /// </summary>
    public int MaxKeyLength { get; set; } = 256;

    /// <summary>
    /// Maximum secret length in characters
    /// </summary>
    public int MaxSecretLength { get; set; } = 10240; // 10KB

    /// <summary>
    /// Directory for storing encrypted secrets to disk (optional)
    /// </summary>
    public string? PersistencePath { get; set; }
}

/// <summary>
/// Exception thrown when secret management operations fail
/// </summary>
public class SecretManagementException : Exception
{
    public SecretManagementException(string message) : base(message) { }
    public SecretManagementException(string message, Exception innerException) : base(message, innerException) { }
}
