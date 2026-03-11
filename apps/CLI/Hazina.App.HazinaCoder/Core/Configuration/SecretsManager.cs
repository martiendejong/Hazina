using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Hazina.App.HazinaCoder.Core.Configuration;

/// <summary>
/// Manages secure storage and retrieval of sensitive configuration values.
/// Improvement #10: Secrets management
/// </summary>
public class SecretsManager
{
    private readonly string _secretsFilePath;
    private readonly string? _encryptionKey;
    private Dictionary<string, string> _secrets = new();

    public SecretsManager(string secretsDirectory, string? encryptionKey = null)
    {
        _secretsFilePath = Path.Combine(secretsDirectory, "appsettings.Secrets.json");
        _encryptionKey = encryptionKey ?? Environment.GetEnvironmentVariable("HAZINACODER_ENCRYPTION_KEY");
    }

    /// <summary>
    /// Loads secrets from secure storage
    /// </summary>
    public async Task<bool> LoadSecretsAsync()
    {
        if (!File.Exists(_secretsFilePath))
        {
            Console.WriteLine("[Secrets] No secrets file found, using environment variables only");
            LoadFromEnvironmentVariables();
            return false;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_secretsFilePath);
            var secretsData = JsonSerializer.Deserialize<Dictionary<string, string>>(json);

            if (secretsData != null)
            {
                _secrets = secretsData;

                // Decrypt secrets if encryption key is available
                if (!string.IsNullOrWhiteSpace(_encryptionKey))
                {
                    DecryptSecrets();
                }
            }

            // Merge with environment variables (environment takes precedence)
            LoadFromEnvironmentVariables();

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Secrets] Failed to load secrets: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Gets a secret value by key
    /// </summary>
    public string? GetSecret(string key)
    {
        return _secrets.TryGetValue(key, out var value) ? value : null;
    }

    /// <summary>
    /// Sets a secret value (in-memory only)
    /// </summary>
    public void SetSecret(string key, string value)
    {
        _secrets[key] = value;
    }

    /// <summary>
    /// Saves secrets to secure storage
    /// </summary>
    public async Task SaveSecretsAsync()
    {
        try
        {
            var secretsToSave = new Dictionary<string, string>(_secrets);

            // Encrypt secrets if encryption key is available
            if (!string.IsNullOrWhiteSpace(_encryptionKey))
            {
                EncryptSecrets(secretsToSave);
            }

            var json = JsonSerializer.Serialize(secretsToSave, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(_secretsFilePath, json);
            Console.WriteLine("[Secrets] Secrets saved successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Secrets] Failed to save secrets: {ex.Message}");
        }
    }

    /// <summary>
    /// Loads secrets from environment variables
    /// </summary>
    private void LoadFromEnvironmentVariables()
    {
        // Common secret keys
        var envKeys = new[]
        {
            "OPENAI_API_KEY",
            "ANTHROPIC_API_KEY",
            "AZURE_OPENAI_API_KEY",
            "GOOGLE_API_KEY",
            "MISTRAL_API_KEY"
        };

        foreach (var key in envKeys)
        {
            var value = Environment.GetEnvironmentVariable(key);
            if (!string.IsNullOrWhiteSpace(value))
            {
                _secrets[key] = value;
            }
        }
    }

    /// <summary>
    /// Encrypts secrets using AES encryption
    /// </summary>
    private void EncryptSecrets(Dictionary<string, string> secrets)
    {
        if (string.IsNullOrWhiteSpace(_encryptionKey))
        {
            return;
        }

        var keys = secrets.Keys.ToList();
        foreach (var key in keys)
        {
            secrets[key] = EncryptString(secrets[key], _encryptionKey);
        }
    }

    /// <summary>
    /// Decrypts secrets using AES encryption
    /// </summary>
    private void DecryptSecrets()
    {
        if (string.IsNullOrWhiteSpace(_encryptionKey))
        {
            return;
        }

        var keys = _secrets.Keys.ToList();
        foreach (var key in keys)
        {
            try
            {
                _secrets[key] = DecryptString(_secrets[key], _encryptionKey);
            }
            catch
            {
                // If decryption fails, assume it's not encrypted
            }
        }
    }

    private static string EncryptString(string plainText, string key)
    {
        using var aes = Aes.Create();
        aes.Key = DeriveKeyFromPassword(key);
        aes.GenerateIV();

        var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        using var ms = new MemoryStream();
        ms.Write(aes.IV, 0, aes.IV.Length);

        using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
        using (var sw = new StreamWriter(cs))
        {
            sw.Write(plainText);
        }

        return Convert.ToBase64String(ms.ToArray());
    }

    private static string DecryptString(string cipherText, string key)
    {
        var buffer = Convert.FromBase64String(cipherText);

        using var aes = Aes.Create();
        aes.Key = DeriveKeyFromPassword(key);

        var iv = new byte[aes.IV.Length];
        Array.Copy(buffer, 0, iv, 0, iv.Length);
        aes.IV = iv;

        var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        using var ms = new MemoryStream(buffer, iv.Length, buffer.Length - iv.Length);
        using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
        using var sr = new StreamReader(cs);

        return sr.ReadToEnd();
    }

    private static byte[] DeriveKeyFromPassword(string password)
    {
        using var sha256 = SHA256.Create();
        return sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
    }
}
