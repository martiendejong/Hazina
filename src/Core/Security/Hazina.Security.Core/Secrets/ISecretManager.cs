namespace Hazina.Security.Core.Secrets;

/// <summary>
/// Interface for secure secret management with encryption
/// </summary>
public interface ISecretManager
{
    /// <summary>
    /// Encrypts and stores a secret
    /// </summary>
    /// <param name="key">Secret identifier</param>
    /// <param name="value">Secret value to encrypt</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task StoreSecretAsync(string key, string value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves and decrypts a secret
    /// </summary>
    /// <param name="key">Secret identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Decrypted secret value or null if not found</returns>
    Task<string?> GetSecretAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a secret
    /// </summary>
    /// <param name="key">Secret identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteSecretAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a secret exists
    /// </summary>
    /// <param name="key">Secret identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<bool> SecretExistsAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Encrypts a value without storing it
    /// </summary>
    /// <param name="value">Value to encrypt</param>
    /// <returns>Encrypted value as base64 string</returns>
    string Encrypt(string value);

    /// <summary>
    /// Decrypts an encrypted value
    /// </summary>
    /// <param name="encryptedValue">Encrypted value as base64 string</param>
    /// <returns>Decrypted value</returns>
    string Decrypt(string encryptedValue);
}
