using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Hazina.Security.ApiKeys;

public sealed class VaultApiKeySecretVaultOptions
{
    /// <summary>Vault (Password Manager) base address, e.g. https://vault.prospergenics.com/.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>The app's vault API key (X-API-Key). Load it from configuration/secret store, never from source.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Vault project the raw keys are filed under.</summary>
    public int ProjectId { get; set; }

    /// <summary>Credential names are "{NamePrefix}: {key name} ({key prefix})".</summary>
    public string NamePrefix { get; set; } = "api-key";

    public string? Environment { get; set; }

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(15);
}

/// <summary>
/// <see cref="IApiKeySecretVault"/> over the Prospergenics vault REST API
/// (<c>/api/projects/{id}/credentials</c>). The raw key is stored as the credential's password
/// (type ApiKey); the reference returned is the credential id.
/// </summary>
public sealed class VaultApiKeySecretVault : IApiKeySecretVault
{
    public const string HttpClientName = "Hazina.Security.ApiKeys.Vault";
    private const int CredentialTypeApiKey = 1;

    private readonly IHttpClientFactory _httpClients;
    private readonly IOptions<VaultApiKeySecretVaultOptions> _options;

    public VaultApiKeySecretVault(IHttpClientFactory httpClients, IOptions<VaultApiKeySecretVaultOptions> options)
    {
        _httpClients = httpClients;
        _options = options;
    }

    public async Task<string> StoreAsync(ApiKeySecretEntry entry, CancellationToken ct = default)
    {
        var options = _options.Value;
        var client = _httpClients.CreateClient(HttpClientName);

        var name = $"{options.NamePrefix}: {entry.Name} ({entry.KeyPrefix})";
        var body = new
        {
            name = name.Length > 200 ? name[..200] : name,
            type = CredentialTypeApiKey,
            username = entry.KeyPrefix,
            password = entry.RawKey,
            notes = $"keyId={entry.KeyId}; tenant={entry.TenantId ?? "platform"}. Managed by Hazina.Security.ApiKeys; rotate or revoke through the owning app, not here.",
            tags = "hazina-api-key",
            environment = options.Environment,
        };

        var baseRoute = $"api/projects/{options.ProjectId}/credentials";
        HttpResponseMessage response;
        if (entry.ExistingReference is { Length: > 0 } existing)
        {
            response = await client.PutAsJsonAsync($"{baseRoute}/{Uri.EscapeDataString(existing)}", body, ct).ConfigureAwait(false);
            using (response)
            {
                // The update endpoint answers with the credential; the reference stays the same.
                Ensure(response, "update");
                return existing;
            }
        }

        response = await client.PostAsJsonAsync(baseRoute, body, ct).ConfigureAwait(false);
        using (response)
        {
            Ensure(response, "create");
            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false), cancellationToken: ct).ConfigureAwait(false);
            if (!doc.RootElement.TryGetProperty("id", out var id))
                throw new InvalidOperationException("Vault did not return a credential id for the stored API key.");
            return id.ToString();
        }
    }

    public async Task DeleteAsync(string reference, CancellationToken ct = default)
    {
        var client = _httpClients.CreateClient(HttpClientName);
        using var response = await client.DeleteAsync(
            $"api/projects/{_options.Value.ProjectId}/credentials/{Uri.EscapeDataString(reference)}", ct).ConfigureAwait(false);

        if (response.StatusCode != System.Net.HttpStatusCode.NotFound) // already gone is fine
            Ensure(response, "delete");
    }

    // Status only: a vault response body must never be echoed into exceptions/logs, it may carry secrets.
    private static void Ensure(HttpResponseMessage response, string operation)
    {
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Vault {operation} of an API key secret failed with HTTP {(int)response.StatusCode}.");
    }
}
